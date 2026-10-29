using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using Overdrive;

// Expand the repaired centreline, preserving car dimensions and cross-section detail.
public static class OverdriveGrandPrixExpansion
{
    const float LengthScale=3,WidthScale=1.15f;
    const string Folder="Assets/Meshes/OverdriveGrandPrix";
    static Vector3 Scale(Vector3 p)=>new Vector3(p.x*LengthScale,p.y,p.z*LengthScale);
    static int Find(int[] parent,int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
    public static string FixBatchedFacilities()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play.");
        var root=GameObject.Find("Detailed Circuit Facilities");if(!root)return "No facility batch.";
        if(root.transform.Find("Expanded Batches Applied"))return "Batches already adjusted.";
        var rv=GameObject.Find("Refined Circuit").transform.Find("Asphalt Surface").GetComponent<MeshFilter>().sharedMesh.vertices;
        var centers=new List<Vector3>();for(int i=0;i<rv.Length/5;i+=24){var c=(rv[i*5]+rv[i*5+4])*.5f;c.x/=3;c.z/=3;centers.Add(c);}centers.Add(centers[0]);var array=centers.ToArray();int changed=0;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if(!filter.sharedMesh.name.StartsWith("Environment "))continue;
            var mesh=Object.Instantiate(filter.sharedMesh);var v=mesh.vertices;int[] parent=new int[v.Length];var weld=new Dictionary<Vector3Int,int>();
            for(int i=0;i<v.Length;i++){parent[i]=i;var key=new Vector3Int(Mathf.RoundToInt(v[i].x*1000),Mathf.RoundToInt(v[i].y*1000),Mathf.RoundToInt(v[i].z*1000));if(weld.TryGetValue(key,out int other))parent[Find(parent,i)]=Find(parent,other);else weld[key]=i;}
            var tris=mesh.triangles;for(int i=0;i<tris.Length;i+=3){parent[Find(parent,tris[i+1])]=Find(parent,tris[i]);parent[Find(parent,tris[i+2])]=Find(parent,tris[i]);}
            var sums=new Dictionary<int,Vector3>();var counts=new Dictionary<int,int>();
            for(int i=0;i<v.Length;i++){int id=Find(parent,i);if(!sums.ContainsKey(id)){sums[id]=Vector3.zero;counts[id]=0;}sums[id]+=v[i];counts[id]++;}
            var shifts=new Dictionary<int,Vector3>();foreach(var pair in sums){var c=pair.Value/counts[pair.Key];shifts[pair.Key]=Relocate(c,array)-c;}
            for(int i=0;i<v.Length;i++)v[i]+=shifts[Find(parent,i)];mesh.vertices=v;mesh.RecalculateBounds();mesh.name=filter.sharedMesh.name+" Expanded";
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+mesh.name+".asset"));filter.sharedMesh=mesh;filter.transform.localPosition=Vector3.zero;changed++;
        }
        new GameObject("Expanded Batches Applied").transform.SetParent(root.transform,false);EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();return "Relocated "+changed+" static facility batches by connected pieces, preserving object dimensions.";
    }
    public static string FixBrakingBoards()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play.");
        var group=GameObject.Find("F1 Circuit - Apex Park").transform;
        if(group.Find("Expanded Brake Boards Applied"))return "Boards already adjusted.";
        var path=Object.FindFirstObjectByType<RaceManager>().ai.path.points;
        foreach(int metres in new[]{150,100,50})
        {
            var board=group.Find("Sign "+metres);if(!board)continue;
            int nearest=0;float minimum=float.PositiveInfinity;
            for(int i=0;i<path.Length;i++){var delta=board.position-path[i].position;delta.y=0;if(delta.sqrMagnitude<minimum){minimum=delta.sqrMagnitude;nearest=i;}}
            var offset=board.position-path[nearest].position;float lateral=Vector3.Dot(offset,path[nearest].right);float height=offset.y;
            float advance=metres*(LengthScale-1);int index=nearest;
            while(advance>Vector3.Distance(path[index].position,path[(index+1)%path.Length].position)){advance-=Vector3.Distance(path[index].position,path[(index+1)%path.Length].position);index=(index+1)%path.Length;}
            var forward=(path[(index+1)%path.Length].position-path[index].position).normalized;var right=Vector3.Cross(Vector3.up,forward).normalized;
            board.position=path[index].position+forward*advance+right*lateral+Vector3.up*height;
            board.rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Inverse(path[nearest].rotation)*board.rotation;
        }
        new GameObject("Expanded Brake Boards Applied").transform.SetParent(group,false);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());return "Moved braking boards forward by scaling excess (300/200/100m), preserving nominal approach distances.";
    }
    static Vector3 Relocate(Vector3 p,Vector3[] centers)
    {
        float best=float.PositiveInfinity;Vector3 projected=centers[0];
        for(int i=0;i<centers.Length-1;i++)
        {
            var a=centers[i];var d=centers[i+1]-a;d.y=0;var delta=p-a;delta.y=0;
            var q=a+(centers[i+1]-a)*Mathf.Clamp01(Vector3.Dot(delta,d)/Mathf.Max(.0001f,d.sqrMagnitude));
            var e=p-q;e.y=0;if(e.sqrMagnitude<best){best=e.sqrMagnitude;projected=q;}
        }
        var offset=p-projected;offset.x*=WidthScale;offset.z*=WidthScale;
        return Scale(projected)+offset;
    }
    public static string Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!="Assets/Scenes/OverdriveF1.unity")throw new System.InvalidOperationException("Open F1 scene and stop Play.");
        var root=GameObject.Find("Refined Circuit").transform;
        if(root.Find("Grand Prix Expansion Applied"))return "Expansion already present.";
        if(scene.isDirty)throw new System.InvalidOperationException("Save existing scene changes before expansion.");
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/OverdriveF1_BeforeGrandPrix.unity",true);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Meshes","OverdriveGrandPrix");
        var road=root.Find("Asphalt Surface").GetComponent<MeshFilter>().sharedMesh;
        int oldRings=road.vertexCount/5-1,newRings=oldRings*3;
        var source=road.vertices;var centers=new Vector3[oldRings+1];
        for(int i=0;i<=oldRings;i++)centers[i]=(source[i*5]+source[i*5+4])*.5f;
        var expanded=new Vector3[newRings+1];var distances=new float[newRings+1];
        for(int i=0;i<=newRings;i++)
        {
            int a=Mathf.Min(i/3,oldRings-1);float t=(i-a*3)/3f;expanded[i]=Scale(Vector3.Lerp(centers[a],centers[a+1],t));
            if(i>0)distances[i]=distances[i-1]+Vector3.Distance(expanded[i-1],expanded[i]);
        }
        var strips=new HashSet<Transform>();int folded=0,changed=0;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=filter.sharedMesh;if(!AssetDatabase.GetAssetPath(mesh).Contains("CurveSmooth"))continue;
            int stride=mesh.vertexCount/(oldRings+1);if(stride!=2 && stride!=5)continue;
            strips.Add(filter.transform);var old=mesh.vertices;var vertices=new Vector3[(newRings+1)*stride];var uv=new Vector2[vertices.Length];
            bool vertical=stride==2;var first=new List<int>();var second=new List<int>();
            for(int i=0;i<=newRings;i++)
            {
                int a=Mathf.Min(i/3,oldRings-1);float t=(i-a*3)/3f;
                for(int x=0;x<stride;x++)
                {
                    var offset=Vector3.Lerp(old[a*stride+x]-centers[a],old[(a+1)*stride+x]-centers[a+1],t);offset.x*=WidthScale;offset.z*=WidthScale;
                    vertices[i*stride+x]=expanded[i]+offset;
                    uv[i*stride+x]=vertical?new Vector2(distances[i]/2,offset.y/2):new Vector2(x/(float)(stride-1),distances[i]/16);
                }
                if(i==newRings)continue;
                var triangles=(int)(distances[i]/3)%2==0?first:second;
                for(int x=0;x<stride-1;x++){int p=i*stride+x,q=p+stride;triangles.AddRange(new[]{p,q,p+1,p+1,q,q+1});}
            }
            if(vertical && filter.name.StartsWith("Left"))foreach(var list in new[]{first,second})for(int i=0;i<list.Count;i+=3){int a=list[i];list[i]=list[i+2];list[i+2]=a;}
            if(!vertical)foreach(var list in new[]{first,second})for(int i=0;i<list.Count;i+=3)if(Vector3.Cross(vertices[list[i+1]]-vertices[list[i]],vertices[list[i+2]]-vertices[list[i]]).y<=0)folded++;
            var replacement=new Mesh{name=filter.name+" Grand Prix",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};replacement.vertices=vertices;replacement.uv=uv;replacement.subMeshCount=2;replacement.SetTriangles(first,0);replacement.SetTriangles(second,1);replacement.RecalculateNormals();
            var normals=replacement.normals;for(int x=0;x<stride;x++){var n=(normals[x]+normals[newRings*stride+x]).normalized;normals[x]=normals[newRings*stride+x]=n;}replacement.normals=normals;replacement.RecalculateBounds();
            AssetDatabase.CreateAsset(replacement,Folder+"/"+filter.name+".asset");filter.sharedMesh=replacement;
            var collider=filter.GetComponent<MeshCollider>();if(collider)
            {
                collider.sharedMesh=null;
                if(vertical){var collision=Object.Instantiate(replacement);var all=new List<int>(replacement.triangles);var triangles=replacement.triangles;for(int i=0;i<triangles.Length;i+=3)all.AddRange(new[]{triangles[i+2],triangles[i+1],triangles[i]});collision.subMeshCount=1;collision.SetTriangles(all,0);AssetDatabase.CreateAsset(collision,Folder+"/"+filter.name+" Collision.asset");collider.sharedMesh=collision;}
                else collider.sharedMesh=replacement;
            }
            changed++;
        }
        // Relocate complete facility groups; their dimensions and nested detail stay unchanged.
        foreach(string name in new[]{"Refined Circuit","F1 Circuit - Apex Park","Grand Prix Circuit Dressing","Detailed Circuit Facilities"})
        {
            var group=GameObject.Find(name);if(!group)continue;
            foreach(Transform child in group.transform)
            {if(strips.Contains(child)||child.GetComponent<CircuitCheckpoint>())continue;child.position=Relocate(child.position,centers);}
        }
        var race=Object.FindFirstObjectByType<RaceManager>();var oldGates=race.ai.path.points;
        int count=Mathf.RoundToInt(distances[newRings]/8);var gates=new Transform[count];int segment=0;
        for(int i=0;i<count;i++)
        {
            float d=i*distances[newRings]/count;while(segment<newRings-1 && distances[segment+1]<d)segment++;
            float t=(d-distances[segment])/(distances[segment+1]-distances[segment]);
            var go=new GameObject("Checkpoint "+i);go.transform.SetParent(root,false);go.transform.position=Vector3.Lerp(expanded[segment],expanded[segment+1],t);
            go.transform.rotation=Quaternion.LookRotation(expanded[segment+1]-expanded[segment],Vector3.up);
            var box=go.AddComponent<BoxCollider>();box.isTrigger=true;box.center=Vector3.up*2;box.size=new Vector3(22,4,1.5f);go.AddComponent<CircuitCheckpoint>().index=i;gates[i]=go.transform;
        }
        race.ai.path.points=gates;race.player.GetComponent<CircuitProgress>().checkpoints=gates;race.opponent.GetComponent<CircuitProgress>().checkpoints=gates;
        foreach(var gate in oldGates)Object.DestroyImmediate(gate.gameObject);
        foreach(var car in new[]{race.player,race.opponent})
        {float side=car==race.player?-2.5f:2.5f;var start=gates[0];car.transform.SetPositionAndRotation(start.position+start.forward*3+start.right*side+Vector3.up*.7f,start.rotation);}
        var terrain=GameObject.Find("Terrain").transform;terrain.position=Scale(terrain.position);var size=terrain.localScale;size.x*=3;size.z*=3;terrain.localScale=size;
        var camera=Camera.main;camera.farClipPlane=Mathf.Max(camera.farClipPlane,2500);camera.transform.SetPositionAndRotation(race.player.transform.position-race.player.transform.forward*8+Vector3.up*3.1f,race.player.transform.rotation);
        RenderSettings.fogDensity=.00035f;
        foreach(var text in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(text.text.Contains("1.69"))text.text=text.text.Replace("1.69",(distances[newRings]/1000).ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
        new GameObject("Grand Prix Expansion Applied").transform.SetParent(root,false);
        if(folded>0)throw new System.InvalidOperationException("Folded horizontal triangles: "+folded);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        string result=$"lengthM={distances[newRings]:F2}; previousLengthM={distances[newRings]/3:F2}; roadWidthM={16.4f*WidthScale:F2}; gates={count}; longitudinalSpacingM={distances[newRings]/newRings:F3}; strips={changed}; foldedHorizontalTriangles={folded}; lengthScale=3; widthScale=1.15; vehicleScaleUnchanged=true";
        System.IO.File.WriteAllText("Docs/GrandPrixExpansion_Geometry.txt",result);return result;
    }
}
