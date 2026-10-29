using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Overdrive;
using Object=UnityEngine.Object;

public static class OverdriveGrandPrixPresentation
{
    const string Folder="Assets/Art/GrandPrix/Presentation";
    const string DetailName="Grand Prix Aero Detail";
    static Material carbon,metal,cameraYellow;
    public static int RemoveFinishMarkings()
    {
        // Only decorative objects; checkpoint zero and race timing remain intact.
        var remove=new List<GameObject>();var scene=EditorSceneManager.GetActiveScene();
        foreach(var root in scene.GetRootGameObjects()) foreach(var t in root.GetComponentsInChildren<Transform>(true))
            if((t.name=="Finish"||t.name=="Flush checker finish paint")&&!t.GetComponent<CircuitCheckpoint>()) remove.Add(t.gameObject);
        foreach(var go in remove) if(go)Object.DestroyImmediate(go);return remove.Count;
    }
    static Material Mat(string name,Color color,float metallic,float smooth)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static Mesh Save(string name,Mesh m)
    {
        m.name=name;string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(m,path);return m;
    }
    static Transform Group(string name,Transform parent)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    static Transform Box(Transform parent,string name,Vector3 p,Vector3 size,Material m)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;
    }
    static void Cylinder(Transform parent,Vector3 p,Vector3 size,Material m)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Exhaust tip";go.transform.SetParent(parent,false);go.transform.localPosition=p;
        go.transform.localRotation=Quaternion.Euler(90,0,0);go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static void Fin(Transform parent,string name,Vector2[] profile,float x,float thickness,Material material)
    {
        var vertices=new List<Vector3>();var tri=new List<int>();int n=profile.Length;
        foreach(int side in new[]{-1,1})foreach(var p in profile)vertices.Add(new Vector3(x+side*thickness*.5f,p.x,p.y));
        for(int i=1;i<n-1;i++){tri.AddRange(new[]{0,i+1,i,n,n+i,n+i+1});}
        for(int i=0;i<n;i++){int j=(i+1)%n;tri.AddRange(new[]{i,j,n+i,j,n+j,n+i});}
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var t=Group(name,parent);t.gameObject.AddComponent<MeshFilter>().sharedMesh=Save(name,mesh);t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    static void Batch(Transform root)
    {
        var groups=new Dictionary<Material,List<CombineInstance>>();var remove=new List<GameObject>();
        foreach(var f in root.GetComponentsInChildren<MeshFilter>())
        {var r=f.GetComponent<Renderer>();if(!groups.ContainsKey(r.sharedMaterial))groups[r.sharedMaterial]=new List<CombineInstance>();groups[r.sharedMaterial].Add(new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix});remove.Add(f.gameObject);}
        foreach(var pair in groups)
        {var mesh=new Mesh {indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());mesh.RecalculateBounds();var t=Group("Aero "+pair.Key.name,root);t.gameObject.AddComponent<MeshFilter>().sharedMesh=Save("Aero "+pair.Key.name,mesh);t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;}
        foreach(var g in remove)Object.DestroyImmediate(g);
    }
    static Texture2D Weave()
    {
        const int size=128;var tex=new Texture2D(size,size,TextureFormat.RGB24,true);var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {bool over=((x/8+y/8)%2)==0;float fiber=over?Mathf.Sin((x%8)*Mathf.PI/8):Mathf.Sin((y%8)*Mathf.PI/8);float noise=Mathf.PerlinNoise(x*.7f,y*.7f);float value=.34f+fiber*.22f+noise*.06f;pixels[y*size+x]=new Color(value,value*1.015f,value*1.025f);}
        tex.SetPixels(pixels);tex.Apply();string path=Folder+"/Carbon weave.png";System.IO.File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);
        var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.anisoLevel=8;imp.wrapMode=TextureWrapMode.Repeat;imp.mipmapEnabled=true;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Transform Prototype(OverdriveVehicle car)
    {
        var t=Group(DetailName,car.transform);
        Box(t,"T camera stem",new Vector3(0,.92f,-.83f),new Vector3(.055f,.22f,.07f),carbon);
        Box(t,"T camera crossbar",new Vector3(0,1.045f,-.83f),new Vector3(.38f,.065f,.10f),cameraYellow);
        for(int s=-1;s<=1;s+=2)
        {
            Box(t,"Onboard camera housing",new Vector3(s*.19f,1.045f,-.83f),new Vector3(.065f,.085f,.15f),carbon);
            Box(t,"Nose camera",new Vector3(s*.23f,.26f,1.05f),new Vector3(.13f,.065f,.16f),carbon);
            Box(t,"Floor edge strake",new Vector3(s*.95f,-.09f,-.15f),new Vector3(.025f,.1f,1.1f),carbon);
            Fin(t,"Curved wingtip "+s,new[]{new Vector2(-.18f,2.73f),new Vector2(-.18f,2.04f),new Vector2(-.02f,2.09f),new Vector2(.12f,2.40f),new Vector2(.07f,2.71f)},s*1.3f,.025f,carbon);
            Box(t,"Rear wing fastener",new Vector3(s*.78f,1.077f,-2.29f),new Vector3(.035f,.025f,.035f),metal);
        }
        Fin(t,"Engine cover stabiliser",new[]{new Vector2(.26f,-2.02f),new Vector2(.33f,-1.26f),new Vector2(.65f,-.98f),new Vector2(.61f,-1.40f)},0,.025f,carbon);
        Cylinder(t,new Vector3(0,.27f,-2.08f),new Vector3(.13f,.11f,.13f),metal);
        Cylinder(t,new Vector3(0,.27f,-2.2f),new Vector3(.095f,.012f,.095f),carbon);
        Box(t,"Rear wing actuator",new Vector3(0,1.11f,-2.17f),new Vector3(.07f,.09f,.25f),carbon);
        Box(t,"Rear wing hinge",new Vector3(0,1.04f,-2.32f),new Vector3(.20f,.035f,.055f),metal);
        Batch(t);return t;
    }
    static Mesh WheelRings()
    {
        var v=new List<Vector3>();var tris=new List<int>();var uv=new List<Vector2>();const int around=48,tube=6;
        foreach(float radius in new[]{.225f,.085f})
        {
            int offset=v.Count;
            for(int i=0;i<=around;i++)for(int j=0;j<=tube;j++)
            {
                float a=i*Mathf.PI*2/around,b=j*Mathf.PI*2/tube;float r=radius+Mathf.Cos(b)*.004f;
                v.Add(new Vector3(Mathf.Sin(b)*.004f,Mathf.Cos(a)*r,Mathf.Sin(a)*r));uv.Add(new Vector2(i/(float)around,j/(float)tube));
                if(i<around&&j<tube){int k=offset+i*(tube+1)+j;tris.AddRange(new[]{k,k+tube+1,k+1,k+1,k+tube+1,k+tube+2});}
            }
        }
        var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tris,0);m.RecalculateNormals();m.RecalculateBounds();return Save("Aero wheel trim rings",m);
    }
    [MenuItem("Overdrive/Refine Grand Prix Presentation Without Finish Line")]
    public static string Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/OverdriveF1.unity")throw new System.InvalidOperationException("Open F1 scene in edit mode.");
        var cars=Object.FindObjectsByType<OverdriveVehicle>(FindObjectsSortMode.InstanceID);if(cars.Length==0)throw new System.InvalidOperationException("No cars in scene.");
        System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var weave=Weave();
        carbon=Mat("Woven carbon",new Color(.09f,.10f,.11f),.12f,.33f);carbon.mainTexture=weave;carbon.mainTextureScale=new Vector2(5,5);
        metal=Mat("Titanium",new Color(.22f,.25f,.28f),.72f,.48f);cameraYellow=Mat("Camera safety yellow",new Color(.92f,.66f,.025f),.04f,.35f);
        foreach(var car in cars){var old=car.transform.Find(DetailName);if(old)Object.DestroyImmediate(old.gameObject);}
        var prototype=Prototype(cars[0]);for(int i=1;i<cars.Length;i++){var clone=Object.Instantiate(prototype.gameObject,cars[i].transform,false);clone.name=DetailName;}
        var wheelMesh=WheelRings();var visited=new HashSet<Material>();
        foreach(var car in cars)
        {
            for(int i=0;i<car.visuals.Length;i++)
            {
                var wheel=car.visuals[i];var old=wheel.Find("Precision rim rings");if(old)Object.DestroyImmediate(old.gameObject);
                var ring=Group("Precision rim rings",wheel);ring.localPosition=new Vector3((i%2==0?-1:1)*(i<2?.201f:.241f),0,0);
                ring.gameObject.AddComponent<MeshFilter>().sharedMesh=wheelMesh;ring.gameObject.AddComponent<MeshRenderer>().sharedMaterial=metal;
            }
            foreach(var r in car.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)
            {
                if(!m||!visited.Add(m)||!m.HasProperty("_Smoothness"))continue;
                string path=AssetDatabase.GetAssetPath(m);if(!path.StartsWith("Assets/"))continue;
                string name=m.name.ToLowerInvariant();
                if(name.Contains("carbon")){m.mainTexture=weave;m.mainTextureScale=new Vector2(5,5);m.SetFloat("_Metallic",.12f);m.SetFloat("_Smoothness",.32f);}
                else if(path.Contains("GrandPrixGrid")){m.SetFloat("_Metallic",.12f);m.SetFloat("_Smoothness",.43f);}
                else if(name.Contains("tyre")&&!name.Contains("yellow")){m.SetFloat("_Smoothness",.17f);m.SetFloat("_Metallic",0);}
                EditorUtility.SetDirty(m);
            }
        }
        int removed=RemoveFinishMarkings();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        string result=$"carsDetailed={cars.Length}; removedFinishObjects={removed}; wheelDetailSets={cars.Length*4}; decorativeColliders={prototype.GetComponentsInChildren<Collider>().Length}; sharedAeroMeshes=true; finishGenerationDisabled=true";
        System.IO.File.WriteAllText("Docs/GrandPrixPresentation_Result.txt",result);return result;
    }
    public static string Preview()
    {
        var car=Object.FindFirstObjectByType<RaceManager>().player.transform;var go=new GameObject("Presentation preview");var camera=go.AddComponent<Camera>();camera.fieldOfView=45;camera.farClipPlane=2500;
        camera.transform.position=car.TransformPoint(new Vector3(6,3.2f,8));camera.transform.LookAt(car.position+Vector3.up*.25f);
        var rt=RenderTexture.GetTemporary(1440,900,24);var previous=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();System.IO.File.WriteAllBytes("Docs/GrandPrixPresentation_Preview.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);}
        finally{RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(go);}return "Preview saved";
    }
}
