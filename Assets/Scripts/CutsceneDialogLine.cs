using UnityEngine;

/// <summary>
/// One line of dialogue, used as a part of a cutscene: put the object in the Cutscener's
/// Start/End Cutscene Parts list. While it is the current part, `photo` is shown with the
/// dialog window on top of it. Lines that follow each other on the same photo switch
/// without the black fade.
/// </summary>
public class CutsceneDialogLine : MonoBehaviour
{
    public string speaker;
    [TextArea(2, 5)] public string text;
    [Tooltip("The cutscene picture that stays on screen behind this line.")]
    public GameObject photo;
}
