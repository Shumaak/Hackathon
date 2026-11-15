using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class Cutscener : MonoBehaviour
{
    public List<GameObject> StartCutsceneParts;
    public List<GameObject> EndCutsceneParts;
    public GameObject StartBG;
    public GameObject EndBG;

    [SerializeField] private float cutscenePartTime = 2.5f;
    [Tooltip("Seconds for one fade to black (and the same again for the fade back in). 0 = instant cut.")]
    [SerializeField][Min(0)] private float fadeTime = 0.4f;
    [SerializeField][ReadOnly] private float cutscenePartTimer;
    [SerializeField][ReadOnly] private int currentIndex;

    private List<GameObject> playingParts;
    private GameObject playingBG;
    private bool ending;
    private Image fadeImage;
    private Coroutine routine;

    void Start()
    {
        PlayStart();
    }

    public void PlayStart() => Play(StartCutsceneParts, StartBG);

    public void PlayEnd() => Play(EndCutsceneParts, EndBG, true);

    private void Play(List<GameObject> parts, GameObject bg, bool end = false)
    {
        if (routine != null)
            StopCoroutine(routine);

        ending = end;
        Time.timeScale = 0;
        routine = StartCoroutine(Run(parts, bg));
    }

    private IEnumerator Run(List<GameObject> parts, GameObject bg)
    {
        EnsureFadeImage();

        // The start cutscene opens from black; the end one fades the gameplay out first.
        if (ending)
            yield return Fade(1);
        else
            SetFade(1);

        playingParts = parts;
        playingBG = bg;
        currentIndex = 0;

        if (playingBG != null)
            playingBG.SetActive(true);

        ShowCurrentPart();
        yield return Fade(0);

        while (true)
        {
            cutscenePartTimer = cutscenePartTime;
            while (cutscenePartTimer > 0 && !Input.GetMouseButtonDown(0))
            {
                cutscenePartTimer -= Time.unscaledDeltaTime;
                yield return null;
            }

            // Don't let the same click skip the next part too.
            yield return null;

            currentIndex++;
            bool finished = currentIndex >= playingParts.Count;

            yield return Fade(1);

            if (finished && !ending)
                Stop();
            else
                ShowCurrentPart();

            yield return Fade(0);

            if (finished)
                break;
        }

        routine = null;
    }

    private void ShowCurrentPart()
    {
        for (int i = 0; i < playingParts.Count; i++)
            playingParts[i].SetActive(i == currentIndex);
    }

    private void Stop()
    {
        Time.timeScale = 1;

        foreach (GameObject part in playingParts)
            part.SetActive(false);

        if (playingBG != null)
            playingBG.SetActive(false);

        playingParts = null;
        playingBG = null;
    }

    private IEnumerator Fade(float target)
    {
        float start = fadeImage.color.a;

        if (fadeTime > 0)
        {
            for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(start, target, t / fadeTime));
                yield return null;
            }
        }

        SetFade(target);
    }

    private void SetFade(float alpha)
    {
        fadeImage.color = new Color(0, 0, 0, alpha);
        fadeImage.enabled = alpha > 0;
    }

    private void EnsureFadeImage()
    {
        if (fadeImage != null)
            return;

        // Full-screen black overlay drawn above everything else.
        var canvasObject = new GameObject("CutsceneFade", typeof(Canvas));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var imageObject = new GameObject("Black", typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeImage = imageObject.GetComponent<Image>();
        fadeImage.raycastTarget = false;
        SetFade(0);
    }
}
