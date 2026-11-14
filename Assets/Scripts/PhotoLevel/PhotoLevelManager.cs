using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Tracks which photo the ball is in. When the ball leaves through the top or the right edge,
/// falls out of the bottom, or lands on a surface that leads to another level, fades to black,
/// moves it to that level's spawn point and fades back in.
/// </summary>
public class PhotoLevelManager : MonoBehaviour
{
    public static PhotoLevelArea Current { get; private set; }
    public static PhotoLevelManager Instance { get; private set; }

    [SerializeField] private PhotoLevelArea startLevel;
    [SerializeField] private float fadeTime = 0.35f;

    float fade;          // 0 = clear, 1 = black
    bool transitioning;

    public void SetStartLevel(PhotoLevelArea level) => startLevel = level;

    void Awake()
    {
        Instance = this;
        Current = startLevel;
    }

    void Start()
    {
        ApplyJumpHeight();
    }

    void ApplyJumpHeight()
    {
        if (PhotoBall.Instance == null || Current == null) return;
        var controller = PhotoBall.Instance.GetComponent<RubberBallController>();
        if (controller != null) controller.jumpHeightMultiplier = Current.jumpHeight;
    }

    void OnDestroy()
    {
        Current = null;
        if (Instance == this) Instance = null;
    }

    /// <summary>Fade to black and move the ball to the given level's spawn point.</summary>
    public void GoTo(PhotoLevelArea level, Transform spawnPoint = null)
    {
        var ball = PhotoBall.Instance;
        if (transitioning || ball == null || level == null || level == Current) return;
        if (spawnPoint == null) spawnPoint = level.spawn != null ? level.spawn : level.transform;
        StartCoroutine(Travel(level, spawnPoint, ball));
    }

    void Update()
    {
        var ball = PhotoBall.Instance;
        if (transitioning || ball == null || !ball.enabled || Current == null) return;

        Vector3 position = ball.transform.position;
        float radius = 0.5f * ball.Scale;
        if (Current.above != null && position.y - radius > Current.Top) GoTo(Current.above);
        else if (Current.right != null && position.x > Current.Right) GoTo(Current.right);
        else if (Current.left != null && position.x < Current.Left) GoTo(Current.left, Current.left.spawnFromRight);
        else if (Current.below != null && position.y + radius < Current.Bottom) GoTo(Current.below);
    }

    IEnumerator Travel(PhotoLevelArea level, Transform spawnPoint, PhotoBall ball)
    {
        transitioning = true;
        var body = ball.GetComponent<Rigidbody>();
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        while (fade < 1f)
        {
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / fadeTime);
            yield return null;
        }

        Current = level;
        ApplyJumpHeight();
        ball.SnapScale(level.spawnScale);
        Vector3 position = spawnPoint.position;
        position.z = ball.transform.position.z;
        ball.transform.position = position;
        body.position = position;

        // Make the camera cut to the new level instead of flying over to it.
        foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            cam.PreviousStateIsValid = false;

        yield return null;
        yield return null;
        body.isKinematic = false;

        while (fade > 0f)
        {
            fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime / fadeTime);
            yield return null;
        }
        transitioning = false;
    }

    void OnGUI()
    {
        if (fade <= 0f) return;
        GUI.depth = -1000;
        GUI.color = new Color(0f, 0f, 0f, fade);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
