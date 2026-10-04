using UnityEngine;

public class PlayerManager : MonoBehaviour
{
  InputManager inputManager;
  PlayerMovement playerMovement;

  void Awake()
  {
    inputManager = GetComponent<InputManager>();
    playerMovement = GetComponent<PlayerMovement>();
  }

  void Update()
  {
    inputManager.HandleAllInputs();
  }

  void FixedUpdate()
  {
    playerMovement.handleAllMovement();
  }
}
