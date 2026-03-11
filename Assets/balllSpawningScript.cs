using UnityEngine;
using UnityEngine.XR.Hands;

public class balllSpawningScript : MonoBehaviour
{
    public GameObject ballPrefab; // Reference to the ball prefab
    public GameObject leftHand;
    public GameObject rightHand;
    private bool leftTouch, rightTouch, handTouch;

    // Update is called once per frame
    void Update()
    {
        if (handTouch) {
            //if a grabbing motion is made
            spawnBall();
        }
    }

    void spawnBall()
    {
        if (leftTouch) {
            Instantiate(ballPrefab, leftHand.transform.position, Quaternion.identity);
        }
        else if (rightTouch) {
            Instantiate(ballPrefab, rightHand.transform.position, Quaternion.identity);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("LeftHand")) {
            leftTouch = true;
            handTouch = true;
        }
        else if (collision.gameObject.CompareTag("RightHand")) {
            rightTouch = true;
            handTouch = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("LeftHand")) {
            leftTouch = false;
            handTouch = false;
        }
        else if (collision.gameObject.CompareTag("RightHand")) {
            rightTouch = false;
            handTouch = false;
        }
    }
}
