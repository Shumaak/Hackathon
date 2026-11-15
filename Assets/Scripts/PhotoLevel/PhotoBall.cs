using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class PhotoBall : MonoBehaviour
{
    public static PhotoBall Instance { get; private set; }

    [SerializeField] private float scaleSpeed = 3f;

    SphereCollider sphere;
    float targetScale;

    public Collider Collider => sphere;
    public float Scale => transform.localScale.x;
    public float Bottom => transform.position.y - sphere.radius * Scale;

    void Awake()
    {
        Instance = this;
        sphere = GetComponent<SphereCollider>();
        targetScale = Scale;
    }

    public void SnapScale(float scale)
    {
        targetScale = scale;
        transform.localScale = Vector3.one * scale;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnCollisionEnter(Collision collision) => ReadSurface(collision);
    void OnCollisionStay(Collision collision) => ReadSurface(collision);

    void ReadSurface(Collision collision)
    {
        var surface = collision.collider.GetComponent<PhotoSurface>();
        if (surface == null) return;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                targetScale = surface.ballScale;
                if (surface.leadsTo != null && surface.leadsTo != PhotoLevelManager.Current && PhotoLevelManager.Instance != null)
                    PhotoLevelManager.Instance.GoTo(surface.leadsTo);
                surface.OnCollision?.Invoke();
                return;
            }
        }
    }

    void FixedUpdate()
    {
        float current = Scale;
        if (Mathf.Approximately(current, targetScale)) return;

        float next = Mathf.MoveTowards(current, targetScale, scaleSpeed * Time.fixedDeltaTime);
        transform.localScale = Vector3.one * next;
        transform.position += Vector3.up * (sphere.radius * (next - current));
    }
}
