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

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SelectPiece(GameObject piece)
    {
        if (piece.tag == "White" && WhiteTurn == true || piece.tag == "Black" && WhiteTurn == false)
        {
            DeselectPiece();
            SelectedPiece = piece;

            // Highlight
            SelectedPiece.GetComponent<SpriteRenderer>().color = Color.yellow;

            // Highlight possible moves
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
                    if (SelectedPiece.GetComponent<PieceController>().ValidateMovement(SelectedPiece.transform.position, new Vector3(x, y, 0f), out encounteredEnemy))
                    {
                        GameObject possibleMoveBox = GetBoxAtPosition(x, y);
                        possibleMoveBox.GetComponent<SpriteRenderer>().color = Color.yellow;
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
        Debug.Log("Trying to find cell at coord: " + coord);
        return Board.transform.Find(coord).GetChild(0).gameObject;
    }

    public void UnhighlightPossibleMoves()
    {
        foreach (GameObject box in PossibleMoves)
        {
            box.GetComponent<SpriteRenderer>().color = Color.white;
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
    }

    bool HasValidMoves(GameObject piece)
    {
        PieceController pieceController = piece.GetComponent<PieceController>();
        GameObject encounteredEnemy;

        foreach (Transform square in Board.transform)
        {
            if (pieceController.ValidateMovement(piece.transform.position, new Vector3(square.position.x, square.position.y, piece.transform.position.z), out encounteredEnemy))
            {
                Debug.Log(piece + " on " + square);
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
