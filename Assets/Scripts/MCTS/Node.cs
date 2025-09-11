using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// MCTS node for use with the ChessState class.
/// - State is the game state _at_ this node (i.e. after the Move was applied).
/// - PlayerJustMoved = +1 for White, -1 for Black (the player who made the move that produced this state).
/// </summary>
public class Node
{
    public ChessState State;
    public Node Parent;
    public List<Node> Children = new List<Node>();

    // Move that was applied on the parent to get to this node.
    // Root's Move is null.
    public ChessState.Move? MoveFromParent;

    // bookkeeping for MCTS
    public int Visits = 0;
    public double TotalValue = 0.0; // sum of values (see Backpropagate comment)
    public List<ChessState.Move> UntriedMoves;

    // which player just moved to produce this node (1 = white, -1 = black)
    public int PlayerJustMoved;

    private static readonly Random rng = new Random();

    public Node(ChessState state, Node parent = null, ChessState.Move? moveFromParent = null)
    {
        State = state;
        Parent = parent;
        MoveFromParent = moveFromParent;
        PlayerJustMoved = state.whiteToMove ? -1 : 1;
        // copy legal moves so we can pop from UntriedMoves during Expand
        var legal = state.GenerateLegalMoves();
        UntriedMoves = new List<ChessState.Move>(legal);
    }

    public bool IsFullyExpanded => UntriedMoves.Count == 0;
    public bool IsLeaf => Children.Count == 0;

    /// <summary>
    /// Expand by taking one untried move (randomly), applying it to a clone of the state,
    /// creating the child node and returning it.
    /// </summary>
    public Node Expand()
    {
        if (UntriedMoves.Count == 0) return null;
        int idx = rng.Next(UntriedMoves.Count);
        ChessState.Move m = UntriedMoves[idx];
        UntriedMoves.RemoveAt(idx);

        ChessState nextState = State.Clone();
        nextState.ApplyMove(m);

        Node child = new Node(nextState, this, m);
        Children.Add(child);
        return child;
    }

    /// <summary>
    /// Selects the best child according to UCT: Q/V + c * sqrt( ln(N) / n )
    /// If any child has Visits == 0 it will be preferred (infinite exploration term).
    /// </summary>
    public Node BestChild(double explorationConstant = 1.4142135623730951)
    {
        if (Children.Count == 0) return null;

        double bestScore = double.NegativeInfinity;
        Node best = null;

        foreach (var child in Children)
        {
            if (child.Visits == 0)
            {
                // prefer unvisited child immediately (tie-break randomly)
                return child;
            }

            double q = child.TotalValue / child.Visits; // average value (value is from perspective of child.PlayerJustMoved)
            double u = explorationConstant * Math.Sqrt(Math.Log(Math.Max(1, this.Visits)) / child.Visits);
            double score = q + u;
            if (score > bestScore)
            {
                bestScore = score;
                best = child;
            }
        }

        return best ?? Children[rng.Next(Children.Count)];
    }

    /// <summary>
    /// Backpropagate a leaf reward up to the root.
    /// leafReward: in [0..1], where 1.0 means a win for leafPlayer, 0.0 means a loss for leafPlayer, 0.5 draw.
    /// leafPlayer: +1 if the player who just moved at the leaf was White, -1 if Black.
    ///
    /// For every ancestor node we add the value from the perspective of that node's PlayerJustMoved:
    ///   if ancestor.PlayerJustMoved == leafPlayer  => add leafReward
    ///   else                                       => add (1 - leafReward)
    /// This keeps TotalValue consistently representing "sum of outcomes for PlayerJustMoved".
    /// </summary>
    public void Backpropagate(double leafReward, int leafPlayer)
    {
        Node node = this;
        while (node != null)
        {
            double valueForNode = (node.PlayerJustMoved == leafPlayer) ? leafReward : (1.0 - leafReward);
            node.Visits++;
            node.TotalValue += valueForNode;
            node = node.Parent;
        }
    }

    /// <summary>
    /// Convenience: choose child with highest visit count (common final move selection).
    /// </summary>
    public Node MostVisitedChild()
    {
        if (Children.Count == 0) return null;
        return Children.OrderByDescending(c => c.Visits).First();
    }

    /// <summary>
    /// Helper to convert ChessState.IsTerminal() result (the ChessState implementation returns:
    ///   +1 = white win, -1 = white loss (black win), 0 = draw)
    /// into a [0..1] leafReward for the given leafPlayer (player who just moved at the leaf).
    /// </summary>
    public static double TerminalResultToLeafReward(float terminalResult, int leafPlayerJustMoved)
    {
        if (Math.Abs(terminalResult) < 1e-9) return 0.5; // draw
        if (terminalResult > 0) // white won
            return leafPlayerJustMoved == 1 ? 1.0 : 0.0;
        else // black won
            return leafPlayerJustMoved == -1 ? 1.0 : 0.0;
    }

    public override string ToString()
    {
        return $"Node(move={(MoveFromParent.HasValue ? MoveFromParent.Value.ToString() : "root")}, visits={Visits}, value={TotalValue:F2}, untried={UntriedMoves.Count}, children={Children.Count})";
    }
}
