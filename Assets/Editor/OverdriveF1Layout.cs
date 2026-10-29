using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Overdrive;
using TMPro;

public static class OverdriveF1Layout
{
    static Vector3 Spline(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
    { return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t); }
    static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
        go.transform.localPosition=position; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=material; return go;
    }
    static Vector3 Edge(List<Vector3> points,int index,float offset,float height)
    {
        int count=points.Count;
        var incoming=(points[index]-points[(index+count-1)%count]).normalized;
        var outgoing=(points[(index+1)%count]-points[index]).normalized;
        var right=Vector3.Cross(Vector3.up,(incoming+outgoing).normalized);
        float miter=Mathf.Max(.4f,Vector3.Dot(right,Vector3.Cross(Vector3.up,outgoing)));
        return points[index]+right*(offset/miter)+Vector3.up*height;
    }
    static GameObject Ribbon(string name,Transform parent,List<Vector3> points,float inner,float outer,float lower,float upper,Material one,Material two,bool wall,bool collider)
    {
        int count=points.Count, per=wall?4:2;
        var vertices=new Vector3[count*per]; var first=new List<int>(); var second=new List<int>();
        for(int i=0;i<count;i++)
        {
            vertices[i*per]=Edge(points,i,inner,lower); vertices[i*per+1]=Edge(points,i,outer,lower);
            if(wall) { vertices[i*per+2]=Edge(points,i,inner,upper); vertices[i*per+3]=Edge(points,i,outer,upper); }
            int a=i*per,b=((i+1)%count)*per;
            var tris=(i/(wall?4:1))%2==0?first:second;
            if(!wall) tris.AddRange(new[]{a,b,a+1,a+1,b,b+1});
            else
            {
                tris.AddRange(new[]{a,a+1,b,a+1,b+1,b});
                tris.AddRange(new[]{a+2,b+2,a+3,a+3,b+2,b+3});
                tris.AddRange(new[]{a,b,a+2,a+2,b,b+2});
                tris.AddRange(new[]{a+1,a+3,b+1,a+3,b+3,b+1});
            }
        }
        var mesh=new Mesh {name=name}; mesh.vertices=vertices; mesh.subMeshCount=2;
        mesh.SetTriangles(first,0); mesh.SetTriangles(second,1); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path="Assets/Meshes/OverdriveF1/"+name.Replace(' ','_')+".asset";
        AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<MeshRenderer>().sharedMaterials=new[]{one,two};
        if(collider) go.AddComponent<MeshCollider>().sharedMesh=mesh;
        return go;
    }
    static void Sign(string text,Transform parent,Vector3 position,Vector3 size,Material white,Material black)
    {
        var root=new GameObject("Sign "+text).transform; root.SetParent(parent,false); root.position=position;
        Cube("Board",root,Vector3.zero,size,white);
        var label=new GameObject("Label").AddComponent<TextMeshPro>(); label.transform.SetParent(root,false);
        label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Settings/OverdriveKorean.asset"); label.text=text; label.color=Color.black;
        label.fontSize=text.Length>3?8:13; label.alignment=TextAlignmentOptions.Center; label.rectTransform.sizeDelta=new Vector2(size.x,size.y);
        label.transform.localPosition=new Vector3(0,0,-size.z*.55f);
    }
    [MenuItem("Overdrive/Extend F1 Straight And Hairpin")]
    public static string Build()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.isDirty || scene.path!="Assets/Scenes/OverdriveF1.unity") throw new System.InvalidOperationException("Open saved F1 scene, stop Play first.");
        var track=GameObject.Find("F1 Circuit - Apex Park").transform;
        if(track.Find("Smooth Tarmac")) return "Extended layout already present; preserved.";
        if(!AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/OverdriveF1_Compact.unity")) EditorSceneManager.SaveScene(scene,"Assets/Scenes/OverdriveF1_Compact.unity",true);
        foreach(var folder in new[]{"Assets/Meshes","Assets/Meshes/OverdriveF1"})
            if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\','/'),System.IO.Path.GetFileName(folder));
        var controls=new[]{new Vector3(-180,0,0),new Vector3(-180,0,140),new Vector3(-180,0,260),new Vector3(-160,0,295),new Vector3(-125,0,305),new Vector3(-90,0,290),new Vector3(-85,0,250),new Vector3(-60,0,110),new Vector3(-115,0,90),new Vector3(-120,0,35),new Vector3(-40,0,20),new Vector3(20,0,65),new Vector3(85,0,90),new Vector3(145,0,60),new Vector3(170,0,10),new Vector3(125,0,-10),new Vector3(90,0,15),new Vector3(55,0,-20),new Vector3(120,0,-65),new Vector3(135,0,-120),new Vector3(90,0,-160),new Vector3(20,0,-150),new Vector3(-35,0,-95),new Vector3(-85,0,-110),new Vector3(-180,0,-110)};
        var points=new List<Vector3>(); int count=controls.Length;
        for(int i=0;i<count;i++)
        {
            int samples=Mathf.Max(5,Mathf.CeilToInt(Vector3.Distance(controls[i],controls[(i+1)%count])/7));
            for(int j=0;j<samples;j++) points.Add(Spline(controls[(i+count-1)%count],controls[i],controls[(i+1)%count],controls[(i+2)%count],(float)j/samples));
        }
        var asphalt=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Overdrive/F1_Asphalt.mat");
        var white=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Overdrive/F1_White.mat");
        var red=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Overdrive/F1_Red.mat");
        var black=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Overdrive/F1_Carbon.mat");
        Ribbon("Smooth Tarmac",track,points,-9,9,0,0,asphalt,asphalt,false,true);
        Ribbon("Left Kerb",track,points,-8.7f,-7.9f,.025f,.025f,red,white,false,false);
        Ribbon("Right Kerb",track,points,7.9f,8.7f,.025f,.025f,red,white,false,false);
        Ribbon("Left Wall",track,points,-11.4f,-10.8f,0,1.3f,red,white,true,true);
        Ribbon("Right Wall",track,points,10.8f,11.4f,0,1.3f,white,red,true,true);
        var remove=new List<GameObject>();
        foreach(Transform child in track)
            if(child.name.StartsWith("Tarmac ") || child.name.StartsWith("Kerb ") || child.name.StartsWith("Barrier ") || child.name.StartsWith("F1 CP ") || child.name=="Finish") remove.Add(child.gameObject);
        var checkpoints=new Transform[points.Count]; float total=0;
        for(int i=0;i<points.Count;i++)
        {
            var p=points[i]; var q=Quaternion.LookRotation(points[(i+1)%points.Count]-p); total+=Vector3.Distance(p,points[(i+1)%points.Count]);
            var cp=new GameObject("F1 CP "+i); cp.transform.SetParent(track,false); cp.transform.SetPositionAndRotation(p,q);
            var gate=cp.AddComponent<BoxCollider>(); gate.isTrigger=true; gate.center=Vector3.up*2; gate.size=new Vector3(20,4,1);
            cp.AddComponent<CircuitCheckpoint>().index=i; checkpoints[i]=cp.transform;
        }
        var race=Object.FindFirstObjectByType<RaceManager>(); race.ai.path.points=checkpoints;
        race.player.GetComponent<CircuitProgress>().checkpoints=checkpoints; race.opponent.GetComponent<CircuitProgress>().checkpoints=checkpoints;
        foreach(var go in remove) Object.DestroyImmediate(go);
        var start=checkpoints[0];
        for(int x=0;x<18;x++) for(int z=0;z<2;z++) Cube("Finish",track,start.position+new Vector3(x-8.5f,.02f,z-.5f),new Vector3(1,.04f,1),(x+z)%2==0?white:black);
        race.player.transform.SetPositionAndRotation(start.position+start.forward*3-start.right*2.3f+Vector3.up*.65f,start.rotation);
        race.opponent.transform.SetPositionAndRotation(start.position+start.forward*3+start.right*2.3f+Vector3.up*.65f,start.rotation);
        var terrain=GameObject.Find("Terrain"); terrain.transform.position=new Vector3(0,-.6f,50); terrain.transform.localScale=new Vector3(750,1,850);
        var bridge=track.Find("Start bridge"); bridge.SetPositionAndRotation(start.position+start.forward*12+Vector3.up*7,start.rotation);
        bridge.Find("Circuit title").localRotation=Quaternion.identity;
        Sign("150",track,new Vector3(-166,1.5f,135),new Vector3(2,2,.2f),white,black);
        Sign("100",track,new Vector3(-166,1.5f,185),new Vector3(2,2,.2f),white,black);
        Sign("50",track,new Vector3(-166,1.5f,235),new Vector3(2,2,.2f),white,black);
        Sign("T1 / HAIRPIN",track,new Vector3(-180,4,277),new Vector3(13,2,.25f),white,black);
        Object.FindFirstObjectByType<RaceUI>().menu.transform.Find("Subtitle").GetComponent<TMP_Text>().text="FORMULA / APEX PARK GP  ·  2 LAPS";
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return $"Extended Apex Park GP: {total:F0}m/lap, {points.Count} checkpoints; long main straight, northern hairpin, braking markers and continuous road/walls. Compact scene preserved.";
    }
}
