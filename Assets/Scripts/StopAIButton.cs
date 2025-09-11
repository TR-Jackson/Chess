using UnityEngine;

public class StopAIButton : MonoBehaviour
{

    [HideInInspector]
    public GameController GameController;
    [HideInInspector]
    public bool isAIRunning;
    [HideInInspector]
    public OpponentAI ai;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (GameController == null) GameController = FindFirstObjectByType<GameController>();
        if (ai == null) ai = FindFirstObjectByType<OpponentAI>();

        // Only show if playing with AI
        this.gameObject.SetActive((GameController.OpponentAI));

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void setIsAIRunning(bool b)
    {
        isAIRunning = b;

        if (isAIRunning)
        {
            this.GetComponent<SpriteRenderer>().color = Color.green;
        }
        else
        {
            this.GetComponent<SpriteRenderer>().color = Color.red;
        }
    }

    void OnMouseDown()
    {
        if (isAIRunning)
        {
            ai.CancelSearch();
        }
    }
}
