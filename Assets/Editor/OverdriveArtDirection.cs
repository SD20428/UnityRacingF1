using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Overdrive;
using Object = UnityEngine.Object;

// Visual dressing only: all generated pieces deliberately have no colliders.
public static class OverdriveArtDirection
{
    const string Folder = "Assets/Art/GrandPrix";
    static Material carbon, white, red, silver, yellow, glass;
    static Material Mat(string name, Color color, float metallic=0, float smooth=.45f)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
        m.color=color; m.SetFloat("_Metallic",metallic); m.SetFloat("_Smoothness",smooth); EditorUtility.SetDirty(m); return m;
    }
    static Transform Group(string name, Transform parent)
    { var t=new GameObject(name).transform; t.SetParent(parent,false); return t; }
    static Transform Box(string name, Transform parent, Vector3 p, Vector3 size, Material m)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name; g.transform.SetParent(parent,false);
        g.transform.localPosition=p; g.transform.localScale=size; g.GetComponent<Renderer>().sharedMaterial=m;
        Object.DestroyImmediate(g.GetComponent<Collider>()); return g.transform;
    }
    static void Rod(Transform parent, Vector3 a, Vector3 b, float radius, Material m)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name="Aero strut"; g.transform.SetParent(parent,false);
        g.transform.localPosition=(a+b)/2; g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        g.transform.localScale=new Vector3(radius*2,(b-a).magnitude/2,radius*2); g.GetComponent<Renderer>().sharedMaterial=m; Object.DestroyImmediate(g.GetComponent<Collider>());
    }
    static void Shell(string name, Transform parent, float[] z, float[] w, float[] h, Material m, Vector3 offset)
    {
        var v=new List<Vector3>(); var tri=new List<int>();
        for(int i=0;i<z.Length;i++)
        {
            v.AddRange(new[]{new Vector3(-w[i],-.12f,z[i]),new Vector3(-w[i],h[i]*.5f,z[i]),new Vector3(-w[i]*.6f,h[i],z[i]),new Vector3(w[i]*.6f,h[i],z[i]),new Vector3(w[i],h[i]*.5f,z[i]),new Vector3(w[i],-.12f,z[i])});
            if(i==z.Length-1) continue;
            for(int f=0;f<6;f++) { int a=i*6+f,b=i*6+(f+1)%6; tri.AddRange(new[]{a,a+6,b,b,a+6,b+6}); }
        }
        for(int j=1;j<5;j++) { tri.AddRange(new[]{0,j,j+1}); int e=(z.Length-1)*6; tri.AddRange(new[]{e,e+j+1,e+j}); }
        var mesh=new Mesh {name=name}; mesh.SetVertices(v); mesh.SetTriangles(tri,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path=Folder+"/"+name+".asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; } else AssetDatabase.CreateAsset(mesh,path);
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); g.transform.SetParent(parent,false); g.transform.localPosition=offset;
        g.GetComponent<MeshFilter>().sharedMesh=mesh; g.GetComponent<Renderer>().sharedMaterial=m;
    }
    static void Label(string text, Transform parent, Vector3 p, Quaternion q, float size, Color color)
    {
        var t=Group(text,parent); t.localPosition=p; t.localRotation=q;
        var tm=t.gameObject.AddComponent<TextMesh>(); tm.text=text; tm.fontSize=64; tm.characterSize=size; tm.anchor=TextAnchor.MiddleCenter; tm.alignment=TextAlignment.Center; tm.color=color;
    }
    static void Car(OverdriveVehicle car, int index)
    {
        var prior=car.transform.Find("Grand Prix Bodywork"); if(prior) Object.DestroyImmediate(prior.gameObject);
        foreach(var r in car.GetComponentsInChildren<Renderer>(true))
        {
            bool wheel=false; foreach(var t in car.visuals) if(t && (r.transform==t || r.transform.IsChildOf(t))) wheel=true;
            if(!wheel) r.enabled=false;
        }
        var root=Group("Grand Prix Bodywork",car.transform);
        var paint=Mat("Team "+index,index==0?new Color(.015f,.44f,.48f):new Color(.95f,.19f,.025f),.12f,.43f);
        Shell("Monocoque "+index,root,new[]{-2.1f,-1.4f,-.7f,.25f,.9f,1.7f,2.65f},new[]{.18f,.42f,.44f,.38f,.26f,.16f,.09f},new[]{.25f,.63f,.40f,.32f,.24f,.17f,.08f},paint,Vector3.zero);
        Box("Carbon floor",root,new Vector3(0,-.16f,-.35f),new Vector3(1.85f,.055f,3.6f),carbon);
        Box("Cockpit opening",root,new Vector3(0,.36f,-.48f),new Vector3(.58f,.08f,.85f),carbon);
        for(int s=-1;s<=1;s+=2)
        {
            Shell("Sidepod "+index+" "+s,root,new[]{-1.65f,-1.1f,-.3f,.35f,.65f},new[]{.14f,.24f,.29f,.31f,.23f},new[]{.13f,.27f,.38f,.37f,.28f},paint,new Vector3(s*.66f,-.015f,0));
            Box("Sidepod intake",root,new Vector3(s*.66f,.18f,.655f),new Vector3(.37f,.16f,.025f),carbon);
            Box("Livery sweep",root,new Vector3(s*.958f,.12f,-.2f),new Vector3(.015f,.06f,1.35f),white);
            Box("Floor edge",root,new Vector3(s*.94f,-.12f,-.65f),new Vector3(.04f,.12f,2.35f),carbon);
            Box("Front wing endplate",root,new Vector3(s*1.27f,-.04f,2.4f),new Vector3(.045f,.28f,.65f),paint);
            Box("Rear wing endplate",root,new Vector3(s*1.02f,.78f,-2.12f),new Vector3(.055f,.55f,.68f),paint);
            Rod(root,new Vector3(s*.35f,.74f,-1),new Vector3(s*.4f,.82f,.08f),.032f,carbon);
            Rod(root,new Vector3(s*.4f,.82f,.08f),new Vector3(0,.85f,.22f),.032f,carbon);
            for(int axle=0;axle<2;axle++) for(int arm=-1;arm<=1;arm+=2)
                Rod(root,new Vector3(s*.38f,.02f,(axle==0?1.65f:-1.65f)+arm*.4f),new Vector3(s*1.05f,-.05f,axle==0?1.65f:-1.65f),.018f,carbon);
            Rod(root,new Vector3(s*.28f,.36f,-1.8f),new Vector3(s*.52f,.72f,-2.12f),.035f,carbon);
            for(int f=0;f<5;f++) Box("Diffuser vane",root,new Vector3(-.7f+f*.35f,-.08f,-1.99f),new Vector3(.025f,.17f,.5f),carbon);
            Box("Endplate carbon inset",root,new Vector3(s*1.052f,.78f,-2.12f),new Vector3(.012f,.34f,.46f),carbon);
            Box("Sidepod shoulder stripe",root,new Vector3(s*.69f,.375f,-.10f),new Vector3(.12f,.012f,.76f),white);
            Box("Mirror housing",root,new Vector3(s*.62f,.48f,.32f),new Vector3(.19f,.09f,.13f),paint);
            Rod(root,new Vector3(s*.36f,.27f,.26f),new Vector3(s*.62f,.46f,.32f),.012f,carbon);
            for(int vent=0;vent<5;vent++)
                Box("Cooling louvre",root,new Vector3(s*.68f,.29f-vent*.014f,-.63f-vent*.09f),new Vector3(.27f,.016f,.035f),carbon);
        }
        Rod(root,new Vector3(0,.3f,.25f),new Vector3(0,.85f,.22f),.032f,carbon);
        for(int f=0;f<4;f++) Box("Front wing cascade",root,new Vector3(0,-.19f+f*.045f,2.63f-f*.145f),new Vector3(2.5f,.035f,.17f),f==3?paint:carbon);
        for(int f=0;f<2;f++) Box("Rear wing aerofoil",root,new Vector3(0,.86f+f*.18f,-2.12f-f*.17f),new Vector3(2.05f,.055f,.34f),f==0?carbon:paint);
        Box("Rear rain light",root,new Vector3(0,.03f,-2.18f),new Vector3(.12f,.09f,.03f),red);
        Shell("Airbox "+index,root,new[]{-1.35f,-.85f,-.68f},new[]{.13f,.17f,.12f},new[]{.4f,.82f,.71f},paint,Vector3.zero);
        var helmet=GameObject.CreatePrimitive(PrimitiveType.Sphere); helmet.name="Driver helmet"; helmet.transform.SetParent(root,false); helmet.transform.localPosition=new Vector3(0,.57f,-.46f); helmet.transform.localScale=new Vector3(.33f,.33f,.38f); helmet.GetComponent<Renderer>().sharedMaterial=yellow; Object.DestroyImmediate(helmet.GetComponent<Collider>());
        Box("Dark visor",root,new Vector3(0,.6f,-.275f),new Vector3(.27f,.1f,.03f),glass);
        Label("APEX",root,new Vector3(0,1.071f,-2.29f),Quaternion.Euler(90,0,0),.066f,Color.white);
        Label(index==0?"27":"81",root,new Vector3(0,.29f,.83f),Quaternion.Euler(90,0,0),.063f,Color.white);
        for(int i=0;i<car.visuals.Length;i++)
        {
            car.visuals[i].position=car.wheels[i].transform.position-car.transform.up*car.wheels[i].suspensionDistance*.5f; car.visuals[i].rotation=car.transform.rotation;
            var old=car.visuals[i].Find("Tyre graphics"); if(old) Object.DestroyImmediate(old.gameObject);
            var tyre=Group("Tyre graphics",car.visuals[i]); float x=(i%2==0?-1:1)*(i<2?.185f:.225f);
            var hub=GameObject.CreatePrimitive(PrimitiveType.Cylinder); hub.name="Aero wheel cover";
            hub.transform.SetParent(tyre,false); hub.transform.localPosition=new Vector3(x,0,0);
            hub.transform.localRotation=Quaternion.Euler(0,0,90); hub.transform.localScale=new Vector3(.46f,.009f,.46f);
            hub.GetComponent<Renderer>().sharedMaterial=carbon; Object.DestroyImmediate(hub.GetComponent<Collider>());
            var nut=GameObject.CreatePrimitive(PrimitiveType.Cylinder); nut.name="Centre lock";
            nut.transform.SetParent(tyre,false); nut.transform.localPosition=new Vector3(x+Mathf.Sign(x)*.014f,0,0);
            nut.transform.localRotation=Quaternion.Euler(0,0,90); nut.transform.localScale=new Vector3(.085f,.014f,.085f);
            nut.GetComponent<Renderer>().sharedMaterial=silver; Object.DestroyImmediate(nut.GetComponent<Collider>());
            // Sidewall rings rotate with the wheel visual, independently of the suspension.
            for(int k=0;k<48;k++)
            { float a=k*Mathf.PI*2/48,b=(k+1)*Mathf.PI*2/48; Rod(tyre,new Vector3(x,Mathf.Sin(a)*.303f,Mathf.Cos(a)*.303f),new Vector3(x,Mathf.Sin(b)*.303f,Mathf.Cos(b)*.303f),.009f,yellow); }
        }
    }
    [MenuItem("Overdrive/Apply Grand Prix Art")]
    public static string Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!="Assets/Scenes/OverdriveF1.unity") throw new System.InvalidOperationException("Open OverdriveF1 in edit mode.");
        System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        carbon=Mat("Carbon",new Color(.018f,.023f,.029f),.3f,.38f); white=Mat("Ivory",new Color(.93f,.95f,.94f)); red=Mat("Race red",new Color(.85f,.025f,.045f)); silver=Mat("Aluminium",new Color(.38f,.44f,.49f),.8f,.65f); yellow=Mat("Tyre yellow",new Color(1,.76f,.035f)); glass=Mat("Visor",new Color(.025f,.075f,.12f),.65f,.9f);
        var race=Object.FindFirstObjectByType<RaceManager>(); if(!race || !race.ai.path || race.ai.path.points.Length<3) throw new System.InvalidOperationException("Race path required.");
        Car(race.player,0); Car(race.opponent,1);
        var old=GameObject.Find("Grand Prix Circuit Dressing"); if(old) Object.DestroyImmediate(old);
        var root=Group("Grand Prix Circuit Dressing",null); var points=race.ai.path.points;
        var start=Group("Start finish",root); start.SetPositionAndRotation(points[0].position,points[0].rotation);
        for(int row=0;row<8;row++) for(int side=-1;side<=1;side+=2)
        {
            float z=3+row*7+(side==1?3.5f:0),x=side*2.3f;
            Box("Grid front",start,new Vector3(x,.018f,z),new Vector3(2.1f,.012f,.10f),white);
            for(int s=-1;s<=1;s+=2) Box("Grid side",start,new Vector3(x+s*1.05f,.018f,z-1),new Vector3(.09f,.012f,2),white);
        }
        Box("Gantry fascia",start,new Vector3(0,6.8f,13),new Vector3(22,1.25f,.5f),carbon);
        foreach(int side in new[]{-1,1}) Box("Gantry column",start,new Vector3(side*10.4f,3.3f,13),new Vector3(.45f,6.6f,.5f),silver);
        Label("APEX  /  GRAND PRIX",start,new Vector3(0,6.9f,12.73f),Quaternion.identity,.18f,Color.white);
        for(int i=0;i<5;i++) { Box("Light housing",start,new Vector3((i-2)*.55f,5.8f,12.85f),new Vector3(.4f,.7f,.2f),carbon); for(int j=0;j<2;j++) Box("Start lamp",start,new Vector3((i-2)*.55f,5.64f+j*.3f,12.73f),new Vector3(.22f,.22f,.03f),red); }
        for(int i=8;i<points.Length;i+=12)
        {
            var anchor=Group("Circuit panel "+i,root); anchor.SetPositionAndRotation(points[i].position,points[i].rotation);
            foreach(int side in new[]{-1,1})
            {
                var panel=Group("Trackside branding",anchor); panel.localPosition=new Vector3(side*14.5f,1.65f,0); panel.localRotation=Quaternion.Euler(0,side==1?-90:90,0);
                Box("Board",panel,Vector3.zero,new Vector3(7,.85f,.08f),i%24==8?red:carbon);
                panel.localRotation=Quaternion.Euler(0,side*90,0);
                Label(i%24==8?"APEX RACING":"OVERDRIVE",panel,new Vector3(0,0,-.05f),Quaternion.identity,.18f,Color.white);
            }
        }
        // Place braking boards by distance before distinct corners, beyond the driveable surface.
        int boards=0;
        for(int i=8;i<points.Length;i+=8)
        {
            var a=points[i].forward; var b=points[(i+4)%points.Length].forward;
            if(Vector3.Angle(a,b)<24) continue;
            foreach(int d in new[]{50,100,150})
            {
                int k=i; float travel=0; while(travel<d) { int prev=(k-1+points.Length)%points.Length; travel+=Vector3.Distance(points[k].position,points[prev].position); k=prev; if(k==i) break; }
                var t=Group("Braking "+d,root); t.SetPositionAndRotation(points[k].position+points[k].right*10.4f,points[k].rotation);
                Box("Board",t,new Vector3(0,1.2f,0),new Vector3(1.1f,1.1f,.08f),white);
                Label(d.ToString(),t,new Vector3(0,1.2f,-.05f),Quaternion.identity,.19f,Color.black);
                Box("Post",t,new Vector3(0,.4f,0),new Vector3(.07f,.8f,.07f),silver); boards++;
            }
            i+=16;
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return "Grand Prix art saved: 2 sculpted cars, layered wings, sidepods, halos, tyre rings, grid, gantry, branding and "+boards+" braking boards. Physics untouched.";
    }
}


