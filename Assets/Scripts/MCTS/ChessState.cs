using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary> MADE WITH CHATGPT
/// Lightweight chess state suitable for MCTS.
/// Board is a 0..63 array: square = rank*8 + file, rank 0 = White's 1st rank (a1=0, h1=7, a8=56, h8=63).
/// Piece encoding: 0 = empty; 1=P,2=N,3=B,4=R,5=Q,6=K for White; negative for Black.
/// </summary>
public class ChessState
{
    public int[] board = new int[64];
    public bool whiteToMove = true;
    public bool whiteCanCastleKing = true;
    public bool whiteCanCastleQueen = true;
    public bool blackCanCastleKing = true;
    public bool blackCanCastleQueen = true;
    public int enPassantSquare = -1; // square index that can be captured en-passant, -1 if none
    public int halfmoveClock = 0;
    public int fullmoveNumber = 1;

    /// <summary>
    /// Create a ChessState from your Unity board GameObjects.
    /// You can scan the pieces on the board and fill `board[]`, `whiteToMove`, castling rights, etc.
    /// This is a stub to implement later.
    /// </summary>
    /// <param name="boardParent">Parent GameObject containing all pieces (children)</param>
    /// <param name="whiteToMove">Which side is to move</param>
    /// <returns>New ChessState representing current board</returns>
    public static ChessState FromBoardGameObjects(GameObject WhitePieces, GameObject BlackPieces, bool whiteToMove = false)
    {
        ChessState state = new ChessState();
        state.board = new int[64];
        state.whiteToMove = whiteToMove;

        // Helper: convert world position (x,y) to square index 0..63
        int PosToSquare(Vector3 pos)
        {
            // x from -3.5..3.5 maps to file 0..7
            int file = Mathf.RoundToInt(pos.x + 3.5f);
            // y from -3.5..3.5 maps to rank 0..7
            int rank = Mathf.RoundToInt(pos.y + 3.5f);
            return rank * 8 + file;
        }

        // Helper: map piece name to piece code
        int NameToPieceCode(string name, bool isWhite)
        {
            int sign = isWhite ? 1 : -1;
            name = name.ToLower();
            if (name.Contains("pawn")) return 1 * sign;
            if (name.Contains("knight")) return 2 * sign;
            if (name.Contains("bishop")) return 3 * sign;
            if (name.Contains("rook")) return 4 * sign;
            if (name.Contains("queen")) return 5 * sign;
            if (name.Contains("king")) return 6 * sign;
            return 0;
        }

        // Fill white pieces
        foreach (Transform piece in WhitePieces.transform)
        {
            int sq = PosToSquare(piece.position);
            state.board[sq] = NameToPieceCode(piece.name, true);
        }

        // Fill black pieces
        foreach (Transform piece in BlackPieces.transform)
        {
            int sq = PosToSquare(piece.position);
            state.board[sq] = NameToPieceCode(piece.name, false);
        }

        return state;
    }
    public ChessState() { SetStartPosition(); }

    public ChessState Clone()
    {
        return new ChessState()
        {
            board = (int[])this.board.Clone(),
            whiteToMove = this.whiteToMove,
            whiteCanCastleKing = this.whiteCanCastleKing,
            whiteCanCastleQueen = this.whiteCanCastleQueen,
            blackCanCastleKing = this.blackCanCastleKing,
            blackCanCastleQueen = this.blackCanCastleQueen,
            enPassantSquare = this.enPassantSquare,
            halfmoveClock = this.halfmoveClock,
            fullmoveNumber = this.fullmoveNumber
        };
    }

    public void SetStartPosition()
    {
        // standard start
        int[] start = new int[]
        {
            4,2,3,5,6,3,2,4,  // rank 1 a1..h1 white
            1,1,1,1,1,1,1,1,  // rank 2
            0,0,0,0,0,0,0,0,  // rank 3
            0,0,0,0,0,0,0,0,  // rank 4
            0,0,0,0,0,0,0,0,  // rank 5
            0,0,0,0,0,0,0,0,  // rank 6
            -1,-1,-1,-1,-1,-1,-1,-1, // rank 7 black pawns
            -4,-2,-3,-5,-6,-3,-2,-4  // rank 8 black back rank
        };
        start.CopyTo(board, 0);
        whiteToMove = true;
        whiteCanCastleKing = whiteCanCastleQueen = blackCanCastleKing = blackCanCastleQueen = true;
        enPassantSquare = -1;
        halfmoveClock = 0;
        fullmoveNumber = 1;
    }


    public struct Move
    {
        public int from;
        public int to;
        public int promotion; // 0 = none, otherwise piece code (2=N,3=B,4=R,5=Q) positive for white, negative for black but we use absolute and apply sign by side
        public bool isEnPassant;
        public bool isCastle;
        public int captured; // captured piece code
        public Move(int f, int t) { from = f; to = t; promotion = 0; isEnPassant = false; isCastle = false; captured = 0; }
        public override string ToString() => $"Move {from}->{to}" + (promotion != 0 ? $" prom={promotion}" : "");
    }

    // utility
    private static int Rank(int sq) => sq >> 3;
    private static int File(int sq) => sq & 7;
    private static bool InBoard(int sq) => sq >= 0 && sq < 64;

    // Apply a move (mutates). Designed so Clone() + ApplyMove can be used in search.
    public void ApplyMove(Move m)
    {
        int piece = board[m.from];
        int side = piece > 0 ? 1 : -1;
        m.captured = board[m.to];

        // detect en-passant capture
        if (m.isEnPassant)
        {
            if (side == 1)
            {
                // white captures black pawn behind to-square
                int capSq = m.to - 8;
                m.captured = board[capSq];
                board[capSq] = 0;
            }
            else
            {
                int capSq = m.to + 8;
                m.captured = board[capSq];
                board[capSq] = 0;
            }
        }

        // handle castling - move rook
        if (m.isCastle)
        {
            if (piece == 6) // white king
            {
                if (m.to == 6) // king-side (e1->g1)
                {
                    board[7] = 0; board[5] = 4; // h1->f1 rook
                }
                else if (m.to == 2) // queen-side (e1->c1)
                {
                    board[0] = 0; board[3] = 4; // a1->d1 rook
                }
            }
            else if (piece == -6) // black king
            {
                if (m.to == 62) // e8->g8
                {
                    board[63] = 0; board[61] = -4;
                }
                else if (m.to == 58) // e8->c8
                {
                    board[56] = 0; board[59] = -4;
                }
            }
        }

        // move piece
        board[m.to] = board[m.from];
        board[m.from] = 0;

        // handle promotion
        if (m.promotion != 0)
        {
            int promotedPiece = m.promotion * (side); // promotion passed as positive (2..5) then apply side
            board[m.to] = promotedPiece;
        }

        // update castling rights
        UpdateCastlingRightsAfterMove(m.from, m.to, piece);

        // set en-passant square
        if (Math.Abs(piece) == 1 && Math.Abs(m.to - m.from) == 16)
        {
            // pawn double move: en-passant square is the square jumped over
            enPassantSquare = (m.from + m.to) >> 1;
        }
        else enPassantSquare = -1;

        // update halfmove/fullmove
        if (Math.Abs(piece) == 1 || m.captured != 0) halfmoveClock = 0; else halfmoveClock++;
        if (!whiteToMove) fullmoveNumber++; // after black moves increment

        // flip side
        whiteToMove = !whiteToMove;
    }

    private void UpdateCastlingRightsAfterMove(int from, int to, int piece)
    {
        // if king moves, lose both sides for that color
        if (piece == 6) { whiteCanCastleKing = whiteCanCastleQueen = false; }
        if (piece == -6) { blackCanCastleKing = blackCanCastleQueen = false; }

        // if rook moves from original squares, lose rights
        if (from == 0 || to == 0) whiteCanCastleQueen = false;
        if (from == 7 || to == 7) whiteCanCastleKing = false;
        if (from == 56 || to == 56) blackCanCastleQueen = false;
        if (from == 63 || to == 63) blackCanCastleKing = false;

        // if capture rook on original square, lose rights
        if (to == 0) whiteCanCastleQueen = false;
        if (to == 7) whiteCanCastleKing = false;
        if (to == 56) blackCanCastleQueen = false;
        if (to == 63) blackCanCastleKing = false;
    }

    // Generate all legal moves for current side
    public List<Move> GenerateLegalMoves()
    {
        List<Move> moves = new List<Move>();
        int side = whiteToMove ? 1 : -1;

        // Generate pseudo-legal moves
        for (int sq = 0; sq < 64; sq++)
        {
            int p = board[sq];
            if (p == 0 || Math.Sign(p) != side) continue;
            int abs = Math.Abs(p);
            switch (abs)
            {
                case 1: GeneratePawnMoves(sq, side, moves); break;
                case 2: GenerateKnightMoves(sq, side, moves); break;
                case 3: GenerateSlidingMoves(sq, side, moves, new int[] { 9, 7, -9, -7 }); break; // bishop
                case 4: GenerateSlidingMoves(sq, side, moves, new int[] { 8, -8, 1, -1 }); break; // rook
                case 5: GenerateSlidingMoves(sq, side, moves, new int[] { 9, 7, -9, -7, 8, -8, 1, -1 }); break; // queen
                case 6: GenerateKingMoves(sq, side, moves); break;
            }
        }

        // Filter illegal (leaves king in check) by making the move on a clone
        List<Move> legal = new List<Move>();
        foreach (var m in moves)
        {
            ChessState s2 = Clone();
            s2.ApplyMove(m);
            if (!s2.IsKingInCheck(!whiteToMove)) legal.Add(m); // check king of side who just moved? Actually IsKingInCheck(side) expects side-to-check: we want to know if the side who just moved left their own king in check => check the side that moved = !current side
        }
        return legal;
    }

    private void GeneratePawnMoves(int sq, int side, List<Move> moves)
    {
        int forward = side == 1 ? 8 : -8;
        int startRank = side == 1 ? 1 : 6;
        int promoteRank = side == 1 ? 7 : 0;
        int r = Rank(sq), f = File(sq);
        int one = sq + forward;
        if (InBoard(one) && board[one] == 0)
        {
            // promotion?
            if (Rank(one) == promoteRank)
            {
                AddPawnPromotionMoves(sq, one, side, moves);
            }
            else moves.Add(new Move(sq, one));

            // double
            int two = sq + forward * 2;
            if (r == startRank && board[two] == 0) moves.Add(new Move(sq, two));
        }

        // captures
        int[] caps = side == 1 ? new int[] { sq + 7, sq + 9 } : new int[] { sq - 7, sq - 9 };
        for (int i = 0; i < 2; i++)
        {
            int t = caps[i];
            if (!InBoard(t)) continue;
            int tf = File(t);
            if (Math.Abs(tf - f) != 1) continue; // wrapped around
            if (board[t] != 0 && Math.Sign(board[t]) == -side)
            {
                if (Rank(t) == promoteRank) AddPawnPromotionMoves(sq, t, side, moves, captured: board[t]);
                else { var m = new Move(sq, t); m.captured = board[t]; moves.Add(m); }
            }
            // en-passant
            if (t == enPassantSquare)
            {
                var m = new Move(sq, t) { isEnPassant = true };
                moves.Add(m);
            }
        }
    }

    private void AddPawnPromotionMoves(int from, int to, int side, List<Move> moves, int captured = 0)
    {
        int[] promos = new int[] { 5, 4, 3, 2 }; // Q, R, B, N
        foreach (int p in promos)
        {
            var m = new Move(from, to) { promotion = p, captured = captured };
            moves.Add(m);
        }
    }

    private static readonly int[] knightOffsets = new int[] { 17, 15, 10, 6, -17, -15, -10, -6 };
    private void GenerateKnightMoves(int sq, int side, List<Move> moves)
    {
        int rf = File(sq);
        int rr = Rank(sq);
        foreach (int off in knightOffsets)
        {
            int t = sq + off;
            if (!InBoard(t)) continue;
            int tf = File(t), tr = Rank(t);
            // quick wrap checks: difference in file must be 1 or 2 etc. We can rely on file difference check.
            int df = Math.Abs(tf - rf), dr = Math.Abs(tr - rr);
            if (!((df == 1 && dr == 2) || (df == 2 && dr == 1))) continue;
            if (board[t] == 0 || Math.Sign(board[t]) != side)
            {
                var m = new Move(sq, t) { captured = board[t] };
                moves.Add(m);
            }
        }
    }

    private void GenerateSlidingMoves(int sq, int side, List<Move> moves, int[] directions)
    {
        foreach (int dir in directions)
        {
            int t = sq + dir;
            while (InBoard(t) && IsSameRay(sq, t, dir))
            {
                if (board[t] == 0) moves.Add(new Move(sq, t));
                else
                {
                    if (Math.Sign(board[t]) != side) moves.Add(new Move(sq, t) { captured = board[t] });
                    break;
                }
                t += dir;
            }
        }
    }

    // ensure we don't wrap across file boundaries when sliding; dir is one of allowed offsets
    private bool IsSameRay(int from, int to, int dir)
    {
        int ff = File(from), ft = File(to);
        if (dir == 1 || dir == -1) return Rank(from) == Rank(to);
        if (dir == 9 || dir == -9) return Math.Abs(ff - ft) == Math.Abs(Rank(from) - Rank(to));
        if (dir == 7 || dir == -7) return Math.Abs(ff - ft) == Math.Abs(Rank(from) - Rank(to));
        return true; // for vertical moves +8/-8
    }

    private void GenerateKingMoves(int sq, int side, List<Move> moves)
    {
        for (int df = -1; df <= 1; df++)
            for (int dr = -1; dr <= 1; dr++)
            {
                if (df == 0 && dr == 0) continue;
                int t = sq + dr * 8 + df;
                if (!InBoard(t)) continue;
                if (Math.Abs(File(t) - File(sq)) > 1) continue; // wrap guard
                if (board[t] == 0 || Math.Sign(board[t]) != side) moves.Add(new Move(sq, t) { captured = board[t] });
            }

        // Castling
        if (side == 1 && Rank(sq) == 0 && Math.Abs(board[sq]) == 6)
        {
            // white e1=4
            if (whiteCanCastleKing && board[5] == 0 && board[6] == 0)
            {
                // ensure squares not under attack and king not in check
                if (!IsKingInCheck(1) && !IsSquareAttacked(5, -1) && !IsSquareAttacked(6, -1))
                {
                    var m = new Move(sq, 6) { isCastle = true };
                    moves.Add(m);
                }
            }
            if (whiteCanCastleQueen && board[1] == 0 && board[2] == 0 && board[3] == 0)
            {
                if (!IsKingInCheck(1) && !IsSquareAttacked(2, -1) && !IsSquareAttacked(3, -1))
                {
                    var m = new Move(sq, 2) { isCastle = true };
                    moves.Add(m);
                }
            }
        }
        else if (side == -1 && Rank(sq) == 7 && Math.Abs(board[sq]) == 6)
        {
            // black e8=60
            if (blackCanCastleKing && board[61] == 0 && board[62] == 0)
            {
                if (!IsKingInCheck(-1) && !IsSquareAttacked(61, 1) && !IsSquareAttacked(62, 1))
                {
                    var m = new Move(sq, 62) { isCastle = true };
                    moves.Add(m);
                }
            }
            if (blackCanCastleQueen && board[57] == 0 && board[58] == 0 && board[59] == 0)
            {
                if (!IsKingInCheck(-1) && !IsSquareAttacked(58, 1) && !IsSquareAttacked(59, 1))
                {
                    var m = new Move(sq, 58) { isCastle = true };
                    moves.Add(m);
                }
            }
        }
    }

    // Check if the side's king is in check.
    // side = 1 for white, -1 for black.
    public bool IsKingInCheck(bool sideIsWhite) => IsKingInCheck(sideIsWhite ? 1 : -1);
    public bool IsKingInCheck(int side)
    {
        int kingPiece = 6 * side;
        int kingSq = -1;
        for (int i = 0; i < 64; i++) if (board[i] == kingPiece) { kingSq = i; break; }
        if (kingSq == -1) return false; // should not happen in normal play
        return IsSquareAttacked(kingSq, -side);
    }

    // Is square 'sq' attacked by color 'attackerSide' (1 white, -1 black)
    public bool IsSquareAttacked(int sq, int attackerSide)
    {
        // pawns
        if (attackerSide == 1)
        {
            int[] ps = new int[] { sq - 7, sq - 9 };
            foreach (var p in ps) if (InBoard(p) && File(Math.Abs(p)) != File(sq)) { } // not needed; handled below
            // white pawns attack from below (square - 7, -9)
            int a1 = sq - 7, a2 = sq - 9;
            if (InBoard(a1) && Math.Sign(board[a1]) == 1 && Math.Abs(board[a1]) == 1 && Math.Abs(File(a1) - File(sq)) == 1) return true;
            if (InBoard(a2) && Math.Sign(board[a2]) == 1 && Math.Abs(board[a2]) == 1 && Math.Abs(File(a2) - File(sq)) == 1) return true;
        }
        else
        {
            // black pawns attack from above (square +7, +9)
            int a1 = sq + 7, a2 = sq + 9;
            if (InBoard(a1) && Math.Sign(board[a1]) == -1 && Math.Abs(board[a1]) == 1 && Math.Abs(File(a1) - File(sq)) == 1) return true;
            if (InBoard(a2) && Math.Sign(board[a2]) == -1 && Math.Abs(board[a2]) == 1 && Math.Abs(File(a2) - File(sq)) == 1) return true;
        }

        // knights
        foreach (int off in knightOffsets)
        {
            int t = sq + off;
            if (!InBoard(t)) continue;
            int df = Math.Abs(File(t) - File(sq)), dr = Math.Abs(Rank(t) - Rank(sq));
            if (!((df == 1 && dr == 2) || (df == 2 && dr == 1))) continue;
            if (board[t] != 0 && Math.Sign(board[t]) == attackerSide && Math.Abs(board[t]) == 2) return true;
        }

        // sliding: bishops/queens (diagonals)
        int[] diagDirs = new int[] { 9, 7, -9, -7 };
        foreach (int dir in diagDirs)
        {
            int t = sq + dir;
            while (InBoard(t) && IsSameRay(sq, t, dir))
            {
                if (board[t] != 0)
                {
                    if (Math.Sign(board[t]) == attackerSide)
                    {
                        int ap = Math.Abs(board[t]);
                        if (ap == 3 || ap == 5) return true;
                    }
                    break;
                }
                t += dir;
            }
        }

        // sliding: rooks/queens (orthogonal)
        int[] orthoDirs = new int[] { 8, -8, 1, -1 };
        foreach (int dir in orthoDirs)
        {
            int t = sq + dir;
            while (InBoard(t) && IsSameRay(sq, t, dir))
            {
                if (board[t] != 0)
                {
                    if (Math.Sign(board[t]) == attackerSide)
                    {
                        int ap = Math.Abs(board[t]);
                        if (ap == 4 || ap == 5) return true;
                    }
                    break;
                }
                t += dir;
            }
        }

        // king (adjacent)
        for (int df = -1; df <= 1; df++)
            for (int dr = -1; dr <= 1; dr++)
            {
                if (df == 0 && dr == 0) continue;
                int t = sq + dr * 8 + df;
                if (!InBoard(t)) continue;
                if (Math.Abs(File(t) - File(sq)) > 1) continue;
                if (board[t] != 0 && Math.Sign(board[t]) == attackerSide && Math.Abs(board[t]) == 6) return true;
            }

        return false;
    }

    /// <summary>
    /// Returns true if making the move `m` would leave the moving side's king in check.
    /// </summary>
    public bool WouldMoveCauseCheck(Move m)
    {
        // Clone current state so we don't mutate it
        ChessState clone = this.Clone();

        // Remember whose turn it is
        int side = clone.whiteToMove ? 1 : -1;

        // Apply the move on the clone
        clone.ApplyMove(m);

        // After applying the move, the side who just moved flipped, so check the king of the player who moved
        return clone.IsKingInCheck(-side);
    }


    // Very simple terminal check: checkmate, stalemate. Does not implement 50-move or threefold repetition detection.
    public bool IsTerminal(out float result)
    {
        var moves = GenerateLegalMoves();
        if (moves.Count == 0)
        {
            if (IsKingInCheck(whiteToMove)) // side to move is in check => checkmate
            {
                result = whiteToMove ? -1f : 1f; // if white to move and is checkmated, black wins => result from white perspective: -1
            }
            else
            {
                result = 0f; // stalemate draw
            }
            return true;
        }
        // crude 50-move draw
        if (halfmoveClock >= 100) { result = 0f; return true; }
        result = 0f; return false;
    }

    // Simple utility to print board (for debugging)
    public override string ToString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int r = 7; r >= 0; r--)
        {
            for (int f = 0; f < 8; f++)
            {
                int p = board[r * 8 + f];
                sb.Append(PieceChar(p)).Append(' ');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
    private char PieceChar(int p)
    {
        switch (p)
        {
            case 1: return 'P';
            case 2: return 'N';
            case 3: return 'B';
            case 4: return 'R';
            case 5: return 'Q';
            case 6: return 'K';
            case -1: return 'p';
            case -2: return 'n';
            case -3: return 'b';
            case -4: return 'r';
            case -5: return 'q';
            case -6: return 'k';
            default: return '.';
        }
    }
}
