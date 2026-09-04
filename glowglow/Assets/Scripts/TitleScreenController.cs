using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public sealed class TitleScreenController : MonoBehaviour
{
    [SerializeField] private CanvasGroup mainMenu;
    [SerializeField] private CanvasGroup settingsPanel;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button firstButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Slider masterVolume;

    private Coroutine transition;

    private void Start()
    {
        Show(mainMenu, true);
        Show(settingsPanel, false);
        if (firstButton != null) EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
        if (masterVolume != null)
        {
            masterVolume.value = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
            AudioListener.volume = masterVolume.value;
        }
    }

    public void PlayMultiplayer()
    {
        SetStatus("LOCAL BATTLE  ·  LOADING");
        SceneManager.LoadScene("Arena");
    }

    public void PlaySolo()
    {
        SetStatus("SOLO MODE  ·  추후 업데이트");
        PulseSelection();
    }

    public void OpenSettings() => BeginTransition(mainMenu, settingsPanel, settingsBackButton);
    public void CloseSettings() => BeginTransition(settingsPanel, mainMenu, firstButton);

    public void SetMasterVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetStatus(string value)
    {
        if (statusText != null) statusText.text = value;
    }

    private void PulseSelection()
    {
        if (statusText != null) StartCoroutine(Pulse(statusText.rectTransform));
    }

    private IEnumerator Pulse(RectTransform target)
    {
        float elapsed = 0f;
        while (elapsed < 0.22f)
        {
            elapsed += Time.unscaledDeltaTime;
            float bump = Mathf.Sin(elapsed / 0.22f * Mathf.PI) * 0.05f;
            target.localScale = Vector3.one * (1f + bump);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    private void BeginTransition(CanvasGroup from, CanvasGroup to, Selectable selection)
    {
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(Transition(from, to, selection));
    }

    private IEnumerator Transition(CanvasGroup from, CanvasGroup to, Selectable selection)
    {
        Show(to, true);
        to.alpha = 0f;
        const float duration = 0.18f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            from.alpha = 1f - t;
            to.alpha = t;
            yield return null;
        }
        Show(from, false);
        to.alpha = 1f;
        if (selection != null) EventSystem.current.SetSelectedGameObject(selection.gameObject);
        transition = null;
    }

    private static void Show(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}
