using System;
using UnityEngine;

public class RubberBallController : MonoBehaviour
{
    private Vector2 dragOffset;
    private Rigidbody rb;
    private Camera cam;
    public bool inAir;
    private bool still;

    private float deform; // 0 = round, >0 stretched, <0 squashed

    [Header("Squish")]
    [SerializeField] private float maxStretch = 0.3f;
    [SerializeField] private float impactSquish = 0.3f;
    [SerializeField] private float squishReturnSpeed = 12f;

    [SerializeField] private float maxStrength = 5f;
    [SerializeField] private float strengthMultiplier = 5f;
    [Tooltip("The ball size at which a launch has its normal strength. A bigger ball jumps further, a smaller one less.")]
    [SerializeField] private float sizeForNormalJump = 1f;
    [Tooltip("Multiplies how high and far a launch goes. Photo levels set this per level.")]
    public float jumpHeightMultiplier = 1f;
    [SerializeField] private float GroundCheckDistance;
    [SerializeField] private float stillThreshold;
    [SerializeField] private Transform visuals;
    [SerializeField] private float maxStretchAtMagnitude;



    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Transform arrow;

    [Header("Arrow colour by charge")]
    [SerializeField] private Color weakColor = new Color(0.2f, 0.85f, 0.2f);
    [SerializeField] private Color mediumColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private Color fullColor = new Color(1f, 0.15f, 0.1f);

    private SpriteRenderer[] arrowPictures;

    private float BallScale => transform.lossyScale.y;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
        arrowPictures = arrow.GetComponentsInChildren<SpriteRenderer>(true);
    }

    void OnMouseDrag()
    {
        if (inAir || !still) return;

        dragOffset = transform.position - MouseWorldPosition();
    }

    private void UpdateArrow()
    {
        arrow.transform.localScale = new Vector3(Mathf.Clamp(dragOffset.magnitude, 0, maxStrength), 1, 1);
        arrow.right = -dragOffset.normalized;

        // Green at no charge, yellow at half, red at full.
        float charge = maxStrength > 0f ? Mathf.Clamp01(dragOffset.magnitude / maxStrength) : 1f;
        Color color = charge < 0.5f
            ? Color.Lerp(weakColor, mediumColor, charge * 2f)
            : Color.Lerp(mediumColor, fullColor, charge * 2f - 1f);
        foreach (SpriteRenderer picture in arrowPictures)
            picture.color = color;
    }

    void OnMouseUp()
    {
        if (!inAir && still)
        {
            Vector2 direction = dragOffset.normalized;
            // The drag distance for full power stays the same at any size, so it always fits
            // on screen. A resized ball jumps in proportion to its size (relative to
            // sizeForNormalJump): sqrt because jump height and distance grow with the square
            // of launch speed.
            float strength = Mathf.Clamp(dragOffset.magnitude, 0, maxStrength);
            rb.AddForce(direction * strength * strengthMultiplier * Mathf.Sqrt(jumpHeightMultiplier * BallScale / sizeForNormalJump), ForceMode.Impulse);
            inAir = true;

        }
        dragOffset = Vector2.zero;
    }

    void Update()
    {
        if (Physics.Raycast(transform.position, Vector3.down, GroundCheckDistance * BallScale, layerMask))
        {
            inAir = false;
        }
        else
        {
            inAir = true;
        }

        still = rb.linearVelocity.magnitude <= stillThreshold;
        arrow.gameObject.SetActive(dragOffset != Vector2.zero || !inAir);

        UpdateSquish();

        UpdateArrow();
    }

    private void UpdateSquish()
    {
        Vector3 velocity = rb.linearVelocity;
        float target = maxStretch * Mathf.Clamp01(velocity.magnitude / maxStretchAtMagnitude);
        deform = Mathf.Lerp(deform, target, squishReturnSpeed * Time.deltaTime);

        if (deform >= 0f && velocity.magnitude > 0.5f)
            visuals.right = velocity.normalized;

        visuals.localScale = new Vector3(1f + deform, 1f / (1f + deform), 1f);
    }

    void OnCollisionEnter(Collision collision)
    {
        visuals.right = collision.GetContact(0).normal;
        deform = -impactSquish * Mathf.Clamp01(collision.relativeVelocity.magnitude / maxStretchAtMagnitude * 2f);
    }

    private Vector3 MouseWorldPosition()
    {
        Vector3 mouse = Input.mousePosition;
        mouse.z = Vector3.Dot(transform.position - cam.transform.position, cam.transform.forward);
        return cam.ScreenToWorldPoint(mouse);
    }
    void OnDrawGizmos()
    {
        Gizmos.color = inAir ? Color.red : Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * GroundCheckDistance * BallScale);
    }
}
