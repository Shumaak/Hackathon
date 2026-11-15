using TMPro;
using UnityEngine;

/// <summary>
/// The dialogue box: the speaker's name and a line of text that types itself out. Runs on unscaled
/// time, so it works while the cutscene has the game paused.
/// </summary>
public class DialogWindow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text lineText;
    [SerializeField] private GameObject nameTag;
    [Tooltip("How fast the line types out. 0 = the whole line appears at once.")]
    [SerializeField][Min(0)] private float charactersPerSecond = 45f;

    private float shown;
    private int total;

    /// <summary>True while the line is still typing out.</summary>
    public bool IsTyping => gameObject.activeSelf && shown < total;

    public void Show(string speaker, string text)
    {
        gameObject.SetActive(true);

        bool hasName = !string.IsNullOrEmpty(speaker);
        if (nameTag != null)
            nameTag.SetActive(hasName);
        if (nameText != null)
            nameText.text = speaker;

        lineText.text = text;
        lineText.ForceMeshUpdate();
        total = lineText.textInfo.characterCount;
        shown = charactersPerSecond > 0 ? 0 : total;
        lineText.maxVisibleCharacters = (int)shown;
    }

    /// <summary>Show the rest of the line at once.</summary>
    public void Complete()
    {
        shown = total;
        lineText.maxVisibleCharacters = total;
    }

    public void Hide() => gameObject.SetActive(false);

    void Update()
    {
        if (shown >= total)
            return;

        shown = Mathf.Min(total, shown + charactersPerSecond * Time.unscaledDeltaTime);
        lineText.maxVisibleCharacters = (int)shown;
    }
}
