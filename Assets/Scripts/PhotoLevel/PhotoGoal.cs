using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class PhotoGoal : MonoBehaviour
{
    [SerializeField] private float suckTime = 0.8f;

    bool finished;
    float finishTime;

    void OnTriggerEnter(Collider other)
    {
        var ball = other.GetComponent<PhotoBall>();
        if (finished || ball == null) return;
        finished = true;
        finishTime = Time.timeSinceLevelLoad;
        StartCoroutine(SuckIn(ball));
    }

    IEnumerator SuckIn(PhotoBall ball)
    {
        var body = ball.GetComponent<Rigidbody>();
        body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
        ball.enabled = false;
        ball.Collider.enabled = false;
        var controller = ball.GetComponent<RubberBallController>();
        if (controller != null) controller.enabled = false;

        Vector3 fromPosition = ball.transform.position;
        Vector3 fromScale = ball.transform.localScale;
        Vector3 hole = new Vector3(transform.position.x, transform.position.y, fromPosition.z);
        for (float t = 0f; t < 1f; t += Time.deltaTime / suckTime)
        {
            float eased = t * t;
            ball.transform.position = Vector3.Lerp(fromPosition, hole, eased);
            ball.transform.localScale = Vector3.Lerp(fromScale, Vector3.zero, eased);
            yield return null;
        }
        ball.transform.localScale = Vector3.zero;
    }

    void Update()
    {
        if (finished && Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnGUI()
    {
        if (!finished) return;
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Screen.height / 10,
            fontStyle = FontStyle.Bold,
        };
        var area = new Rect(0, Screen.height * 0.3f, Screen.width, Screen.height * 0.2f);
        GUI.Label(area, "DOWN THE SLIDE!", style);
        style.fontSize = Screen.height / 28;
        area.y += Screen.height * 0.16f;
        GUI.Label(area, finishTime.ToString("0.0") + " s   -   press R to play again", style);
    }
}
