using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum Page { Game, Transition, GameOver, Menu, Settings, Pause }

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    // Pages
    public GameObject gamePage;          // GamePlayCanvas
    public GameObject transitionPage;    // RoundFinish
    public GameObject gameOverPage;      // EndGameCanvas
    public GameObject menuPage;          // MenuCanvas
    public GameObject settingsPage;      // SettingsCanvas
    public GameObject pausePage;         // PauseCanvas

    // HUD
    public TMP_Text timerText;           // TimeTxt
    public TMP_Text scoreText;           // ScoreTxt
    public Image[] rageFires;            // Fire images (3)

    // Settings
    public Slider volumeSlider;          // Volume slider on the settings page

    // Game over
    public TMP_Text winnerText;         // WinnerTxt
    public TMP_Text player1Text;         // Player1
    public TMP_Text player2Text;         // Player2
    public PlayerData playerData;        // same PlayerData asset as DataManager (total scores)

    // Day-night transition
    public SpriteRenderer backgroundImage;   // Background (scene object)
    public Sprite dayBackground;
    public Sprite nightBackground;
    public Animator transitionAnimator;      // Animator on RoundFinish > Canvas
    public float transitionDuration = 2f;    // length of the sun/moon clip
    public float backgroundSwapTime = 1f;    // when the sprite changes during the clip

    public event Action OnRoundStartPressed;

    Page currentPage;
    Page pageBeforeSettings;
    bool continuePressed;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (backgroundImage != null) backgroundImage.sprite = dayBackground;
        PageTransection(Page.Menu);
        if (volumeSlider != null) volumeSlider.value = AudioListener.volume;   // keeps the slider in sync after a scene reload
        UpdateRage(1);
        UpdateTimer(0f);
        UpdateScore(0);
    }

    public void PageTransection(Page page)
    {
        currentPage = page;
        SetPage(menuPage, page == Page.Menu);
        SetPage(settingsPage, page == Page.Settings);
        SetPage(pausePage, page == Page.Pause);
        // Game page stays visible behind the pause page (frozen game)
        SetPage(gamePage, page == Page.Game || page == Page.Pause);
        SetPage(transitionPage, page == Page.Transition);
        SetPage(gameOverPage, page == Page.GameOver);
    }

    void SetPage(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    // Shows the total score (sum of survival times of all rounds) of both players
    public void ShowGameOver()
    {
        float p1Total = playerData.Player1Score;
        float p2Total = playerData.Player2Score;

        if (p1Total > p2Total) winnerText.text = "Player 1 Wins!";
        else if (p2Total > p1Total) winnerText.text = "Player 2 Wins!";
        else winnerText.text = "Draw!";

        player1Text.text = "Player 1: " + p1Total.ToString("F1") + " s";
        player2Text.text = "Player 2: " + p2Total.ToString("F1") + " s";
        PageTransection(Page.GameOver);
    }

    // Main Menu button on the pause page and Play Again button on the game over page.
    // Reloading the scene resets everything; Start() then opens the menu page.
    public void OpenMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Play button on the menu page
    public void StartGame()
    {
        PageTransection(Page.Game);
        OnRoundStartPressed?.Invoke();
    }

    // Settings button on the menu page and on the pause page
    public void OpenSettings()
    {
        pageBeforeSettings = currentPage;
        PageTransection(Page.Settings);
    }

    // Volume slider on the settings page (0 - 1)
    public void SetVolume(float value)
    {
        AudioListener.volume = value;
    }

    // Back button on the settings page: returns to the page that opened it (menu or pause)
    public void CloseSettings()
    {
        PageTransection(pageBeforeSettings);
    }

    // Menu button on the game page: the game stops
    public void OpenPause()
    {
        Time.timeScale = 0f;
        PageTransection(Page.Pause);
    }

    // Continue button on the pause page
    public void ResumeGame()
    {
        Time.timeScale = 1f;
        PageTransection(Page.Game);
    }

    // Quit button on the pause page
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Continue button on the transition page: starts the next round
    public void ContinueRound()
    {
        continuePressed = true;
    }

    public void PlayRoundTransition(bool toNight)
    {
        StartCoroutine(TransitionRoutine(toNight));
    }

    IEnumerator TransitionRoutine(bool toNight)
    {
        PageTransection(Page.Transition);
        yield return null;

        if (transitionAnimator != null)
            transitionAnimator.SetTrigger(toNight ? "ToNight" : "ToDay");

        // Background changes in the middle of the sun/moon animation
        yield return new WaitForSecondsRealtime(backgroundSwapTime);
        if (backgroundImage != null)
            backgroundImage.sprite = toNight ? nightBackground : dayBackground;

        // Rest of the animation
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, transitionDuration - backgroundSwapTime));

        // Wait for Space or the continue button (presses during the animation are ignored)
        continuePressed = false;
        while (!continuePressed && !Input.GetKeyDown(KeyCode.Space))
            yield return null;

        PageTransection(Page.Game);
        OnRoundStartPressed?.Invoke();
    }

    public void UpdateRage(int rage)
    {
        // Collected fires are white (normal sprite colors), the others are transparent
        for (int i = 0; i < rageFires.Length; i++)
            rageFires[i].color = i < rage ? Color.white : Color.clear;
    }

    public void UpdateTimer(float seconds)
    {
        if (timerText != null) timerText.text = "Time: " + seconds.ToString("F1");
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
    }
}