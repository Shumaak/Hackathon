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

    [Tooltip("Shows the text of parts that are dialogue lines (objects with a CutsceneDialogLine).")]
    [SerializeField] private DialogWindow dialogWindow;

    [SerializeField] private float cutscenePartTime = 2.5f;
    [Tooltip("Seconds a dialogue line stays up before moving on by itself. A click moves on sooner.")]
    [SerializeField] private float dialogPartTime = 4f;
    [Tooltip("Seconds for one fade to black (and the same again for the fade back in). 0 = instant cut.")]
    [SerializeField][Min(0)] private float fadeTime = 0.4f;
    [Tooltip("Seconds to wait after PlayEnd is called before the end cutscene starts. The game keeps running meanwhile.")]
    [SerializeField][Min(0)] private float endDelay = 0.5f;
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

    public void PlayEnd()
    {
        if (ending && routine != null)
            return;

        Play(EndCutsceneParts, EndBG, true);
    }

    private void Play(List<GameObject> parts, GameObject bg, bool end = false)
    {
        if (routine != null)
            StopCoroutine(routine);

        ending = end;
        routine = StartCoroutine(Run(parts, bg));
    }

    private IEnumerator Run(List<GameObject> parts, GameObject bg)
    {
        EnsureFadeImage();

        if (ending && endDelay > 0)
            yield return new WaitForSecondsRealtime(endDelay);

        Time.timeScale = 0;

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
            if (ending && currentIndex >= playingParts.Count - 1)
                break;

            CutsceneDialogLine line = LineAt(currentIndex);
            cutscenePartTimer = line != null ? dialogPartTime : cutscenePartTime;
            while (cutscenePartTimer > 0)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    if (dialogWindow != null && dialogWindow.IsTyping)
                        dialogWindow.Complete();
                    else
                        break;
                }

                cutscenePartTimer -= Time.unscaledDeltaTime;
                yield return null;
            }

            yield return null;

            currentIndex++;
            bool finished = currentIndex >= playingParts.Count;

            CutsceneDialogLine next = LineAt(currentIndex);
            bool samePhoto = line != null && next != null && line.photo == next.photo;

            if (!samePhoto)
                yield return Fade(1);

            if (finished && !ending)
                Stop();
            else
                ShowCurrentPart();

            if (!samePhoto)
                yield return Fade(0);

            if (finished)
                break;
        }

        routine = null;
    }

    private CutsceneDialogLine LineAt(int index)
    {
        if (playingParts == null || index < 0 || index >= playingParts.Count || playingParts[index] == null)
            return null;

        return playingParts[index].GetComponent<CutsceneDialogLine>();
    }

    private void ShowCurrentPart()
    {
        CutsceneDialogLine line = LineAt(currentIndex);

        HideLinePhotos();
        for (int i = 0; i < playingParts.Count; i++)
            playingParts[i].SetActive(i == currentIndex);

        if (line != null && line.photo != null)
            line.photo.SetActive(true);

        if (dialogWindow == null)
            return;

        if (line != null)
            dialogWindow.Show(line.speaker, line.text);
        else
            dialogWindow.Hide();
    }

    private void HideLinePhotos()
    {
        for (int i = 0; i < playingParts.Count; i++)
        {
            CutsceneDialogLine line = LineAt(i);
            if (line != null && line.photo != null)
                line.photo.SetActive(false);
        }
    }

    private void Stop()
    {
        Time.timeScale = 1;

        HideLinePhotos();
        foreach (GameObject part in playingParts)
            part.SetActive(false);

        if (dialogWindow != null)
            dialogWindow.Hide();

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
