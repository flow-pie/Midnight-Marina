using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class WaypointEditor
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
    public static void DrawWaypointGizmos(Waypoint waypoint, GizmoType gizmoType)
    {
        //Set sphere color based on selection state
        if ((gizmoType & GizmoType.Selected) != 0)
        {
            Gizmos.color = Color.blue;
        }
        else
        {
            Gizmos.color = Color.blue * 0.5f; // Dimmed blue when unselected
        }

        // Draw the core waypoint center
        Gizmos.DrawSphere(waypoint.transform.position, 0.5f);

        // Draw the waypoint width line (Left to Right)
        Gizmos.color = Color.white;
        Vector3 widthOffset = waypoint.transform.right * (waypoint.waypointWidth / 2f);
        Gizmos.DrawLine(waypoint.transform.position + widthOffset, waypoint.transform.position - widthOffset);

        if (waypoint.previousWaypoint != null)
        {
            Gizmos.color = Color.red;
            
            Vector3 offsetFrom = waypoint.transform.right * (waypoint.waypointWidth / 2f);
            Vector3 offsetTo = waypoint.previousWaypoint.transform.right * (waypoint.previousWaypoint.waypointWidth / 2f);

            Gizmos.DrawLine(waypoint.transform.position + offsetFrom, waypoint.previousWaypoint.transform.position + offsetTo);
        }

        if (waypoint.nextWaypoint != null)
        {
            Gizmos.color = Color.green;
            
            Vector3 offsetFrom = waypoint.transform.right * (-waypoint.waypointWidth / 2f);
            Vector3 offsetTo = waypoint.nextWaypoint.transform.right * (-waypoint.nextWaypoint.waypointWidth / 2f);

            Gizmos.DrawLine(waypoint.transform.position + offsetFrom, waypoint.nextWaypoint.transform.position + offsetTo);
        }
    }

}
