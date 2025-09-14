using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public GameObject Board;
    public GameObject WhitePieces;
    public GameObject BlackPieces;
    public GameObject SelectedPiece;
    public List<GameObject> PossibleMoves = new List<GameObject>();
    public bool WhiteTurn = true;
    public bool OpponentAI = false;
    public OpponentAI ai;
    public GameObject StopAIButton;


    // Use this for initialization
    void Start()
    {
        if (OpponentAI)
        {
            ai = FindFirstObjectByType<OpponentAI>();
            ai.OnMoveChosen += HandleAIMove;
        } 
    }

    // Update is called once per frame
    void Update()
    {

    }

    void HandleAIMove(ChessState.Move move)
    {
        // runs on main thread (because OpponentAI invokes it in Update)
        ApplyMoveToBoard(move);
    }

    void ApplyMoveToBoard(ChessState.Move move)
    {
        // your existing method that updates GameObjects, UI, etc.
        Vector2 from = SquareToXY(move.from);
        Vector2 to = SquareToXY(move.to);
        SelectedPiece = GetBlackPieceAtPosition(from.x, from.y);
        SelectedPiece.GetComponent<PieceController>().MovePiece(new Vector3(to.x, to.y, 0));
    }

    // Convert ChatGPT's coord system to coords used by original GitHub project
    Vector2 SquareToXY(int square)
    {
        int file = square & 7;       // 0..7 for a..h
        int rank = square >> 3;      // 0..7 for 1..8
        float x = -3.5f + file;
        float y = -3.5f + rank;
        return new Vector2(x, y);
    }

    public void SelectPiece(GameObject piece)
    {
        if (piece.tag == "White" && WhiteTurn == true || piece.tag == "Black" && WhiteTurn == false && OpponentAI == false)
        {
            DeselectPiece();
            SelectedPiece = piece;

            // Highlight
            SelectedPiece.GetComponent<SpriteRenderer>().color = Color.yellow;

            HighlightPossibleMoves();

            // Put above other pieces
            Vector3 newPosition = SelectedPiece.transform.position;
            newPosition.z = -1;
            SelectedPiece.transform.SetPositionAndRotation(newPosition, SelectedPiece.transform.rotation);
        }
    }

    public void HighlightPossibleMoves()
    {
        if (SelectedPiece != null)
        {
            // Search all squares and highlight possible (+ store for later so can remove highlight)
            for (float x = -3.5f; x <= 3.5; x++)
            {
                for (float y = -3.5f; y <= 3.5; y++)
                {
                    GameObject encounteredEnemy = null;
                    if (SelectedPiece.GetComponent<PieceController>().ValidateMovement(SelectedPiece.transform.localPosition, new Vector3(x, y, 0f), out encounteredEnemy))
                    {
                        GameObject possibleMoveBox = GetBoxAtPosition(x, y);
                        possibleMoveBox.GetComponent<SpriteRenderer>().color = Color.softYellow;
                        PossibleMoves.Add(possibleMoveBox);
                    }
                }
            }
        }

    }

    public GameObject GetBoxAtPosition(float x, float y)
    {
        char col = (char) (65 + ((int) (x+3.5)));
        int row = (int) (y + 3.5 + 1);
        string coord = col.ToString() + row.ToString();
        return Board.transform.Find(coord).GetChild(0).gameObject;
    }

    // Used for making AI's move
    public GameObject GetBlackPieceAtPosition(float x, float y)
    {
        foreach (Transform child in BlackPieces.transform)
        {
            Vector3 pos = child.position;
            if (pos.x == x && pos.y == y)
            {
                return child.gameObject;
            }
        }

        return null; // no piece found at given coordinates
    }



    public void UnhighlightPossibleMoves()
    {
        foreach (GameObject box in PossibleMoves)
        {
            if (box.transform.name.Contains("White")) box.GetComponent<SpriteRenderer>().color = new Color32(236, 230, 179, 255);
            else box.GetComponent<SpriteRenderer>().color = Color.white;
        }

        PossibleMoves.Clear();
    }

    public void DeselectPiece()
    {
        if (SelectedPiece != null)
        {
            // Remove highlight
            SelectedPiece.GetComponent<SpriteRenderer>().color = Color.white;

            // Remove possible moves highlight
            UnhighlightPossibleMoves();

            // Put back on the same level as other pieces
            Vector3 newPosition = SelectedPiece.transform.position;
            newPosition.z = 0;
            SelectedPiece.transform.SetPositionAndRotation(newPosition, SelectedPiece.transform.rotation);

            SelectedPiece = null;
        }
    }

    public void EndTurn()
    {
        bool kingIsInCheck = false;
        bool hasValidMoves = false;

        WhiteTurn = !WhiteTurn;

        if (WhiteTurn)
        {
            foreach (Transform piece in WhitePieces.transform)
            {
                if (hasValidMoves == false && HasValidMoves(piece.gameObject))
                {
                    hasValidMoves = true;
                }

                if (piece.name.Contains("Pawn"))
                {
                    piece.GetComponent<PieceController>().DoubleStep = false;
                }
                else if (piece.name.Contains("King"))
                {
                    kingIsInCheck = piece.GetComponent<PieceController>().IsInCheck(piece.position);
                }
            }
        }
        else
        {
            foreach (Transform piece in BlackPieces.transform)
            {
                if (hasValidMoves == false && HasValidMoves(piece.gameObject))
                {
                    hasValidMoves = true;
                }

                if (piece.name.Contains("Pawn"))
                {
                    piece.GetComponent<PieceController>().DoubleStep = false;
                }
                else if (piece.name.Contains("King"))
                {
                    kingIsInCheck = piece.GetComponent<PieceController>().IsInCheck(piece.position);
                }
            }
        }

        if (hasValidMoves == false)
        {
            if (kingIsInCheck == false)
            {
                Stalemate();
            }
            else
            {
                Checkmate();
            }
        }

        // AI plays black's turn if AI opponent enabled
        if (OpponentAI)
        {
            StopAIButton.GetComponent<StopAIButton>().setIsAIRunning(!WhiteTurn);
        }

        if (OpponentAI && !WhiteTurn)
        {
            GetComponent<OpponentAI>().MakeMove();
        }

    }

    bool HasValidMoves(GameObject piece)
    {
        PieceController pieceController = piece.GetComponent<PieceController>();
        GameObject encounteredEnemy;

        foreach (Transform square in Board.transform)
        {
            if (pieceController.ValidateMovement(piece.transform.position, new Vector3(square.position.x, square.position.y, piece.transform.position.z), out encounteredEnemy))
            {
                return true;
            }
        }
        return false;
    }

    void Stalemate()
    {
        Debug.Log("Stalemate!");
    }

    void Checkmate()
    {
        Debug.Log("Checkmate!");
    }
}
