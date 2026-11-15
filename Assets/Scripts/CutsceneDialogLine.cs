using UnityEngine;

public class CutsceneDialogLine : MonoBehaviour
{
    public string speaker;
    [TextArea(2, 5)] public string text;
    [Tooltip("The cutscene picture that stays on screen behind this line.")]
    public GameObject photo;
}
