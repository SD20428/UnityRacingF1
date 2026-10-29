using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Overdrive;

public static class Overdrive2025Setup
{
    public static string Apply()
    {
        if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play before applying settings.");
        if(EditorSceneManager.GetActiveScene().path!="Assets/Scenes/OverdriveF1.unity") throw new System.InvalidOperationException("Open the current F1 scene; preserved prototype scenes must stay unchanged.");
        foreach(var car in Object.FindObjectsByType<OverdriveVehicle>(FindObjectsSortMode.None))
        {
            var s=new SerializedObject(car);
            foreach(string f in new[]{"advancedPhysics","usePowertrain","reference2025"}) s.FindProperty(f).boolValue=true;
            s.FindProperty("tractionAssist").boolValue=false; s.FindProperty("absAssist").boolValue=false;
            s.FindProperty("manualTransmission").boolValue=false;
            s.FindProperty("centerOfMass").vector3Value=new Vector3(0,-.20f,-.18f);
            s.FindProperty("engineTorque").floatValue=620;
            s.FindProperty("icePowerKW").floatValue=560; s.FindProperty("hybridPowerKW").floatValue=120;
            s.FindProperty("startingFuelKg").floatValue=10;
            s.FindProperty("redlineRPM").floatValue=15000; s.FindProperty("shiftSeconds").floatValue=.06f;
            s.FindProperty("liftArea").floatValue=6; s.FindProperty("dragArea").floatValue=1.2f;
            s.FindProperty("brakeTorque").floatValue=5200;
            s.ApplyModifiedPropertiesWithoutUndo();
            for(int i=0;i<car.wheels.Length;i++)
            {
                var w=car.wheels[i]; w.wheelDampingRate=.05f;
                var f=w.forwardFriction; f.extremumSlip=.12f; f.extremumValue=1.7f; f.asymptoteSlip=.45f; f.asymptoteValue=1.35f; f.stiffness=1.3f; w.forwardFriction=f;
                var side=w.sidewaysFriction; side.extremumSlip=i<2?.08f:.1f; side.extremumValue=1; side.asymptoteSlip=i<2?.25f:.28f; side.asymptoteValue=.8f; side.stiffness=1.8f; w.sidewaysFriction=side;
            }
            EditorUtility.SetDirty(car);
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        return "2025 approximate baseline: 800kg dry +10kg fuel, 560kW ICE +120kW MGU-K, no ABS/TC.";
    }
}
