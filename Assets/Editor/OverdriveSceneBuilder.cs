using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Overdrive;

public static class OverdriveSceneBuilder
{
    static Material road, grass, rail, white, blue, dark;
    static Material Mat(string name, Color color)
    {
        string path = "Assets/Materials/Overdrive/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial); AssetDatabase.CreateAsset(m, path); }
        m.color = color; return m;
    }
    static GameObject Box(string name, Vector3 p, Vector3 size, Material material, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.position = p; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
    [MenuItem("Overdrive/Build P1 Circuit")]
    public static string Build()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) throw new System.InvalidOperationException("Save current scene before building circuit.");
        foreach (var folder in new[] { "Assets/Materials", "Assets/Materials/Overdrive", "Assets/Prefabs" })
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\','/'), System.IO.Path.GetFileName(folder));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        road = Mat("Asphalt", new Color(.08f,.10f,.14f)); grass = Mat("Infield", new Color(.12f,.22f,.18f));
        rail = Mat("Barrier", new Color(.95f,.23f,.07f)); white = Mat("Markings", Color.white);
        blue = Mat("Body", new Color(.02f,.5f,.95f)); dark = Mat("Tyre", new Color(.025f,.03f,.04f));
        Box("Terrain", new Vector3(0,-.6f,0), new Vector3(500,1,430), grass);
        var track = new GameObject("Circuit - clockwise");
        var points = new List<Vector3>();
        // Smooth closed centerline with a tighter top-right bend and a flowing lower section.
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2 / 48;
            points.Add(new Vector3(145 * Mathf.Cos(a) + 18 * Mathf.Cos(2*a), 0, 105 * Mathf.Sin(a) + 16*Mathf.Sin(3*a)));
        }
        var checks = new Transform[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i], next = points[(i+1)%points.Count], d = next-p;
            var q = Quaternion.LookRotation(d); float length = d.magnitude;
            var segment = Box("Road " + i, (p+next)/2 - Vector3.up*.1f, new Vector3(17,.2f,length+1.2f), road, track.transform); segment.transform.rotation=q;
            foreach (int side in new[] {-1,1})
            {
                var b = Box("Barrier " + i, (p+next)/2 + q*new Vector3(side*9.4f,.65f,0), new Vector3(.65f,1.3f,length+1f), rail, track.transform); b.transform.rotation=q;
                var edge = Box("Edge " + i, (p+next)/2 + q*new Vector3(side*7.7f,.015f,0), new Vector3(.2f,.03f,length), white, track.transform); edge.transform.rotation=q;
            }
            var cp = new GameObject("Checkpoint " + i); cp.transform.SetParent(track.transform); cp.transform.SetPositionAndRotation(p, q);
            var trigger = cp.AddComponent<BoxCollider>(); trigger.isTrigger=true; trigger.center=Vector3.up*2; trigger.size=new Vector3(18,4,1);
            cp.AddComponent<CircuitCheckpoint>().index=i; checks[i]=cp.transform;
        }
        for (int x=0;x<16;x++) for(int z=0;z<2;z++)
        {
            var stripe=Box("Finish", points[0]+checks[0].rotation*new Vector3(x-7.5f,.025f,z-.5f), new Vector3(1,.04f,1), (x+z)%2==0?white:dark,track.transform); stripe.transform.rotation=checks[0].rotation;
        }
        var car = new GameObject("Overdrive Player"); car.transform.position=new Vector3(0,.85f,0);
        var body=car.AddComponent<Rigidbody>(); body.mass=1250; body.interpolation=RigidbodyInterpolation.Interpolate; body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        var hull=car.AddComponent<BoxCollider>(); hull.center=new Vector3(0,.15f,0); hull.size=new Vector3(1.8f,.75f,4);
        var shell=Box("Sports body",car.transform.position+Vector3.up*.15f,new Vector3(1.8f,.65f,4),blue,car.transform); Object.DestroyImmediate(shell.GetComponent<Collider>());
        var cabin=Box("Cabin",car.transform.position+new Vector3(0,.72f,-.25f),new Vector3(1.4f,.55f,1.8f),dark,car.transform); Object.DestroyImmediate(cabin.GetComponent<Collider>());
        var vehicle=car.AddComponent<OverdriveVehicle>(); vehicle.wheels=new WheelCollider[4]; vehicle.visuals=new Transform[4];
        for(int i=0;i<4;i++)
        {
            var w=new GameObject("Wheel physics " + i); w.transform.SetParent(car.transform,false); w.transform.localPosition=new Vector3(i%2==0?-.92f:.92f,-.15f,i<2?1.35f:-1.35f);
            var wc=w.AddComponent<WheelCollider>(); wc.radius=.36f; wc.mass=25; wc.suspensionDistance=.22f; wc.wheelDampingRate=1;
            var spring=wc.suspensionSpring; spring.spring=35000; spring.damper=4500; spring.targetPosition=.5f; wc.suspensionSpring=spring;
            var f=wc.sidewaysFriction; f.stiffness=1.5f; wc.sidewaysFriction=f;
            vehicle.wheels[i]=wc;
            var pivot=new GameObject("Wheel visual " + i); pivot.transform.SetParent(car.transform,false);
            var mesh=GameObject.CreatePrimitive(PrimitiveType.Cylinder); mesh.name="Tyre"; mesh.transform.SetParent(pivot.transform,false); mesh.transform.localRotation=Quaternion.Euler(0,0,90); mesh.transform.localScale=new Vector3(.72f,.16f,.72f); Object.DestroyImmediate(mesh.GetComponent<Collider>()); mesh.GetComponent<Renderer>().sharedMaterial=dark;
            vehicle.visuals[i]=pivot.transform;
        }
        var progress=car.AddComponent<CircuitProgress>(); car.AddComponent<PlayerVehicleInput>();
        PrefabUtility.SaveAsPrefabAsset(car,"Assets/Prefabs/OverdrivePlayer.prefab");
        progress.checkpoints=checks;
        car.transform.SetPositionAndRotation(points[0]+checks[0].forward*3+Vector3.up*.85f,checks[0].rotation);
        var light=new GameObject("Sun").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.5f; light.shadows=LightShadows.Soft; light.transform.rotation=Quaternion.Euler(45,-35,0);
        RenderSettings.ambientLight=new Color(.5f,.55f,.65f);
        var cam=new GameObject("Follow Camera").AddComponent<Camera>(); cam.tag="MainCamera"; cam.fieldOfView=65; cam.farClipPlane=800; cam.gameObject.AddComponent<AudioListener>(); cam.gameObject.AddComponent<VehicleFollowCamera>().target=car.transform;
        cam.transform.position=car.transform.position-car.transform.forward*9+Vector3.up*4; cam.transform.LookAt(car.transform.position+Vector3.up);
        const string scenePath="Assets/Scenes/OverdriveCircuit.unity";
        EditorSceneManager.SaveScene(scene,scenePath);
        EditorBuildSettings.scenes=new[] { new EditorBuildSettingsScene(scenePath,true),new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity",false) };
        AssetDatabase.SaveAssets();
        return "Built " + scenePath + "; 48 ordered checkpoints, RWD player, follow camera. W/S/A/D drive; R recover. Practice laps reported to Console.";
    }
}
