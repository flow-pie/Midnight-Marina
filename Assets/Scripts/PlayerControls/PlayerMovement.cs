using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Scripts References")]
    InputManager inputManager;

    [Header("Movement Variables")]
    Vector3 movementDirection;
    Transform cameraObject;
    Rigidbody playerRigidbody;

    public float movementSpeed = 5f;

    void Awake()
    {
        inputManager = GetComponent<InputManager>();
        playerRigidbody = GetComponent<Rigidbody>();
    }

    public void HandleMovement()
    {
        movementDirection = cameraObject.forward * inputManager.verticalInput 
            +  cameraObject.right * inputManager.horizontalInput;

        movementDirection.Normalize();
        movementDirection.y = 0;

        Vector3 movementVelocity = movementDirection * movementSpeed; //to determine final velocity of the player
        playerRigidbody.linearVelocity = movementVelocity;
    }


}
