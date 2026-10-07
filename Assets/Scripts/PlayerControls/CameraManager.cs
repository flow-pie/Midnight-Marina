using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public Transform playerTransform;
    private Vector3 cameraFollowVelocity = Vector3.zero;
    public float cameraFollowSpeed = 0.1f;

    void Awake()
    {
        PlayerManager playerManager = FindAnyObjectByType<PlayerManager>();
        if (playerManager != null)
        {
            playerTransform = playerManager.transform;
        }
    }

    void Update()
    {
        FollowTarget();
    }

    void FollowTarget()
    {
        Vector3 targetPosition  = Vector3.SmoothDamp(transform.position, playerTransform.position, ref cameraFollowVelocity, cameraFollowSpeed);
        transform.position = targetPosition;
    }
}
