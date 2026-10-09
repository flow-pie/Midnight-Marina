using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Scripts References")]
    InputManager inputManager;

    [Header("Movement Variables")]
    Vector3 movementDirection;
    public Transform cameraObject;
    Rigidbody playerRigidbody;

    [Header("Movement Flags")]
    public bool isSprinting;
    public bool isWalking;

    [Header("Movement Values")]
    public float walkingSpeed = 1.5f;
    public float runningSpeed = 5f;
    public float sprintingSpeed = 10f;
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
        
        if(isSprinting)
        {
            movementDirection *= sprintingSpeed;
        } else {
            if(inputManager.moveAmount >= 0.5f)
            {
                movementDirection *= runningSpeed;
                isWalking = false;
            } else {
                movementDirection *= walkingSpeed;
                isWalking = true;
            }
        }

        Vector3 movementVelocity = movementDirection; //to determine final velocity of the player
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

        if(targetDirection == Vector3.zero)
        {
            targetDirection = transform.forward;
        }

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        Quaternion playerRotation = Quaternion.Slerp(playerRigidbody.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        
        transform.rotation = playerRotation;
    }


}
