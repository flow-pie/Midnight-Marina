using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Scripts References")]
    InputManager inputManager;

    [Header("Movement Variables")]
    Vector3 movementDirection;
    public Transform cameraObject;
    Rigidbody playerRigidbody;

    public float movementSpeed = 5f;
    public float rotationSpeed = 10f;

    void Awake()
    {
        inputManager = GetComponent<InputManager>();
        playerRigidbody = GetComponent<Rigidbody>();
        cameraObject = Camera.main.transform;
    }
    public void handleAllMovement()
    {
        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        movementDirection = cameraObject.forward * inputManager.verticalInput 
            +  cameraObject.right * inputManager.horizontalInput;

        movementDirection.Normalize();
        movementDirection.y = 0;

        Vector3 movementVelocity = movementDirection * movementSpeed; //to determine final velocity of the player
        playerRigidbody.linearVelocity = movementVelocity;
    }
    
    private void HandleRotation()
    {
        Vector3 targetDirection = Vector3.zero;

        //calculate the target direction based on camera orientation and player input
        targetDirection= cameraObject.forward * inputManager.verticalInput
            + cameraObject.right * inputManager.horizontalInput;
        targetDirection.Normalize();
        targetDirection.y = 0;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        Quaternion playerRotation = Quaternion.Slerp(playerRigidbody.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        
        transform.rotation = playerRotation;
    }


}
