using UnityEngine;

public class PlayerManager : MonoBehaviour
{
  InputManager inputManager;
  PlayerMovement playerMovement;
  CameraManager cameraManager;

  void Awake()
  {
    inputManager = GetComponent<InputManager>();
    playerMovement = GetComponent<PlayerMovement>();
    cameraManager = FindAnyObjectByType<CameraManager>();
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
}
