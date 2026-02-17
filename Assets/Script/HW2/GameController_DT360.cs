using System.Collections.Generic;
using NUnit.Framework.Internal;
using UnityEngine;
using UnityEngine.UI;

public class GameController_DT360 : MonoBehaviour
{

    public int whosTurn;
    public int turnCount;
    public int[] markedSpaces;
    public Button[] tictactoeSpaces;

    public GameObject[] turnIcons;
    public Sprite[] playIcons;
    public Text WinnerText;
    public GameObject[] winningLines;
    public GameObject WinnerPannel;
    public int xPlayersScore;
    public int oPlayersScore;
    public Text xPlaterScoreText;
    public Text oPlayerScoreText;
    public Button xPlayerButton;
    public Button oPlayerButton;

    // NEW:
    public enum Strategy
    {
        None,
        FirstAvailable,
        Random,
        MiniMax
    }
    public Strategy ai_strategy;
    const int EMPTY_SPACE = -100;

    // TODO: 
    // Change ai_strategy to Strategy.MiniMax on the line 92 in Start() 
    /***************************************/
    /*****  IMPLEMENT YOUR CODE HERE  ******/
    /***************************************/
    int pickMiniMax(int turn, bool isWinner)
    {
        if (isWinner) {
            int bestScore = int.MinValue;
            int bestSpot = -1;

            for (int i = 0; i < 9; i++)
            {
                if (markedSpaces[i] == EMPTY_SPACE)
                {
                    markedSpaces[i] = 2; // try O here
                    int score = pickMiniMax(turn, Winnercheck()); // next turn is X (minimizer)
                    markedSpaces[i] = EMPTY_SPACE;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSpot = i;
                    }
                }
            }
            return bestSpot;
        }

        if (turn % 2 == 0)
        {
            int best = int.MinValue;
            for (int i = 0; i < 9; i++)
            {
                if (markedSpaces[i] == EMPTY_SPACE)
                {
                    markedSpaces[i] = 2;
                    best = Mathf.Max(best, pickMiniMax(turn + 1, Winnercheck()));
                    markedSpaces[i] = EMPTY_SPACE;
                }
            }
            return best;
        }

        else
        {
            int best = int.MaxValue;
            for (int i = 0; i < 9; i++)
            {
                if (markedSpaces[i] == EMPTY_SPACE)
                {
                    markedSpaces[i] = 1;
                    best = Mathf.Min(best, pickMiniMax(turn + 1, Winnercheck()));
                    markedSpaces[i] = EMPTY_SPACE;
                }
            }
        }
            return 0;
    }
    /***************************************/
    /***** END OF YOUR IMPLEMENTATION ******/
    /***************************************/
     // TODO: END


    // NEW:
    // Simple strategy to pick the first available spot.
    int pickFirstAvailableSpot()
    {
        for(int i=0; i<9; i++)
        {
            if (markedSpaces[i] == EMPTY_SPACE)
            {
                return i;
            }
        }

        return -1;
    }

    // NEW:
    // Simple strategy to randomly pick the available spot.
    int pickRandomSpot()
    {
        int spot = -1;
        var emptySpots = new List<int>();

        // create a list of empty spots
        for (int i=0; i<9; i++)
        {
            if (markedSpaces[i] == EMPTY_SPACE)
            {
                emptySpots.Add(i);
            }
        }

        // randomly pick an empty spot
        int index = UnityEngine.Random.Range(0, emptySpots.Count);
        spot = emptySpots[index];
        return spot;
    }

    // Start is called before the first frame update
    void Start()
    {
        ai_strategy = Strategy.MiniMax;
        GameSetup();
    }

    void GameSetup() 
    {
        turnCount = 0;
        SetPlayerTurn(0);

        for (int i = 0; i < tictactoeSpaces.Length; i++) 
        {
            markedSpaces[i] = EMPTY_SPACE;

            tictactoeSpaces[i].interactable = true;
            tictactoeSpaces[i].GetComponent<Image>().sprite = null;
        }
    }

    public void TicTacToeButton(int whichnumber) 
    {
        xPlayerButton.interactable = false;
        oPlayerButton.interactable = false;
        tictactoeSpaces[whichnumber].image.sprite = playIcons[whosTurn];
        tictactoeSpaces[whichnumber].interactable = false;

        markedSpaces[whichnumber] = whosTurn + 1;   // assign X = 1 & O = 2 for calculation of the winner in Winnercheck()
        turnCount++;

        if (turnCount >= 4) {
            bool iswinner =  Winnercheck();

            if (turnCount == 9 && iswinner==false) {
                winnerDisplay(-1, whosTurn);  // tie
            }
        }
    
        SetPlayerTurn(whosTurn == 0 ? 1 : 0);
   
        // NEW
        // Player 1 picks the spot according to its strategy
        if (turnCount < 9 && whosTurn == 1)
        {
            int spot = -1;
            switch (ai_strategy)
            {
                case Strategy.None:             // manual play
                    break;

                case Strategy.FirstAvailable:   // pick first available spot
                    spot = pickFirstAvailableSpot();
                    TicTacToeButton(spot);
                    break;

                case Strategy.Random:           // pick random spot
                    spot = pickRandomSpot();
                    TicTacToeButton(spot);
                    break;

                case Strategy.MiniMax:          // compute from MiniMax algorithm
                    spot = pickMiniMax(turnCount, Winnercheck());
                    TicTacToeButton(spot);
                    break;
            }
        }
    }

    bool Winnercheck() 
    {
        int s1 = markedSpaces[0] + markedSpaces[1] + markedSpaces[2];
        int s2 = markedSpaces[3] + markedSpaces[4] + markedSpaces[5];
        int s3 = markedSpaces[6] + markedSpaces[7] + markedSpaces[8];
        int s4 = markedSpaces[0] + markedSpaces[3] + markedSpaces[6];
        int s5 = markedSpaces[1] + markedSpaces[4] + markedSpaces[7];
        int s6 = markedSpaces[2] + markedSpaces[5] + markedSpaces[8];
        int s7 = markedSpaces[0] + markedSpaces[4] + markedSpaces[8];
        int s8 = markedSpaces[2] + markedSpaces[4] + markedSpaces[6];
        var Solutions = new int[] { s1, s2, s3, s4, s5, s6, s7, s8 };
        for (int i = 0; i < Solutions.Length; i++)
        {
            if (Solutions[i] == 3 *(whosTurn + 1))
            {
                winnerDisplay(i, whosTurn);
                return true;
            }
            
        }
        return false;
    }


    void winnerDisplay(int index, int who) 
    {
        if (index < 0)
        {
            WinnerText.text = "It's a draw!!";
            WinnerText.fontSize = 185;
        }
        else
        {
            if (who == 0)
            {
                WinnerText.text = "Player X Wins!";
                WinnerText.fontSize = 150;
                xPlayersScore++;
                xPlaterScoreText.text = xPlayersScore.ToString();
            }
            else
            {
                WinnerText.text = "Player O Wins!";
                WinnerText.fontSize = 150;
                oPlayersScore++;
                oPlayerScoreText.text = oPlayersScore.ToString();
            }

            winningLines[index].SetActive(true);
        }

        WinnerPannel.SetActive(true);
    }

    public void ReStart() 
    {
        GameSetup();

        xPlayerButton.interactable = true;
        oPlayerButton.interactable = true;
        WinnerPannel.SetActive(false);
        for (int i = 0; i < winningLines.Length; i++)
        {
            winningLines[i].SetActive(false);
        }
    }

    public void SetPlayerTurn(int player)
    {
        whosTurn = player;

        switch (player)
        {
            case 0:
                turnIcons[0].SetActive(true);
                turnIcons[1].SetActive(false);
                break;
            case 1:
                turnIcons[0].SetActive(false);
                turnIcons[1].SetActive(true);
                break;
        }
    }
}
