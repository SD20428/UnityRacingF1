using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Overdrive;
using TMPro;

public static class OverdriveF1Setup
{
    public static string MoveStartToStraight()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.isDirty || scene.path!="Assets/Scenes/OverdriveF1.unity") throw new System.InvalidOperationException("Open saved F1 scene and stop Play.");
        var race=Object.FindFirstObjectByType<RaceManager>(); var path=race.ai.path; var old=path.points;
        int first=0; for(int i=1;i<old.Length;i++) if((old[i].position-new Vector3(-180,0,0)).sqrMagnitude<(old[first].position-new Vector3(-180,0,0)).sqrMagnitude) first=i;
        var ordered=new Transform[old.Length]; for(int i=0;i<old.Length;i++) ordered[i]=old[(first+i)%old.Length];
        var oldPosition=old[0].position; var oldRotation=old[0].rotation; var start=ordered[0];
        var track=GameObject.Find("F1 Circuit - Apex Park").transform;
        foreach(Transform child in track) if(child.name=="Finish")
        { var local=Quaternion.Inverse(oldRotation)*(child.position-oldPosition); child.SetPositionAndRotation(start.position+start.rotation*local,start.rotation); }
        var banner=track.Find("Start bridge"); banner.SetPositionAndRotation(start.position+start.forward*12+Vector3.up*7,start.rotation);
        for(int i=0;i<ordered.Length;i++) { ordered[i].GetComponent<CircuitCheckpoint>().index=i; ordered[i].name="F1 CP "+i; }
        path.points=ordered; race.player.GetComponent<CircuitProgress>().checkpoints=ordered; race.opponent.GetComponent<CircuitProgress>().checkpoints=ordered;
        race.player.transform.SetPositionAndRotation(start.position+start.forward*3-start.right*2.3f+Vector3.up*.65f,start.rotation);
        race.opponent.transform.SetPositionAndRotation(start.position+start.forward*3+start.right*2.3f+Vector3.up*.65f,start.rotation);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); return "Start and finish moved to main straight; ordered checkpoint references preserved.";
    }
    static Material asphalt, kerbRed, white, grass, black, blue, orange;
    static Material Material(string name,Color color)
    {
        string file="Assets/Materials/Overdrive/F1_"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(file);
        if(!material) { material=new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial); AssetDatabase.CreateAsset(material,file); }
        material.color=color; return material;
    }
    static GameObject Box(string name,Transform parent,Vector3 local,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
        go.transform.localPosition=local; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=material; return go;
    }
    static void Visual(string name,Transform parent,Vector3 local,Vector3 size,Material material)
    { var go=Box(name,parent,local,size,material); Object.DestroyImmediate(go.GetComponent<Collider>()); }
    static void Setting(Object target,string name,float value)
    { var so=new SerializedObject(target); so.FindProperty(name).floatValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static Vector3 Spline(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
    { return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t); }
    static AIDifficulty Difficulty(string name,float maximum,float lateral)
    {
        string file="Assets/Settings/"+name+".asset"; var asset=AssetDatabase.LoadAssetAtPath<AIDifficulty>(file);
        if(!asset) { asset=ScriptableObject.CreateInstance<AIDifficulty>(); AssetDatabase.CreateAsset(asset,file); }
        asset.maximumSpeedKmh=maximum; asset.cornerSpeedKmh=65; asset.lateralAcceleration=lateral;
        asset.lookaheadBase=12; asset.lookaheadPerKmh=.12f; asset.brakingLookahead=85; asset.steeringResponse=9;
        EditorUtility.SetDirty(asset); return asset;
    }
    static void Car(OverdriveVehicle car,Material color)
    {
        foreach(string child in new[]{"Sports body","Cabin"})
        { var found=car.transform.Find(child); if(found) Object.DestroyImmediate(found.gameObject); }
        var hull=car.GetComponent<BoxCollider>(); hull.center=new Vector3(0,.12f,0); hull.size=new Vector3(1.45f,.55f,4.6f);
        Visual("Monocoque",car.transform,new Vector3(0,.1f,-.45f),new Vector3(1.12f,.6f,2.5f),color);
        Visual("Nose",car.transform,new Vector3(0,-.02f,1.35f),new Vector3(.48f,.32f,2.2f),color);
        Visual("Cockpit",car.transform,new Vector3(0,.48f,-.45f),new Vector3(.72f,.2f,1),black);
        Visual("Helmet",car.transform,new Vector3(0,.62f,-.55f),new Vector3(.34f,.3f,.4f),white);
        Visual("Front wing",car.transform,new Vector3(0,-.17f,2.45f),new Vector3(2.6f,.1f,.6f),black);
        Visual("Rear wing",car.transform,new Vector3(0,.87f,-2.22f),new Vector3(2.3f,.12f,.6f),color);
        foreach(int side in new[]{-1,1})
        {
            Visual("Sidepod",car.transform,new Vector3(side*.78f,.08f,-.6f),new Vector3(.44f,.47f,1.8f),color);
            Visual("Wing support",car.transform,new Vector3(side*.6f,.43f,-2.22f),new Vector3(.09f,.8f,.12f),black);
            Visual("Front endplate",car.transform,new Vector3(side*1.27f,-.05f,2.45f),new Vector3(.08f,.35f,.65f),color);
            Visual("Rear endplate",car.transform,new Vector3(side*1.12f,.83f,-2.22f),new Vector3(.08f,.55f,.65f),black);
        }
        var body=car.GetComponent<Rigidbody>(); body.mass=800;
        Setting(car,"driveTorque",3400); Setting(car,"brakeTorque",6500); Setting(car,"steeringAngle",26);
        Setting(car,"maximumForwardKmh",320); Setting(car,"maximumReverseKmh",20); Setting(car,"highSpeedSteering",7);
        Setting(car,"steeringFadeKmh",260); Setting(car,"downforce",2.2f); Setting(car,"aeroDrag",.3f); Setting(car,"gearStepKmh",35);
        var serialized=new SerializedObject(car); serialized.FindProperty("gearCount").intValue=8; serialized.ApplyModifiedPropertiesWithoutUndo();
        var player=car.GetComponent<PlayerVehicleInput>(); if(player) Setting(player,"steeringResponse",5);
        var ai=car.GetComponent<AIDriver>(); if(ai) Setting(ai,"wheelbase",3.3f);
        for(int i=0;i<car.wheels.Length;i++)
        {
            var w=car.wheels[i]; w.transform.localPosition=new Vector3(i%2==0?-1.05f:1.05f,-.05f,i<2?1.65f:-1.65f);
            w.suspensionDistance=.12f; var spring=w.suspensionSpring; spring.spring=60000; spring.damper=6500; w.suspensionSpring=spring;
            var side=w.sidewaysFriction; side.stiffness=1.8f; w.sidewaysFriction=side;
            var forward=w.forwardFriction; forward.stiffness=1.5f; w.forwardFriction=forward;
            var tyre=car.visuals[i].GetChild(0); tyre.localScale=new Vector3(.72f,i<2?.18f:.22f,.72f);
        }
    }
    [MenuItem("Overdrive/Build F1 Style Scene")]
    public static string Build()
    {
        var scene=EditorSceneManager.GetActiveScene(); const string output="Assets/Scenes/OverdriveF1.unity";
        if(EditorApplication.isPlaying || scene.isDirty || scene.path!="Assets/Scenes/OverdriveCircuit.unity") throw new System.InvalidOperationException("Open saved P3 circuit, stop Play first.");
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(output)) throw new System.InvalidOperationException("F1 scene already exists; preserve it and edit incrementally.");
        EditorSceneManager.SaveScene(scene,output,true); scene=EditorSceneManager.OpenScene(output,OpenSceneMode.Single);
        var race=Object.FindFirstObjectByType<RaceManager>(); var ai=race.ai;
        Object.DestroyImmediate(GameObject.Find("Circuit - clockwise"));
        asphalt=Material("Asphalt",new Color(.07f,.08f,.10f)); kerbRed=Material("Red",new Color(.88f,.035f,.04f)); white=Material("White",new Color(.93f,.94f,.92f));
        grass=Material("Runoff",new Color(.1f,.22f,.17f)); black=Material("Carbon",new Color(.025f,.03f,.035f));
        blue=Material("Player",new Color(.0f,.58f,.85f)); orange=Material("AI",new Color(1,.32f,.025f));
        var terrain=GameObject.Find("Terrain"); terrain.transform.localScale=new Vector3(650,1,600); terrain.GetComponent<Renderer>().sharedMaterial=grass;
        var controls=new[]{new Vector3(-180,0,-110),new Vector3(-180,0,0),new Vector3(-180,0,120),new Vector3(-150,0,155),new Vector3(-85,0,150),new Vector3(-60,0,110),new Vector3(-115,0,90),new Vector3(-120,0,35),new Vector3(-40,0,20),new Vector3(20,0,65),new Vector3(85,0,90),new Vector3(145,0,60),new Vector3(170,0,10),new Vector3(125,0,-10),new Vector3(90,0,15),new Vector3(55,0,-20),new Vector3(120,0,-65),new Vector3(135,0,-120),new Vector3(90,0,-160),new Vector3(20,0,-150),new Vector3(-35,0,-95),new Vector3(-85,0,-110)};
        var points=new List<Vector3>(); int count=controls.Length;
        for(int i=0;i<count;i++)
        {
            int samples=Mathf.Max(4,Mathf.CeilToInt(Vector3.Distance(controls[i],controls[(i+1)%count])/9));
            for(int j=0;j<samples;j++) points.Add(Spline(controls[(i+count-1)%count],controls[i],controls[(i+1)%count],controls[(i+2)%count],(float)j/samples));
        }
        int straightStart=points.FindIndex(p=>(p-controls[1]).sqrMagnitude<.01f);
        var rotated=new List<Vector3>(); for(int i=0;i<points.Count;i++) rotated.Add(points[(straightStart+i)%points.Count]); points=rotated;
        var track=new GameObject("F1 Circuit - Apex Park"); var checkpoints=new Transform[points.Count]; float total=0;
        for(int i=0;i<points.Count;i++)
        {
            var a=points[i]; var b=points[(i+1)%points.Count]; var q=Quaternion.LookRotation(b-a); float length=Vector3.Distance(a,b); total+=length;
            var segment=Box("Tarmac "+i,track.transform,(a+b)*.5f-Vector3.up*.1f,new Vector3(18,.2f,length+1),asphalt); segment.transform.rotation=q;
            foreach(int side in new[]{-1,1})
            {
                var kerb=Box("Kerb "+i,track.transform,(a+b)*.5f+q*new Vector3(side*8.3f,.03f,0),new Vector3(.8f,.06f,length+.2f),i%2==0?kerbRed:white); kerb.transform.rotation=q;
                var barrier=Box("Barrier "+i,track.transform,(a+b)*.5f+q*new Vector3(side*11.1f,.65f,0),new Vector3(.6f,1.3f,length+.8f),i%8<4?white:kerbRed); barrier.transform.rotation=q;
            }
            var cp=new GameObject("F1 CP "+i); cp.transform.SetParent(track.transform); cp.transform.SetPositionAndRotation(a,q);
            var trigger=cp.AddComponent<BoxCollider>(); trigger.isTrigger=true; trigger.center=Vector3.up*2; trigger.size=new Vector3(20,4,1);
            cp.AddComponent<CircuitCheckpoint>().index=i; checkpoints[i]=cp.transform;
        }
        // No finish-line paint: user preference. Checkpoint zero still times laps.
        var path=Object.FindFirstObjectByType<WaypointPath>(); path.points=checkpoints;
        race.player.GetComponent<CircuitProgress>().checkpoints=checkpoints; race.opponent.GetComponent<CircuitProgress>().checkpoints=checkpoints;
        race.normal=Difficulty("F1_Normal",185,14); race.hard=Difficulty("F1_Hard",220,18); ai.difficulty=race.normal;
        Object.FindFirstObjectByType<RaceUI>().menu.transform.Find("Subtitle").GetComponent<TMP_Text>().text="FORMULA / APEX PARK  ·  2 LAPS";
        Car(race.player,blue); Car(race.opponent,orange);
        var start=checkpoints[0];
        race.player.transform.SetPositionAndRotation(start.position+start.forward*4-start.right*2.3f+Vector3.up*.65f,start.rotation);
        race.opponent.transform.SetPositionAndRotation(start.position+start.forward*4+start.right*2.3f+Vector3.up*.65f,start.rotation);
        var camera=Camera.main; camera.fieldOfView=72; camera.transform.position=race.player.transform.position-race.player.transform.forward*10+Vector3.up*4; camera.transform.LookAt(race.player.transform.position+Vector3.up);
        // Pit buildings and grandstands are trackside scenery; no pit-stop mechanic is implied.
        for(int i=0;i<8;i++)
        {
            Box("Pit garage",track.transform,new Vector3(-215,3,-80+i*22),new Vector3(20,6,18),black);
            Box("Garage trim",track.transform,new Vector3(-204.8f,4,-80+i*22),new Vector3(.2f,1.2f,17),i%2==0?blue:orange);
            Box("Grandstand",track.transform,new Vector3(-142,2+i%2,-80+i*22),new Vector3(12,4+i%2*2,16),white);
        }
        var banner=Box("Start bridge",track.transform,start.position+start.forward*12+Vector3.up*7,new Vector3(23,2,.7f),black); banner.transform.rotation=start.rotation;
        var title=new GameObject("Circuit title").AddComponent<TextMeshPro>(); title.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Settings/OverdriveKorean.asset"); title.text="OVERDRIVE  /  APEX PARK"; title.fontSize=5; title.alignment=TextAlignmentOptions.Center; title.rectTransform.sizeDelta=new Vector2(22,2); title.transform.SetParent(banner.transform,false); title.transform.localScale=new Vector3(1/23f,.5f,1/.7f); title.transform.localPosition=new Vector3(0,0,-.52f); title.transform.localRotation=Quaternion.Euler(0,180,0);
        PrefabUtility.SaveAsPrefabAsset(race.player.gameObject,"Assets/Prefabs/OverdriveF1Player.prefab"); PrefabUtility.SaveAsPrefabAsset(race.opponent.gameObject,"Assets/Prefabs/OverdriveF1AI.prefab");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(output,true),new EditorBuildSettingsScene("Assets/Scenes/OverdriveCircuit.unity",false),new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity",false)};
        return $"F1-style Apex Park: {total:F0} m/lap, {points.Count} ordered checkpoints. New scene saved; original P3 circuit preserved.";
    }
}
