using UnityEditor;
using UnityEngine;
namespace Overdrive.EditorTools
{
    [CustomEditor(typeof(VehiclePerception))]
    public sealed class VehiclePerceptionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var sensor = (VehiclePerception)target;
            if (!Application.isPlaying) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Simulation state perception", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Reference valid", sensor.ReferenceValid.ToString());
            EditorGUILayout.LabelField("Observed vehicles", sensor.Observations.Count.ToString());
            EditorGUILayout.LabelField("Track s / d (m)", $"{sensor.SelfTrack.distance:F1} / {sensor.SelfTrack.lateral:F1}");
            var driver = sensor.GetComponent<AIDriver>();
            EditorGUILayout.LabelField("Following enabled", driver.FollowingActive.ToString());
            EditorGUILayout.LabelField("Behaviour state", driver.BehaviourState.ToString());
            EditorGUILayout.LabelField("Target speed (km/h)", driver.TargetSpeedKmh.ToString("F1"));
            if(driver.FollowingActive)
            {
                EditorGUILayout.LabelField("Reason", driver.FollowingPlan.reason ?? "Waiting for first decision");
                EditorGUILayout.LabelField("Bumper gap (m)", driver.FollowingPlan.bumperGap.ToString("F1"));
                EditorGUILayout.LabelField("TTC (s)", driver.FollowingPlan.ttc.ToString("F2"));
            }
            if (sensor.HasLead)
            {
                var lead = sensor.Lead;
                EditorGUILayout.ObjectField("Lead", lead.vehicle, typeof(OverdriveVehicle), true);
                EditorGUILayout.LabelField("ID", lead.id.ToString());
                EditorGUILayout.LabelField("Distance / lateral (m)", $"{lead.longitudinal:F1} / {lead.lateral:F1}");
                EditorGUILayout.LabelField("Closing speed (m/s)", lead.closingSpeed.ToString("F1"));
            }
            else EditorGUILayout.LabelField("Lead", "None");
            Repaint();
        }
    }
}
