using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight GameTree wrapper around Node for MCTS usage.
/// Holds the root node and provides helpers commonly used by a MCTS driver:
/// - selection entry (SelectLeaf)
/// - expanding root children (ExpandAllRootChildren / ExpandOne)
/// - updating the root after a real move (UpdateRootAfterMove) to reuse subtree
/// - retrieving final move choice by visits (GetBestMoveByVisits)
/// This is NOT a MonoBehaviour; use from your controller script.
/// </summary>
public class GameTree
{
    public Node root;

    public GameTree(ChessState rootState)
    {
        if (rootState == null) throw new ArgumentNullException(nameof(rootState));
        root = new Node(rootState.Clone());
    }

    public GameTree(Node existingRoot)
    {
        if (existingRoot == null) throw new ArgumentNullException(nameof(existingRoot));
        root = existingRoot;
    }

    /// <summary>
    /// Replace the tree root with a fresh node built from state (clone).
    /// </summary>
    public void SetRootState(ChessState state)
    {
        root = new Node(state.Clone());
    }

    /// <summary>
    /// Select a leaf node starting from the root using Node.BestChild selection.
    /// Stops when it finds a node that is not fully expanded (has untried moves) or is terminal.
    /// Returns the selected node (may be the root).
    /// </summary>
    public Node SelectLeaf(double explorationConstant = 1.4142135623730951)
    {
        Node node = root;
        while (node.IsFullyExpanded && node.Children.Count > 0)
        {
            node = node.BestChild(explorationConstant);
            if (node == null) break;
        }
        return node;
    }

    /// <summary>
    /// Expand a single child of the provided node (random untried move).
    /// If node is null, expands root.
    /// Returns the newly created child or null if no expansion was possible.
    /// </summary>
    public Node ExpandOne(Node node = null)
    {
        if (node == null) node = root;
        if (!node.IsFullyExpanded) return node.Expand();
        return null;
    }

    /// <summary>
    /// Expand all untried moves at root (creates children for every legal move).
    /// Useful to prepare a shallow tree or to inspect all root moves before playouts.
    /// </summary>
    public void ExpandAllRootChildren()
    {
        while (!root.IsFullyExpanded)
        {
            root.Expand();
        }
    }

    /// <summary>
    /// After the real game plays `move`, try to reuse the subtree by making the corresponding child the new root.
    /// If the child is found, its parent reference is cleared and it becomes the root (retaining its subtree).
    /// If not found, a new root Node is created from applying the move to the current root state.
    /// </summary>
    public void UpdateRootAfterMove(ChessState.Move move)
    {
        if (root == null) return;

        Node match = FindChildMatchingMove(root, move);
        if (match != null)
        {
            // detach
            match.Parent = null;
            root = match;
        }
        else
        {
            // apply to a clone of the current root state and make a fresh node
            ChessState next = root.State.Clone();
            next.ApplyMove(move);
            root = new Node(next, null, move);
        }
    }

    /// <summary>
    /// Find a direct child of `node` whose MoveFromParent equals `move`.
    /// </summary>
    private Node FindChildMatchingMove(Node node, ChessState.Move move)
    {
        foreach (var c in node.Children)
        {
            if (c.MoveFromParent.HasValue && MovesEqual(c.MoveFromParent.Value, move))
                return c;
        }
        return null;
    }

    /// <summary>
    /// Compare two moves by from/to/promotion/isEnPassant/isCastle (enough to identify).
    /// </summary>
    private bool MovesEqual(ChessState.Move a, ChessState.Move b)
    {
        return a.from == b.from
            && a.to == b.to
            && a.promotion == b.promotion
            && a.isEnPassant == b.isEnPassant
            && a.isCastle == b.isCastle;
    }

    /// <summary>
    /// Choose the move with the highest visit count from root's children.
    /// Returns null if no children exist.
    /// </summary>
    public ChessState.Move? GetBestMoveByVisits()
    {
        if (root == null || root.Children.Count == 0) return null;
        Node best = null;
        int bestVisits = -1;
        foreach (var c in root.Children)
        {
            if (c.Visits > bestVisits)
            {
                bestVisits = c.Visits;
                best = c;
            }
        }
        return best?.MoveFromParent;
    }

    /// <summary>
    /// Get the child of root which has the best average value (TotalValue/Visits).
    /// Useful if you prefer value-maximizing selection instead of visits.
    /// </summary>
    public ChessState.Move? GetBestMoveByValue()
    {
        if (root == null || root.Children.Count == 0) return null;
        Node best = null;
        double bestAvg = double.NegativeInfinity;
        foreach (var c in root.Children)
        {
            if (c.Visits == 0) continue;
            double avg = c.TotalValue / c.Visits;
            if (avg > bestAvg)
            {
                bestAvg = avg;
                best = c;
            }
        }
        // fallback to most visited if no child has visits yet
        if (best == null) return GetBestMoveByVisits();
        return best.MoveFromParent;
    }

    /// <summary>
    /// Reset the tree by creating a fresh root from the provided state.
    /// </summary>
    public void Reset(ChessState state)
    {
        root = new Node(state.Clone());
    }
}
