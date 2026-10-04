using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reuse the menu's canvas, font and button art when the scene has no settings page.
public static class SettingsPageBuilder
{
    public static void Create(UIManager ui)
    {
        GameObject page = Object.Instantiate(ui.menuPage, ui.menuPage.transform.parent);
        page.name = "SettingsCanvas";
        page.SetActive(false);
        page.transform.localScale = Vector3.one;
        ui.settingsPage = page;

        TMP_Text templateText = ui.menuPage.GetComponentInChildren<TMP_Text>(true);
        foreach (Button button in page.GetComponentsInChildren<Button>(true))
        {
            if (button.name != "PlayButtonMain")
            {
                Object.Destroy(button.gameObject);
                continue;
            }

            button.name = "BackButtonSettings";
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(ui.CloseSettings);
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
                text.text = "Back";
            Position(button.GetComponent<RectTransform>(), new Vector2(0f, -120f), new Vector2(180f, 50f));
        }

        Label(page.transform, templateText, "Settings", new Vector2(0f, 120f), 28f);
        Label(page.transform, templateText, "Volume", new Vector2(0f, 55f), 20f);

        GameObject sliderObject = new GameObject("VolumeSlider", typeof(RectTransform), typeof(Image), typeof(Slider));
        sliderObject.transform.SetParent(page.transform, false);
        Position(sliderObject.GetComponent<RectTransform>(), Vector2.zero, new Vector2(340f, 24f));
        sliderObject.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f);
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        Image fill = Image(sliderObject.transform, "Fill", new Color(0.36f, 0f, 0.55f));
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.sizeDelta = Vector2.zero;
        slider.fillRect = fill.rectTransform;

        Image handle = Image(sliderObject.transform, "Handle", Color.white);
        handle.rectTransform.sizeDelta = new Vector2(24f, 36f);
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.value = AudioListener.volume;
        slider.onValueChanged.AddListener(ui.SetVolume);
        ui.volumeSlider = slider;
    }

    static void Label(Transform parent, TMP_Text template, string content, Vector2 position, float fontSize)
    {
        GameObject obj = new GameObject(content + "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        Position(obj.GetComponent<RectTransform>(), position, new Vector2(600f, 60f));
        TMP_Text text = obj.GetComponent<TMP_Text>();
        if (template != null) text.font = template.font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = new Color(0.36f, 0f, 0.55f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    static Image Image(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        obj.transform.SetParent(parent, false);
        Image graphic = obj.GetComponent<Image>();
        graphic.color = color;
        return graphic;
    }

    static void Position(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
