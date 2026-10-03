using UnityEngine;

public class InputManager : MonoBehaviour
{
    PlayerControls playerControls;
    public Vector2 movementInput;

    void OnEnable ()
    {
        if(playerControls == null)
        {
            playerControls = new PlayerControls();
            playerControls.PlayerMovement.Movement.performed += i => movementInput = i.ReadValue<Vector2>();
            playerControls.PlayerMovement.Movement.canceled += i => movementInput = i.ReadValue<Vector2>();
        }

        playerControls.Enable();
    }

    void OnDisable()
    {
        playerControls.Disable();
    }
}
