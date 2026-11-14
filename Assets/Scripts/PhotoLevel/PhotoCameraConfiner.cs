using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Goes on the CinemachineCamera. Keeps the view inside the current level's photo, so the
/// camera never shows anything beyond the picture's edges. The camera keeps its normal
/// distance; it only moves closer if the window is so wide that the view would not fit.
/// </summary>
public class PhotoCameraConfiner : CinemachineExtension
{
    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        var level = PhotoLevelManager.Current;
        if (stage != CinemachineCore.Stage.Body || level == null) return;

        Vector3 position = state.GetCorrectedPosition();
        float tan = Mathf.Tan(state.Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float aspect = state.Lens.Aspect;

        float distance = Mathf.Abs(level.transform.position.z - position.z);
        float fitDistance = Mathf.Min(level.size.x / (2f * tan * aspect), level.size.y / (2f * tan));
        distance = Mathf.Min(distance, fitDistance);

        float halfHeight = distance * tan;
        float halfWidth = halfHeight * aspect;
        Vector3 confined = new Vector3(
            Mathf.Clamp(position.x, level.Left + halfWidth, level.Right - halfWidth),
            Mathf.Clamp(position.y, level.Bottom + halfHeight, level.Top - halfHeight),
            level.transform.position.z - distance);
        state.PositionCorrection += confined - position;
    }
}
