using UnityEngine;
using UnityEngine.UI;

public class SimpleUI : MonoBehaviour
{
    public Text statusText;
    public Text attemptsText;

    public Button restartButton;
    public Button NextStageButton;

    private MineHuntGameManager gm;

    public void BindGameManager(MineHuntGameManager gameManager)
    {
        gm = gameManager;

        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(() =>
            {
                restartButton.gameObject.SetActive(false);
                NextStageButton?.gameObject.SetActive(false);
                gm.NewGame();
            });
        }

        if (NextStageButton != null)
        {
            NextStageButton.onClick.RemoveAllListeners();
            NextStageButton.onClick.AddListener(() =>
            {
                restartButton?.gameObject.SetActive(false);
                NextStageButton.gameObject.SetActive(false);
                gm.NextStage();
            });
        }
    }

    public void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    public void SetAttempts(int left)
    {
        if (attemptsText != null) attemptsText.text = $"남은횟수: {left}";
    }

    public void ShowRestart(bool show)
    {
        if (restartButton != 
            null)
            restartButton.gameObject.SetActive(show);
    }

    public void ShowNextStage(bool show)
    {
        if (NextStageButton != null)
            NextStageButton.gameObject.SetActive(show);
    }
}