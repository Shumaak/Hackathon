using UnityEngine;

public class PhotoLevelArea : MonoBehaviour
{
    public Vector2 size = new Vector2(16f, 12f);
    public Transform spawn;
    public float spawnScale = 1f;
    [Tooltip("How much higher than normal the ball jumps in this level (2 = twice as high).")]
    public float jumpHeight = 1f;
    [Tooltip("Fade through black when the ball arrives in this level (also when it is put back on the spawn point). Off = straight cut.")]
    public bool shouldFadeBlack = true;
    public PhotoLevelArea above;
    public PhotoLevelArea below;
    public PhotoLevelArea right;
    public PhotoLevelArea left;
    [Tooltip("Where the ball appears when it comes back into this level from the level on its right.")]
    public Transform spawnFromRight;

    public float Left => transform.position.x;
    public float Right => transform.position.x + size.x;
    public float Bottom => transform.position.y;
    public float Top => transform.position.y + size.y;

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = transform.position + new Vector3(size.x / 2f, size.y / 2f, 0f);
        Gizmos.DrawWireCube(center, new Vector3(size.x, size.y, 0f));
        if (spawn != null) Gizmos.DrawWireSphere(spawn.position, 0.5f * spawnScale);
        if (spawnFromRight != null) Gizmos.DrawWireSphere(spawnFromRight.position, 0.5f * spawnScale);
    }
}
