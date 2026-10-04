using UnityEngine;
using UnityEngine.SceneManagement;

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

        // Playing GameScene directly in the editor must also bring in its UI.
        if (UIManager.Instance == null && !SceneManager.GetSceneByPath(UIManager.MenuScenePath).isLoaded)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name == "Main Camera" || root.name == "Global Light 2D" || root.name == "EventSystem")
                    root.SetActive(false);
            }
            SceneManager.LoadSceneAsync(UIManager.MenuScenePath, LoadSceneMode.Additive);
        }

    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
        if (roundNum == 1)
            UIManager.Instance.PlayRoundTransition(true);   
        else
            UIManager.Instance.ShowGameOver();              
        roundNum++;
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

        /// <summary>
    /// Starts the next round after the transition page.
    /// </summary>
    public void StartNextRound()
    {
        gamePlayController.StartRound(roundNum);
    }

}
