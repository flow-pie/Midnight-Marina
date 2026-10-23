using UnityEngine;

public class Waypoint : MonoBehaviour
{
    [Header("Waypoint Status")]
    public Waypoint previousWaypoint;
    public Waypoint nextWaypoint;

    [Range(0f, 10f)]
    public float waypointWidth = 5f;

    public Vector3 GetPosition()
    {
        Vector3 minBounds = transform.position + transform.right * waypointWidth / 2f ;
        Vector3 maxBounds = transform.position - transform.right * waypointWidth / 2f ;

        return Vector3.Lerp(minBounds, maxBounds, Random.Range(0f, 1f));
    }
}
