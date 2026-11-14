using System;
using UnityEngine;

public class RubberBallController : MonoBehaviour
{
    private Vector2 dragOffset;
    private Rigidbody rb;
    private Camera cam;
    public bool inAir;
    [SerializeField] private float maxStrength = 5f;
    [SerializeField] private float strengthMultiplier = 5f;
    [SerializeField] private float GroundCheckDistance;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Transform arrow;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
    }

    void OnMouseDrag()
    {
        if (inAir) return;
        
        dragOffset = transform.position - MouseWorldPosition();
        UpdateArrow();
    }

    private void UpdateArrow()
    {
        arrow.transform.localScale = new Vector3(Mathf.Clamp(dragOffset.magnitude, 0, maxStrength), 1, 1);
        arrow.right = -dragOffset.normalized;
    }

    void OnMouseUp()
    {
        if (!inAir)
        {
            Vector2 direction = dragOffset.normalized;
            float strength = Mathf.Clamp(dragOffset.magnitude, 0, maxStrength);
            rb.AddForce(direction * strength * strengthMultiplier, ForceMode.Impulse);
            inAir = true;

            dragOffset = Vector2.zero;
        }
    }

    void Update()
    {
        if (Physics.Raycast(transform.position, Vector3.down, GroundCheckDistance, layerMask))
        {
            inAir = false;
        }
        else
        {
            inAir = true;
        }

        arrow.gameObject.SetActive(dragOffset != Vector2.zero && !inAir);
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
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * GroundCheckDistance);
    }
}
