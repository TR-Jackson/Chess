using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// OpponentAI: runs MCTS on a background Task and stops when CancelSearch() is called.
/// - Emits OnMoveChosen on the main thread when the search is cancelled (player pressed button) or when search finishes.
/// - Does NOT touch Unity GameObjects from the worker thread; it only works with ChessState/GameTree/Node (lightweight classes).
/// 
/// Usage:
/// - Subscribe to OnMoveChosen to receive the chosen ChessState.Move and apply it to your board/controller on the main thread.
/// - Call MakeMove() to start the search.
/// - Call CancelSearch() (from UI button) to stop search and apply current best move.
/// </summary>
public class OpponentAI : MonoBehaviour
{
    public GameObject WhitePieces; // assign in Inspector
    public GameObject BlackPieces; // assign in Inspector

    // Public event consumers can subscribe to to receive the chosen move on the main thread.
    // Example: ai.OnMoveChosen += move => MyBoardController.ApplyMove(move);
    public Action<ChessState.Move> OnMoveChosen;

    // Search configuration
    [Tooltip("Maximum playout depth in Simulation")]
    public int maxPlayoutDepth = 256;

    [Tooltip("Exploration constant (UCT)")]
    public double explorationConstant = 1.4142135623730951;

    // Internal
    private GameTree tree;
    private CancellationTokenSource cts;
    private readonly object treeLock = new object();

    // Safely published chosen move (set by worker under lock, consumed in Update on main thread)
    private bool chosenMoveReady = false;
    private ChessState.Move chosenMove;

    // RNG used in simulation (worker uses its own RNG)
    private System.Random rng = new System.Random();

    void Start()
    {

    }

    void Update()
    {
        // If worker finished and picked a move, invoke the callback on main thread and clear flag.
        ChessState.Move moveToApply;
        bool fire = false;
        lock (treeLock)
        {
            if (chosenMoveReady)
            {
                moveToApply = chosenMove;
                chosenMoveReady = false;
                fire = true;
            }
            else moveToApply = default;
        }

        if (fire)
        {
            try
            {
                OnMoveChosen?.Invoke(moveToApply);
            }
            catch (Exception ex)
            {
                Debug.LogError($"OpponentAI: exception while invoking OnMoveChosen: {ex}");
            }
        }
    }

    /// <summary>
    /// External entry to start a search (AI to make move). Requires you to have set up the tree/root beforehand.
    /// If tree is null, this will create a tree from a new ChessState start position.
    /// </summary>
    /// 
    public void MakeMove()
    {
        // start search on background task
        if (cts != null)
        {
            Debug.LogWarning("OpponentAI: search already running.");
            return;
        }

        // Ensure there is a root tree
        lock (treeLock)
        {
            if (tree == null)
            {
                if (WhitePieces == null || BlackPieces == null)
                {
                    Debug.LogError("WhitePieces or BlackPieces not assigned!");
                    return;
                }

                ChessState currentState = ChessState.FromBoardGameObjects(WhitePieces, BlackPieces, whiteToMove: false);
                tree = new GameTree(currentState);
                Debug.Log(tree.root.State.ToString());
            }
        }

        cts = new CancellationTokenSource();
        var token = cts.Token;

        Task.Run(() => MCTSLoop(token), token);
    }

    /// <summary>
    /// Request the running search to stop. The worker will pick the current best move and publish it to main thread.
    /// Safe to call from UI button.
    /// </summary>
    public void CancelSearch()
    {
        if (cts == null) return;
        cts.Cancel();
    }

    /// <summary>
    /// The core MCTS driver loop running on a background thread.
    /// It repeatedly runs iterations until cancellation is requested.
    /// </summary>
    private void MCTSLoop(CancellationToken token)
    {
        // Local RNG for this task
        var localRng = new System.Random();

        // We'll run iterations until cancelled. Do not touch Unity APIs here.
        try
        {
            while (!token.IsCancellationRequested)
            {
                Node leaf;
                lock (treeLock)
                {
                    // selection (stops on node with untried moves or terminal)
                    leaf = tree.SelectLeaf(explorationConstant);
                }

                if (leaf == null)
                {
                    // nothing to do
                    continue;
                }

                // If terminal state, we can backpropagate its value directly
                float terminalResult;
                bool isTerminal;
                lock (treeLock)
                {
                    isTerminal = leaf.State.IsTerminal(out terminalResult);
                }

                if (isTerminal)
                {
                    // terminal: compute reward and backpropagate
                    double reward = Node.TerminalResultToLeafReward(terminalResult, leaf.PlayerJustMoved);
                    lock (treeLock)
                    {
                        leaf.Backpropagate(reward, leaf.PlayerJustMoved);
                    }

                    // continue to next iteration
                    continue;
                }

                // Expansion: create one child if possible
                Node nodeToSimulate = null;
                lock (treeLock)
                {
                    if (!leaf.IsFullyExpanded)
                    {
                        nodeToSimulate = leaf.Expand();
                    }
                    else if (leaf.Children.Count > 0)
                    {
                        // fully expanded: choose best child to descend into for simulation
                        nodeToSimulate = leaf.BestChild(explorationConstant);
                    }
                    else
                    {
                        // fallback: simulate from leaf itself
                        nodeToSimulate = leaf;
                    }
                }

                if (nodeToSimulate == null)
                {
                    continue;
                }

                // Simulation (rollout) - operate on a local clone (do not touch shared tree state)
                ChessState rolloutState;
                lock (treeLock)
                {
                    rolloutState = nodeToSimulate.State.Clone();
                }

                double rolloutValue = SimulateRollout(rolloutState, localRng, token);

                // Backpropagate the rollout result up the tree
                lock (treeLock)
                {
                    // nodeToSimulate.PlayerJustMoved is the player who just moved to create this node
                    nodeToSimulate.Backpropagate(rolloutValue, nodeToSimulate.PlayerJustMoved);
                }

                // Loop continues until cancellation is requested
            }
        }
        catch (OperationCanceledException)
        {
            // expected on cancellation
        }
        catch (Exception ex)
        {
            Debug.LogError($"OpponentAI MCTSLoop exception: {ex}");
        }
        finally
        {
            // When search stops (either cancelled or finished loop), pick current best move and publish it for main thread to apply.
            ChessState.Move? bestMove = null;
            int totalRootVisits = 0;
            Node bestNode = null;

            lock (treeLock)
            {
                if (tree != null)
                {
                    bestMove = tree.GetBestMoveByVisits();
                    if (tree.root != null)
                    {
                        totalRootVisits = tree.root.Visits;
                        // find the Node corresponding to bestMove
                        foreach (var child in tree.root.Children)
                        {
                            if (child.MoveFromParent.HasValue && MovesEqual(child.MoveFromParent.Value, bestMove.Value))
                            {
                                bestNode = child;
                                break;
                            }
                        }
                    }
                }
            }

            if (bestMove.HasValue)
            {
                lock (treeLock)
                {
                    chosenMove = bestMove.Value;
                    chosenMoveReady = true;

                    // Convert square indices to coordinates
                    string fromCoord = SquareToCoords(bestMove.Value.from);
                    string toCoord = SquareToCoords(bestMove.Value.to);

                    // Compute certainty as fraction of visits
                    double certainty = 0.0;
                    if (bestNode != null && totalRootVisits > 0)
                        certainty = (double)bestNode.Visits / totalRootVisits;

                    // Compute max search depth under bestNode
                    int maxDepth = 0;
                    if (bestNode != null)
                    {
                        maxDepth = GetMaxDepth(bestNode);
                    }
                    tree = null;
                    Debug.Log($"AI move from {fromCoord} to {toCoord}, certainty={certainty:P1}, max depth={maxDepth}, visits={bestNode?.Visits ?? 0}, total root visits={totalRootVisits}");
                }
            }

            // dispose cancellation token source and allow new searches
            cts.Dispose();
            cts = null;
        }
    }

    /// <summary>
    /// Recursively compute the maximum depth from the given node down its subtree.
    /// </summary>
    private int GetMaxDepth(Node node)
    {
        if (node.Children.Count == 0) return 0;
        int maxChildDepth = 0;
        foreach (var child in node.Children)
        {
            int d = GetMaxDepth(child);
            if (d > maxChildDepth) maxChildDepth = d;
        }
        return 1 + maxChildDepth;
    }

    /// <summary>
    /// Utility: compare moves for equality
    /// </summary>
    private bool MovesEqual(ChessState.Move a, ChessState.Move b)
    {
        return a.from == b.from
            && a.to == b.to
            && a.promotion == b.promotion
            && a.isEnPassant == b.isEnPassant
            && a.isCastle == b.isCastle;
    }

    public static string SquareToCoords(int square)
    {
        int file = square & 7;        // 0..7
        int rank = square >> 3;       // 0..7

        char fileChar = (char)('A' + file);      // A..H
        char rankChar = (char)('1' + rank);      // 1..8

        return $"{fileChar}{rankChar}";
    }

    /// <summary>
    /// Random playout from the given state until terminal or maxPlayoutDepth.
    /// Returns a leaf reward in [0..1] representing the perspective of the player who just moved at the leaf.
    /// </summary>
    private double SimulateRollout(ChessState state, System.Random localRng, CancellationToken token)
    {
        int depth = 0;
        while (!token.IsCancellationRequested)
        {
            float terminalResult;
            if (state.IsTerminal(out terminalResult))
            {
                int leafPlayer = state.whiteToMove ? -1 : 1;
                return Node.TerminalResultToLeafReward(terminalResult, leafPlayer);
            }

            var legal = state.GenerateLegalMoves();
            if (legal.Count == 0) return 0.5; // stalemate

            // assign a weight to each move
            List<(ChessState.Move move, double weight)> weightedMoves = new List<(ChessState.Move, double)>();
            foreach (var m in legal)
            {
                double w = 1.0; // base weight

                // prefer captures
                int targetPiece = state.board[m.to];
                if (targetPiece != 0) w += Math.Abs(targetPiece);

                // penalize moves that leave own king in check
                if (state.WouldMoveCauseCheck(m)) w = 0.01;

                // central control bonus
                int rank = m.to >> 3;
                int file = m.to & 7;
                if (rank >= 2 && rank <= 5 && file >= 2 && file <= 5) w += 0.5;

                weightedMoves.Add((m, w));
            }

            // weighted random selection
            double totalWeight = 0;
            foreach (var wm in weightedMoves) totalWeight += wm.weight;
            double r = localRng.NextDouble() * totalWeight;
            ChessState.Move selected = weightedMoves[0].move;
            foreach (var wm in weightedMoves)
            {
                if (r < wm.weight) { selected = wm.move; break; }
                r -= wm.weight;
            }

            state.ApplyMove(selected);

            depth++;
            if (depth >= maxPlayoutDepth) break;
        }

        // non-terminal or depth cutoff: use material heuristic
        double eval = EvaluateMaterial(state); // positive -> white ahead
        double scaled = 1.0 / (1.0 + Math.Exp(-eval * 0.1));
        scaled = Math.Max(0.01, Math.Min(0.99, scaled));

        int leafPlayerJustMoved = state.whiteToMove ? -1 : 1;
        return (leafPlayerJustMoved == 1) ? scaled : (1.0 - scaled);
    }


    /// <summary>
    /// Very simple material evaluation: pawn=1, knight=3, bishop=3, rook=5, queen=9
    /// positive = white ahead, negative = black ahead
    /// </summary>
    private double EvaluateMaterial(ChessState s)
    {
        double score = 0.0;
        for (int i = 0; i < 64; i++)
        {
            int p = s.board[i];
            if (p == 0) continue;
            int abs = Math.Abs(p);
            double val = 0;
            switch (abs)
            {
                case 1: val = 1.0; break;
                case 2: val = 3.0; break;
                case 3: val = 3.0; break;
                case 4: val = 5.0; break;
                case 5: val = 9.0; break;
                case 6: val = 0.0; break; // king not counted for material
            }
            score += Math.Sign(p) * val;
        }
        return score;
    }

}
