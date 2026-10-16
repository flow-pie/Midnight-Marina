using UnityEngine;

public class PlayerManager : MonoBehaviour
{
  InputManager inputManager;
  PlayerMovement playerMovement;
  CameraManager cameraManager;
  Animator animator;

  public bool isInteracting;

  void Awake()
  {
    inputManager = GetComponent<InputManager>();
    playerMovement = GetComponent<PlayerMovement>();
    cameraManager = FindAnyObjectByType<CameraManager>();
    animator = GetComponent<Animator>();
  }

  void Update()
  {
    inputManager.HandleAllInputs();
    cameraManager.HandleCameraMovement();
  }

  void FixedUpdate()
  {
    playerMovement.handleAllMovement();
  }

  void LateUpdate()
  {
    isInteracting = animator.GetBool("isInteracting");
    playerMovement.SetIsJumping(animator.GetBool("isJumping"));
    animator.SetBool("isGrounded", playerMovement.isGrounded);
  }
}
