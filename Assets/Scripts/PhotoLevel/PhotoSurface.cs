using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class PhotoSurface : MonoBehaviour
{
    public float ballScale = 1f;
    public bool oneWay;
    [Tooltip("Landing on this surface sends the ball to that level.")]
    public PhotoLevelArea leadsTo;
    public UnityEvent OnCollision;


    Collider col;
    bool ignoring;

    void Start()
    {
        col = GetComponent<Collider>();
        if (oneWay && PhotoBall.Instance != null) SetIgnoring(true);
    }

    void FixedUpdate()
    {
        var ball = PhotoBall.Instance;
        if (!oneWay || ball == null) return;

        float top = col.bounds.max.y;
        if (ignoring)
        {
            if (ball.Bottom >= top - 0.05f * ball.Scale) SetIgnoring(false);
        }
        else if (ball.transform.position.y < top)
        {
            SetIgnoring(true);
        }
    }

    void SetIgnoring(bool value)
    {
        ignoring = value;
        Physics.IgnoreCollision(col, PhotoBall.Instance.Collider, value);
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = oneWay ? new Color(1f, 0.8f, 0f, 0.35f) : new Color(0f, 1f, 0.3f, 0.35f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = oneWay ? new Color(1f, 0.8f, 0f) : new Color(0f, 1f, 0.3f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
