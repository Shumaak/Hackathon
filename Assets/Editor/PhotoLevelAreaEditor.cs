using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws PhotoLevelArea with Unity's own UI Toolkit inspector instead of Odin's IMGUI one.
/// Unity 6000.7.0b5 segfaults (TextSelectionService::MoveDown) when the Down arrow is pressed
/// in an Odin number field, which killed the editor while editing Jump Height.
/// </summary>
[CustomEditor(typeof(PhotoLevelArea)), CanEditMultipleObjects]
public class PhotoLevelAreaEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);
        return root;
    }
}
