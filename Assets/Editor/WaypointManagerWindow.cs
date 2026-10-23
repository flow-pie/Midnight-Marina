using UnityEngine;
using UnityEditor;

public class WaypointManagerWindow : EditorWindow
{
    public Transform waypointParent;

    [MenuItem("Waypoint/Waypoints Editor Tools")]

    public static void ShowWindow()
    {
        GetWindow<WaypointManagerWindow>("Waypoints Editor Tools");
    }

    void OnGUI()
    {
        SerializedObject serializedObject = new SerializedObject(this);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("waypointParent"));

        if(waypointParent == null)
        {
            EditorGUILayout.HelpBox("Please assign a Waypoint Parent Transform.", MessageType.Warning);
        }else
        {
            EditorGUILayout.BeginVertical("box");
            CreateWaypointButton();
            EditorGUILayout.EndVertical();
        }
        
        serializedObject.ApplyModifiedProperties();
    }

    void CreateWaypointButton()
    {
        if (GUILayout.Button("Create Waypoint"))
        {
           //create waypoint
           GameObject newWaypoint = new GameObject("Waypoint " + waypointParent.childCount, typeof(Waypoint));

           newWaypoint.transform.SetParent(waypointParent, false);

           Waypoint waypointComponent = newWaypoint.GetComponent<Waypoint>();

           if(waypointParent.childCount > 1)
           {
               waypointComponent.previousWaypoint = waypointParent.GetChild(waypointParent.childCount - 2).GetComponent<Waypoint>();
               waypointComponent.previousWaypoint.nextWaypoint = waypointComponent;

               waypointComponent.transform.position = waypointComponent.previousWaypoint.transform.position;
               waypointComponent.transform.forward = waypointComponent.previousWaypoint.transform.forward;
           }

           Selection.activeGameObject = newWaypoint;
        }
        
    }
}
