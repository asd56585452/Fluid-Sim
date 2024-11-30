using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamController : MonoBehaviour
{
    public GameObject Camera; // Reference to the Camera GameObject
    public float MovingSpeed = 0.5f; // Rotation speed in degrees per second
    private float currentHeight = 5f; // Track the current height of the camera

    void Update()
    {
        MoveCam();
    }

    void MoveCam()
    {
        float heightInput = 0f;

        // Check if 'E' is being pressed for surging
        if (Input.GetKey(KeyCode.E))
        {
            heightInput = MovingSpeed * Time.deltaTime;
        }
        // Check if 'Q' is being pressed for nosedive
        else if (Input.GetKey(KeyCode.Q))
        {
            heightInput = -MovingSpeed * Time.deltaTime;
        }

        // Calculate the new height, ensuring it stays within valid range
        float newHeight = Mathf.Clamp(currentHeight + heightInput, 0.5f, 15f);

        // Apply the height change only if it's within bounds
        Camera.transform.position = new Vector3(Camera.transform.position.x, newHeight, Camera.transform.position.z);

        // Update the current height tracking variable
        currentHeight = newHeight;
    }
}
