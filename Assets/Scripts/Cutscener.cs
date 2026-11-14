using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

public class Cutscener : MonoBehaviour
{
    public List<GameObject> StartCutsceneParts;
    public List<GameObject> EndCutsceneParts;
    public GameObject StartBG;
    public GameObject EndBG;

    [SerializeField] private float cutscenePartTime = 2.5f;
    [SerializeField][ReadOnly] private float cutscenePartTimer;
    [SerializeField][ReadOnly] private int currentIndex;

    private List<GameObject> playingParts;
    private GameObject playingBG;
    private bool ending;

    void Start()
    {
        PlayStart();
    }

    public void PlayStart() => Play(StartCutsceneParts, StartBG);

    public void PlayEnd() => Play(EndCutsceneParts, EndBG,true);

    private void Play(List<GameObject> parts, GameObject bg,bool end =false)
    {
        ending = end;
        Time.timeScale = 0;
        playingParts = parts;
        playingBG = bg;
        cutscenePartTimer = cutscenePartTime;
        currentIndex = 0;

        if (playingBG != null)
            playingBG.SetActive(true);

        ShowCurrentPart();
    }

    void Update()
    {
        if (playingParts == null)
            return;

        cutscenePartTimer -= Time.unscaledDeltaTime;

        if (Input.GetMouseButtonDown(0))
            cutscenePartTimer = 0;

        if (cutscenePartTimer > 0)
            return;

        currentIndex++;
        cutscenePartTimer = cutscenePartTime;

        if (currentIndex >= playingParts.Count && !ending)
        {
            Stop();
            return;
        }

        ShowCurrentPart();
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
}
