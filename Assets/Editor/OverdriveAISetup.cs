using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Overdrive;

public static class OverdriveAISetup
{
    public static string RestoreDefaults()
    {
        if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        var ai=Object.FindFirstObjectByType<AIDriver>();
        ai.difficulty=AssetDatabase.LoadAssetAtPath<AIDifficulty>("Assets/Settings/AI_Normal.asset");
        Object.FindFirstObjectByType<PlayerVehicleInput>(FindObjectsInactive.Include).gameObject.SetActive(true);
        var scene=EditorSceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Saved active player and NORMAL default.";
    }
    [MenuItem("Overdrive/Configure P2 AI")]
    public static string Configure()
    {
        if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/OverdriveCircuit.unity" || scene.isDirty)
            throw new System.InvalidOperationException("Open the saved OverdriveCircuit scene first.");
        if(Object.FindFirstObjectByType<AIDriver>()) return "AI already configured; existing scene preserved.";
        var player=Object.FindFirstObjectByType<PlayerVehicleInput>();
        if(!player) throw new System.InvalidOperationException("Player not found.");
        var progress=player.GetComponent<CircuitProgress>();
        var normal=Difficulty("AI_Normal",65,30,8,40);
        var hard=Difficulty("AI_Hard",80,40,11,55);
        var path=new GameObject("AI centerline").AddComponent<WaypointPath>(); path.points=progress.checkpoints;
        var car=Object.Instantiate(player.gameObject); car.name="Overdrive AI";
        Object.DestroyImmediate(car.GetComponent<PlayerVehicleInput>());
        var ai=car.AddComponent<AIDriver>(); ai.difficulty=normal;
        var material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Overdrive/Body.mat")); material.color=new Color(1,.65f,.05f);
        AssetDatabase.CreateAsset(material,"Assets/Materials/Overdrive/AI_Body.mat");
        car.transform.Find("Sports body").GetComponent<Renderer>().sharedMaterial=material;
        // Scene references are assigned after saving the reusable unbound vehicle template.
        car.GetComponent<CircuitProgress>().checkpoints=null;
        PrefabUtility.SaveAsPrefabAsset(car,"Assets/Prefabs/OverdriveAI.prefab");
        car.GetComponent<CircuitProgress>().checkpoints=progress.checkpoints; ai.path=path;
        var start=progress.checkpoints[0]; car.transform.SetPositionAndRotation(start.position+start.forward*3+start.right*3.5f+Vector3.up*.85f,start.rotation);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return "AI configured: NORMAL/HARD assets, shared path, physical AI vehicle, lateral grid spacing 3.5m. Solo test must disable player during Play Mode.";
    }
    static AIDifficulty Difficulty(string name,float speed,float corner,float response,float preview)
    {
        string file="Assets/Settings/"+name+".asset";
        var config=AssetDatabase.LoadAssetAtPath<AIDifficulty>(file);
        if(config) return config;
        config=ScriptableObject.CreateInstance<AIDifficulty>(); config.maximumSpeedKmh=speed; config.cornerSpeedKmh=corner;
        config.steeringResponse=response; config.brakingLookahead=preview;
        AssetDatabase.CreateAsset(config,file); return config;
    }
}
