using UnityEngine;

public class BoxController : MonoBehaviour
{
    public GameController GameController;

    public float HighestRankY = 3.5f;
    public float LowestRankY = -3.5f;

    // Use this for initialization
    void Start()
    {
        if (GameController == null) GameController = FindFirstObjectByType<GameController>();

        string algebraicName = "";
        algebraicName += (char)(this.transform.parent.localPosition.x - LowestRankY + 'A');
        algebraicName += this.transform.parent.localPosition.y - LowestRankY + 1;
        this.transform.parent.name = algebraicName;
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnMouseDown()
    {

        if (GameController.SelectedPiece != null && GameController.SelectedPiece.GetComponent<PieceController>().IsMoving() == true)
        {
            // Prevent clicks during movement
            return;
        }

        if (GameController.SelectedPiece != null)
        {
            GameController.SelectedPiece.GetComponent<PieceController>().MovePiece(this.transform.parent.localPosition);
        }
    }
}
