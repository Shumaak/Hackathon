using UnityEngine;

public class RubberBallController : MonoBehaviour
{
    private Vector2 dragOffset;
    private Rigidbody rb;
    private Camera cam;

    [SerializeField] private float maxStrength = 5f;
    [SerializeField] private float strengthMultiplier = 5f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
    }

    void OnMouseDrag()
    {
        dragOffset = transform.position - MouseWorldPosition();
    }

    void OnMouseUp()
    {
        Vector2 direction = dragOffset.normalized;
        float strength = Mathf.Clamp(dragOffset.magnitude, 0, maxStrength);
        rb.AddForce(direction * strength * strengthMultiplier, ForceMode.Impulse);
        dragOffset = Vector2.zero;
    }

    private Vector3 MouseWorldPosition()
    {
        Vector3 mouse = Input.mousePosition;
        mouse.z = Vector3.Dot(transform.position - cam.transform.position, cam.transform.forward);
        return cam.ScreenToWorldPoint(mouse);
    }
}
