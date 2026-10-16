using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    PlayerControls playerControls;
    AnimatorManager animatorManager;
    PlayerMovement playerMovement;

    public float moveAmount;
    private Vector2 movementInput;
    public float verticalInput;
    public float horizontalInput;
    private Vector2 cameraInput;
    public float cameraInputX;
    public float cameraInputY;

    [Header("Input Buttons Flags")]
    public bool bInput;
    public bool JumpInput;

    void Awake()
    {
        animatorManager = GetComponent<AnimatorManager>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    void OnEnable ()
    {
        if(playerControls == null)
        {
            playerControls = new PlayerControls();
            playerControls.PlayerMovement.Movement.performed += i => movementInput = i.ReadValue<Vector2>();
            playerControls.PlayerMovement.Movement.canceled += i => movementInput = i.ReadValue<Vector2>();

            playerControls.PlayerMovement.CameraMovement.performed += i => cameraInput = i.ReadValue<Vector2>();
            playerControls.PlayerMovement.CameraMovement.canceled += i => cameraInput = i.ReadValue<Vector2>();

            playerControls.PlayActions.B.performed += i => bInput = true;
            playerControls.PlayActions.B.canceled += i => bInput = false;

            playerControls.PlayActions.Jump.performed += i => JumpInput = true;
            playerControls.PlayActions.Jump.canceled += i => JumpInput = false;
        }

        playerControls.Enable();
    }

    void OnDisable()
    {
        playerControls.Disable();
    }
    public void HandleAllInputs()
    {
        HandleMovementInput();
        HandleSprintingInput();
        HandleJumpInput();
    }

    private void HandleJumpInput()
    {
        if(JumpInput)
        {
            JumpInput = false;
            playerMovement.SetIsJumping(true);
            playerMovement.HandleJumping();
        }
    }

    private void HandleMovementInput()
    {
        verticalInput = movementInput.y;
        horizontalInput = movementInput.x;

        cameraInputX = cameraInput.x;
        cameraInputY = cameraInput.y;

        moveAmount = Mathf.Clamp01(Mathf.Abs(horizontalInput) + Mathf.Abs(verticalInput));
        animatorManager.updateAnimatorValues(0f, moveAmount, playerMovement.isSprinting);
    }

    private void HandleSprintingInput()
    {
        if(bInput && moveAmount > 0.5) playerMovement.isSprinting = true;
        else playerMovement.isSprinting = false;
    }
}
