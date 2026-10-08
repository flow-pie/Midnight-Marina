using UnityEngine;

public class CameraManager : MonoBehaviour
{
    InputManager inputManager;

    public Transform playerTransform;
    public Transform cameraPivot;
    private Vector3 cameraFollowVelocity = Vector3.zero;

    [Header("Camera Movement and Rotation")]
    public float cameraFollowSpeed = 0.1f;
    public float cameraLookSpeed = 0.1f;
    public float cameraPivotSpeed = 1.0f;
    public float lookAngle;
    [Header("Camera Follow Settings")]
    public float pivotAngle;

    void Awake()
    {
        PlayerManager playerManager = FindAnyObjectByType<PlayerManager>();
        if (playerManager != null)  playerTransform = playerManager.transform;
        
        inputManager = FindAnyObjectByType<InputManager>();
        if (inputManager == null)   Debug.LogError("InputManager not found in the scene.");
        
    }

    public void HandleCameraMovement()
    {
        FollowTarget();
        HandleCameraRotation();
    }

    void FollowTarget()
    {
        Vector3 targetPosition  = Vector3.SmoothDamp(transform.position, playerTransform.position, ref cameraFollowVelocity, cameraFollowSpeed);
        transform.position = targetPosition;
    }

    void HandleCameraRotation()
    {
        Vector3 rotation;
        Quaternion targetRotation;

        //calculate the rotation of the camera based on the input from the player
        if (inputManager == null) return;
        lookAngle += (inputManager.cameraInputX * cameraLookSpeed);
        pivotAngle -= (inputManager.cameraInputY * cameraPivotSpeed);

        //horizontal rotation of the camera
        rotation = Vector3.zero;
        rotation.y = lookAngle;
        targetRotation = Quaternion.Euler(rotation);
        transform.rotation = targetRotation;

        //vertical rotation of the camera
        rotation = Vector3.zero;
        rotation.x = pivotAngle;
        targetRotation = Quaternion.Euler(rotation);
        cameraPivot.localRotation = targetRotation;
    }
}
