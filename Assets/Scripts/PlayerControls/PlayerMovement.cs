using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Scripts References")]
    InputManager inputManager;
    PlayerManager playerManager;
    AnimatorManager animatorManager;


    [Header("Movement Variables")]
    Vector3 movementDirection;
    public Transform cameraObject;
    Rigidbody playerRigidbody;

    [Header("Movement Flags")]
    public bool isSprinting;
    public bool isWalking;
    public bool isGrounded;

    [Header("Movement Values")]
    public float walkingSpeed = 1.5f;
    public float runningSpeed = 5f;
    public float sprintingSpeed = 10f;
    public float rotationSpeed = 10f;

    [Header("Falling and Landing")]
    public float inAirTimer;
    public float leapingVelocity;
    public float fallingVelocity;
    public float rayCastHeightOffset = 0.5f;
    public LayerMask groundLayer;

    [Header("Jump Variables")]
    public float jumpHeight = 4f;
    public float gravityIntensity = -15f;
    private bool isJumping;

    void Awake()
    {
        inputManager = GetComponent<InputManager>();
        playerManager = GetComponent<PlayerManager>();
        playerRigidbody = GetComponent<Rigidbody>();
        animatorManager = GetComponent<AnimatorManager>();
        
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = Camera.allCameras.Length > 0 ? Camera.allCameras[0] : null;
        }
        if (mainCamera == null)
        {
            mainCamera = FindAnyObjectByType<Camera>();
        }
        if (mainCamera != null)
        {
            cameraObject = mainCamera.transform;
        }
        else
        {
            Debug.LogError("No camera found in scene. PlayerMovement requires a camera.");
        }
    }
    public void handleAllMovement()
    {
        HandleFallingAndLanding();

        if(playerManager.isInteracting) return;
        
        if(isJumping) return;

        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        if(isJumping) return;

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
        if(isJumping) return;

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

    void HandleFallingAndLanding()
    {
        RaycastHit hit;

        Vector3 rayCastOrigin = transform.position;
        Vector3 targetPosition ;

        rayCastOrigin.y += rayCastHeightOffset;
        targetPosition = transform.position;

        if(!isGrounded && !isJumping)
        {
            if(!playerManager.isInteracting)
            {
                animatorManager.PlayTargetAnimation("Falling", true);
            }
        }
        inAirTimer += Time.deltaTime;
        playerRigidbody.AddForce(transform.forward * leapingVelocity);
        playerRigidbody.AddForce(-Vector3.up * fallingVelocity * inAirTimer);

        if(Physics.SphereCast(rayCastOrigin, 0.3f, -Vector3.up, out hit, groundLayer))
        {
            if(!isGrounded && playerManager.isInteracting)
            {
                animatorManager.PlayTargetAnimation("Landing", true);
            }
            Vector3 rayCastHitPoint = hit.point;
            targetPosition.y = rayCastHitPoint.y;
            inAirTimer = 0;
            isGrounded = true;
        } else {
            isGrounded = false;
        }

        if(isGrounded && !isJumping)
        {
            if(playerManager.isInteracting || inputManager.moveAmount > 0)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime / 0.1f);
            } else {
                transform.position = targetPosition;
            }
        }
    }

    public void HandleJumping()
    {
        if(isGrounded)
        {
            animatorManager.animator.SetBool("isJumping", true);
            animatorManager.PlayTargetAnimation("Jumping", false);

            float jumpVelocity = Mathf.Sqrt(-2 * gravityIntensity * jumpHeight);
            Vector3 playerVelocity = movementDirection;
            playerVelocity.y = jumpVelocity;
            playerRigidbody.linearVelocity = playerVelocity;

            isJumping = false;

        }
    }

    public void SetIsJumping(bool value)
    {
        isJumping = value;
    }

}
