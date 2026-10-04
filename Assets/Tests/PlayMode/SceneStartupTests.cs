using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class SceneStartupTests
{
    const string MenuScene = "Assets/Scenes/MenuScene.unity";
    const string GameScene = "Assets/Scenes/GameScene.unity";

    // The game uses Assembly-CSharp, which cannot be referenced by a test asmdef.
    static MonoBehaviour Find(string className) =>
        UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(component => component.GetType().Name == className);

    static object Call(MonoBehaviour component, string method) =>
        component.GetType().GetMethod(method).Invoke(component, null);

    static GameObject Page(MonoBehaviour ui, string field) =>
        (GameObject)ui.GetType().GetField(field).GetValue(ui);

    static void Press(string buttonName) =>
        UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(button => button.name == buttonName).onClick.Invoke();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        Scene empty = SceneManager.CreateScene("SceneStartupCleanup");
        SceneManager.SetActiveScene(empty);
        foreach (string path in new[] { MenuScene, GameScene })
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }
    }

    static IEnumerator PressPlayAndAssertRunning()
    {
        Button play = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(button => button.name == "PlayButtonMain");
        play.onClick.Invoke();
        // A double click while the scene loads must not load it twice.
        play.onClick.Invoke();
        float deadline = Time.realtimeSinceStartup + 15f;
        MonoBehaviour gameplay = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            gameplay = Find("GamePlayController");
            if (gameplay != null && (bool)Call(gameplay, "IsRoundActive")) break;
            yield return null;
        }
        Assert.That(gameplay, Is.Not.Null, "Play must load the gameplay scene.");
        Assert.That((bool)Call(gameplay, "IsRoundActive"), Is.True);
        Assert.That(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .Count(camera => camera.isActiveAndEnabled), Is.EqualTo(1));
        Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)
            .Count(system => system.isActiveAndEnabled), Is.EqualTo(1));
        Assert.That(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
            .Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));
        float before = (float)Call(gameplay, "GetCurrentSurvivalTime");
        yield return new WaitForSeconds(0.2f);
        Assert.That((float)Call(gameplay, "GetCurrentSurvivalTime"), Is.GreaterThan(before));
    }

    [UnityTest]
    public IEnumerator MenuOnly_PlayLoadsGameplay()
    {
        yield return SceneManager.LoadSceneAsync(MenuScene, LoadSceneMode.Single);
        yield return null;
        yield return PressPlayAndAssertRunning();
    }

    [UnityTest]
    public IEnumerator GameOnly_LoadsMenuAndCanPlay()
    {
        yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
        float deadline = Time.realtimeSinceStartup + 15f;
        while (Find("UIManager") == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Find("UIManager"), Is.Not.Null, "Direct gameplay startup must also load the UI.");
        yield return null;
        yield return PressPlayAndAssertRunning();
    }

    [UnityTest]
    public IEnumerator Settings_WorkFromMenuAndPause()
    {
        float previousVolume = AudioListener.volume;
        try
        {
            yield return SceneManager.LoadSceneAsync(MenuScene, LoadSceneMode.Single);
            yield return null;
            MonoBehaviour ui = Find("UIManager");
            Press("SettingsButtonMain");
            yield return null;
            Assert.That(Page(ui, "settingsPage").activeInHierarchy, Is.True);
            Slider volume = (Slider)ui.GetType().GetField("volumeSlider").GetValue(ui);
            Assert.That(volume, Is.Not.Null);
            volume.value = 0.35f;
            Assert.That(AudioListener.volume, Is.EqualTo(0.35f).Within(0.001f));
            Press("BackButtonSettings");
            Assert.That(Page(ui, "menuPage").activeInHierarchy, Is.True);
            Assert.That(Page(ui, "settingsPage").activeSelf, Is.False);

            yield return PressPlayAndAssertRunning();
            Press("Pause");
            Press("SettingsButtonPause");
            Assert.That(Page(ui, "settingsPage").activeInHierarchy, Is.True);
            Press("BackButtonSettings");
            Assert.That(Page(ui, "pausePage").activeInHierarchy, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Press("MainMenuButton");
            yield return null;
            yield return null;
            Assert.That(Page(Find("UIManager"), "menuPage").activeInHierarchy, Is.True);
        }
        finally
        {
            AudioListener.volume = previousVolume;
        }
    }

    [UnityTest]
    public IEnumerator BothScenes_ReturnToMenuAndReplay()
    {
        yield return SceneManager.LoadSceneAsync(MenuScene, LoadSceneMode.Single);
        yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByPath(GameScene));
        yield return null;
        yield return PressPlayAndAssertRunning();
        Call(Find("UIManager"), "OpenPause");
        Assert.That(Time.timeScale, Is.Zero);
        Call(Find("UIManager"), "OpenMenu");
        yield return null;
        yield return null;
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(SceneManager.GetSceneByPath(MenuScene).isLoaded, Is.True);
        Assert.That(SceneManager.GetSceneByPath(GameScene).isLoaded, Is.False);
        Assert.That(Find("UIManager").GetType().GetField("menuPage")
            .GetValue(Find("UIManager")), Is.Not.Null);
        yield return PressPlayAndAssertRunning();
    }
}
