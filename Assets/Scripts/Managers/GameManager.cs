using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GamePlayController gamePlayController;

    [Header("Global Running Speed")]
    [SerializeField] private float startSpeed = 3f;
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float timeToMaxSpeed = 120f;

    [SerializeField] private int roundNum;
    public static GameManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        if (gamePlayController == null)
        {
            Debug.LogError("ASSIGN THE GAME_PLAY_CONTROLLER!!!!");
            return;
        }

        gamePlayController.RoundFinished += HandleRoundFinished;

        // TODO: DELETE THIS
        StartGamePlay();
    }

    private void OnDestroy()
    {
        if (gamePlayController != null)
        {
            gamePlayController.RoundFinished -= HandleRoundFinished;
        }
    }

    public float GetCurrentSpeed()
    {
        if (gamePlayController == null || !gamePlayController.IsRoundActive())
        {
            return 0f;
        }

        float survivalTime = gamePlayController.GetCurrentSurvivalTime();
        float progress = 1f;

        if (timeToMaxSpeed > 0f)
        {
            progress = Mathf.Clamp01(survivalTime / timeToMaxSpeed);
        }

        return Mathf.Lerp(startSpeed, maxSpeed, progress);
    }

    public int GetCurrentRoundNum()
    {
        return roundNum;
    }

    public void HandleRoundFinished(float time)
    {
        if (roundNum % 2 == 1)
        {
            DataManager.Instance.IncreasePlayer1Score(time);
        }
        else
        {
            DataManager.Instance.IncreasePlayer2Score(time);
        }

        roundNum++;

        // TODO: Call the UI
    }

    /// <summary>
    /// To start game play first time with fresh scores.
    /// </summary>
    public void StartGamePlay()
    {
        AudioManager.Instance.PlayGameBgMusic();
        roundNum = 1;
        if (DataManager.Instance != null)
        {
            DataManager.Instance.ResetPlayerData();
        }
        gamePlayController.StartRound(roundNum);
    }
}