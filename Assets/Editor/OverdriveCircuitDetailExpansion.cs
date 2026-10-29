using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Overdrive;
using Object=UnityEngine.Object;

public static class OverdriveCircuitDetailExpansion
{
    const string Base="Assets/Art/CircuitDetails",RootName="Circuit Detail Expansion";
    static string folder,key;static Transform root;static WaypointPath route;static RaceManager race;static CircuitSceneryMotion motion;
    static TMP_FontAsset font;static Mesh cube,cylinder,head,flagMesh;static System.Random random;
    static Material dark,steel,white,concrete,accent,orange,red,skin,skinDark,blue,yellow,bark,leaf,leafLight,glass,screen,flagWhite,rubber,lamp;
    static readonly List<CircuitSceneryMotion.Zone> zones=new List<CircuitSceneryMotion.Zone>();
    static readonly List<CircuitSceneryMotion.Placement> sites=new List<CircuitSceneryMotion.Placement>();
    static readonly List<MeshFilter> flags=new List<MeshFilter>();
    static readonly List<Transform> arms=new List<Transform>(),sway=new List<Transform>();
    static readonly List<Renderer> lamps=new List<Renderer>();static readonly List<TMP_Text> boards=new List<TMP_Text>();
    static readonly HashSet<GameObject> dynamicObjects=new HashSet<GameObject>();
    static void Folder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;string parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));}
    static Mesh SaveMesh(string path,Mesh mesh)
    {
        var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static Material Mat(string name,Color color,bool transparent=false,bool emissive=false)
    {
        string path=Base+"/Shared/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.24f);
        if(transparent){m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;}
        if(emissive){m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.2f);}
        EditorUtility.SetDirty(m);return m;
    }
    static Mesh LowCylinder()
    {
        var mesh=new Mesh{name="Low polygon cylinder"};var vertices=new List<Vector3>();var tris=new List<int>();const int sides=10;
        for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Cos(a)*.5f,-.5f,Mathf.Sin(a)*.5f));vertices.Add(new Vector3(Mathf.Cos(a)*.5f,.5f,Mathf.Sin(a)*.5f));}
        vertices.Add(new Vector3(0,-.5f,0));vertices.Add(new Vector3(0,.5f,0));
        for(int j=0;j<sides;j++){int a=j*2,b=(j+1)%sides*2;tris.AddRange(new[]{a,a+1,b,a+1,b+1,b,20,a,b,21,b+1,a+1});}
        mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return SaveMesh(Base+"/Shared/Cylinder.asset",mesh);
    }
    static Mesh Head()
    {
        var mesh=new Mesh{name="Low polygon spectator head"};mesh.vertices=new[]{Vector3.up*.5f,Vector3.down*.5f,Vector3.left*.5f,Vector3.right*.5f,Vector3.forward*.5f,Vector3.back*.5f};
        mesh.triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};mesh.RecalculateNormals();mesh.RecalculateBounds();return SaveMesh(Base+"/Shared/Head.asset",mesh);
    }
    static Mesh Flag()
    {
        var mesh=new Mesh{name="Detail flag source"};var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
        for(int x=0;x<=8;x++)for(int y=0;y<=4;y++){v.Add(new Vector3(x*.3f,y*.3f,0));uv.Add(new Vector2(x/8f,y/4f));if(x<8 && y<4){int a=x*5+y;tri.AddRange(new[]{a,a+1,a+5,a+1,a+6,a+5});}}
        mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.bounds=new Bounds(new Vector3(1.2f,.6f,0),new Vector3(2.6f,1.5f,.5f));return SaveMesh(Base+"/Shared/Flag.asset",mesh);
    }
    static GameObject Shape(string name,Transform parent,Mesh mesh,Vector3 pos,Vector3 scale,Material material,Quaternion? rotation=null)
    {
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;
        if(rotation.HasValue)g.transform.localRotation=rotation.Value;g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=material;return g;
    }
    static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 scale,Material material,Quaternion? rotation=null)=>Shape(name,parent,cube,pos,scale,material,rotation);
    static void Beam(string name,Transform parent,Vector3 a,Vector3 b,float size,Material material)
        =>Box(name,parent,(a+b)*.5f,new Vector3(size,size,(b-a).magnitude),material,Quaternion.LookRotation(b-a));
    static Transform Group(string name,Transform parent,Vector3 position,Quaternion? rotation=null)
    {var g=new GameObject(name).transform;g.SetParent(parent,false);g.localPosition=position;if(rotation.HasValue)g.localRotation=rotation.Value;return g;}
    static TMP_Text Text(string name,string content,Transform parent,Vector3 pos,Quaternion rotation,float size,Vector2 area,Color color)
    {
        var text=new GameObject(name,typeof(TextMeshPro)).GetComponent<TMP_Text>();text.transform.SetParent(parent,false);text.transform.localPosition=pos;text.transform.localRotation=rotation;
        text.font=font;text.text=content;text.fontSize=size;text.rectTransform.sizeDelta=area;text.color=color;text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;return text;
    }
    static float Distance(Vector3 p)
    {
        p.y=0;float best=float.PositiveInfinity;
        for(int i=0;i<route.points.Length;i++){var a=route.Position(i);var b=route.Position(i+1);a.y=b.y=0;var delta=b-a;var q=a+delta*Mathf.Clamp01(Vector3.Dot(p-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));best=Mathf.Min(best,(p-q).sqrMagnitude);}
        return Mathf.Sqrt(best);
    }
    static bool Ground(Vector3 p,out float y)
    {
        y=p.y;foreach(var hit in Physics.RaycastAll(p+Vector3.up*30,Vector3.down,60,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
        {if(hit.rigidbody || hit.normal.y<.85f)continue;var s=hit.collider.GetComponentInParent<TrackSurface>();if(s || hit.collider.name=="Terrain"){y=hit.point.y;return true;}}
        return false;
    }
    static Transform Site(string name,int checkpoint,int side,float offset,float radius,float range=750)
    {
        checkpoint=(checkpoint%route.points.Length+route.points.Length)%route.points.Length;var p=route.points[checkpoint];var position=p.position+p.right*side*offset;
        if(Distance(position)<motion.protectedHalfWidth+radius || sites.Any(s=>Vector3.Distance(s.centre,position)<s.radius+radius+3) || !Ground(position,out float y))return null;
        position.y=y;var group=Group(name,root,position,p.rotation);sites.Add(new CircuitSceneryMotion.Placement{label=name,centre=position,radius=radius});zones.Add(new CircuitSceneryMotion.Zone{root=group.gameObject,centre=position,radius=radius,viewDistance=range});return group;
    }
    static void FlagAt(Transform parent,Vector3 pos,Material material)
    {
        Beam("Flag mast",parent,pos,pos+Vector3.up*6.5f,.075f,steel);
        var g=Shape("Wind flag",parent,flagMesh,pos+Vector3.up*5.1f,Vector3.one,material);flags.Add(g.GetComponent<MeshFilter>());dynamicObjects.Add(g);
    }
    static void Person(Transform parent,Vector3 pos,Quaternion rotation,Material shirt,bool wave=false,bool seated=false)
    {
        var person=Group(wave?"Waving supporter":"Spectator",parent,pos,rotation);float torso=seated?.57f:.95f;
        Box("Jacket",person,new Vector3(0,torso,0),new Vector3(.42f,.63f,.3f),shirt);
        Shape("Head",person,head,new Vector3(0,torso+.54f,0),new Vector3(.29f,.34f,.29f),random.Next(3)==0?skinDark:skin);
        if(seated){Box("Seated legs",person,new Vector3(0,.2f,.19f),new Vector3(.38f,.18f,.55f),dark);}
        else foreach(int side in new[]{-1,1})Box("Trouser leg",person,new Vector3(side*.115f,.37f,0),new Vector3(.17f,.65f,.2f),dark);
        foreach(int side in new[]{-1,1})
        {
            if(wave && side==1)
            {
                var pivot=Group("Waving arm pivot",person,new Vector3(.27f,torso+.23f,0),Quaternion.Euler(0,0,-25));
                var arm=Box("Raised arm",pivot,new Vector3(0,.27f,0),new Vector3(.13f,.58f,.14f),shirt);Shape("Raised hand",pivot,head,new Vector3(0,.6f,0),Vector3.one*.16f,skin);
                arms.Add(pivot);foreach(var renderer in pivot.GetComponentsInChildren<MeshRenderer>())dynamicObjects.Add(renderer.gameObject);
            }
            else Box("Arm",person,new Vector3(side*.29f,torso-.04f,.03f),new Vector3(.13f,.59f,.15f),shirt,Quaternion.Euler(side*8,0,side*9));
        }
    }
    static void Grandstand(Transform zone,int side,int number)
    {
        Box("Spectator plaza",zone,new Vector3(side*6,.04f,0),new Vector3(30,.08f,38),concrete);
        for(int row=0;row<7;row++)
        {
            float x=side*(-6.1f+row*1.85f),y=.52f+row*.63f;
            Box("Seating terrace",zone,new Vector3(x,y-.2f,0),new Vector3(1.85f,.4f,31),concrete);
            for(int seat=0;seat<22;seat++)
            {
                float z=-14.3f+seat*1.36f;if(seat==10 || seat==11)continue;
                Box("Seat pan",zone,new Vector3(x,y+.15f,z),new Vector3(.55f,.12f,.56f),(row+seat)%5==0?white:accent);
                if(random.NextDouble()>.16)Person(zone,new Vector3(x,y+.20f,z),Quaternion.Euler(0,-side*90,0),new[]{accent,white,blue,red,orange}[random.Next(5)],row%3==0 && seat==6,true);
            }
            Box("Central aisle",zone,new Vector3(x,y+.015f,0),new Vector3(1.85f,.03f,1.8f),white);
        }
        Box("Canopy",zone,new Vector3(0,7.4f,0),new Vector3(15.5f,.28f,34),white,Quaternion.Euler(0,0,-side*4));
        Box("Team fascia",zone,new Vector3(-side*7.65f,7,0),new Vector3(.14f,.7f,34),accent);
        foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Beam("Canopy column",zone,new Vector3(x*7,0,z*15.8f),new Vector3(x*7,7.3f,z*15.8f),.16f,steel);
        for(int z=-15;z<=15;z+=3){Beam("Front railing post",zone,new Vector3(-side*7,0,z),new Vector3(-side*7,1.1f,z),.055f,steel);}
        Beam("Front safety rail",zone,new Vector3(-side*7,1.1f,-15),new Vector3(-side*7,1.1f,15),.06f,steel);
        Text("Stand title",race.CircuitName+"  /  GRANDSTAND "+number,zone,new Vector3(-side*7.77f,7,0),Quaternion.Euler(0,side*90,0),2.5f,new Vector2(31,.65f),Color.white);
        Box("Fan kiosk",zone,new Vector3(side*20,1.4f,0),new Vector3(5,2.8f,9),dark);
        Box("Kiosk awning",zone,new Vector3(side*17.3f,3,0),new Vector3(5,.18f,10),accent);
        foreach(int s in new[]{-1,1})for(int table=0;table<3;table++)
        {var p=new Vector3(side*20,0,s*(8+table*3));Box("Picnic table",zone,p+Vector3.up*.8f,new Vector3(2,.14f,1),white);foreach(int b in new[]{-1,1})Box("Bench",zone,p+Vector3.up*.4f+Vector3.forward*b*.85f,new Vector3(2,.15f,.35f),accent);}
        FlagAt(zone,new Vector3(side*24,0,-14),accent);FlagAt(zone,new Vector3(side*24,0,14),flagWhite);
    }
    static void MarshalPost(Transform zone,int number)
    {
        Box("Marshal platform",zone,new Vector3(0,.15f,0),new Vector3(4,.3f,5),concrete);
        Box("Marshal shelter",zone,new Vector3(1.1f,1.2f,0),new Vector3(1.4f,2.1f,3.5f),white);Box("Shelter roof",zone,new Vector3(0,2.8f,0),new Vector3(4.2f,.18f,5.2f),accent);
        foreach(int z in new[]{-1,1})Beam("Shelter pole",zone,new Vector3(-1.7f,0,z*2),new Vector3(-1.7f,2.8f,z*2),.09f,steel);
        Person(zone,new Vector3(-.5f,.3f,-.9f),Quaternion.Euler(0,90,0),orange);Person(zone,new Vector3(-.4f,.3f,1),Quaternion.Euler(0,90,0),orange);
        Shape("Fire extinguisher",zone,cylinder,new Vector3(1.5f,.75f,2.1f),new Vector3(.22f,.8f,.22f),red);
        Box("Emergency cabinet",zone,new Vector3(1.5f,1,-2.1f),new Vector3(.6f,1.3f,.3f),red);
        Text("Marshal post label","MP "+number.ToString("00"),zone,new Vector3(-1.96f,2.2f,0),Quaternion.Euler(0,90,0),2.2f,new Vector2(3,.5f),Color.white);
        Beam("Signal mast",zone,new Vector3(-1.5f,0,-2),new Vector3(-1.5f,4,-2),.12f,steel);
        var panel=Box("Marshal signal panel",zone,new Vector3(-1.5f,3.7f,-2),new Vector3(.15f,.7f,1.3f),lamp);lamps.Add(panel.GetComponent<Renderer>());dynamicObjects.Add(panel);
        FlagAt(zone,new Vector3(1.5f,0,2.2f),accent);
    }
    static Texture2D CircuitMap()
    {
        const int w=1024,h=512;var texture=new Texture2D(w,h,TextureFormat.RGBA32,false);var pixels=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)pixels[y*w+x]=x%64==0 || y%64==0?new Color(.025f,.075f,.1f):new Color(.008f,.018f,.035f);
        Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
        foreach(var p in route.points){var v=new Vector2(p.position.x,p.position.z);min=Vector2.Min(min,v);max=Vector2.Max(max,v);}
        float scale=Mathf.Min((w-120)/(max.x-min.x),(h-80)/(max.y-min.y));
        Vector2 Map(Vector3 p)=>new Vector2(w*.5f,h*.5f)+(new Vector2(p.x,p.z)-(min+max)*.5f)*scale;
        for(int i=0;i<route.points.Length;i++)
        {
            var a=Map(route.Position(i));var b=Map(route.Position(i+1));int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)));
            for(int j=0;j<=steps;j++){var p=Vector2.Lerp(a,b,j/(float)steps);for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++){int u=Mathf.RoundToInt(p.x)+x,v=Mathf.RoundToInt(p.y)+y;if(u>=0 && u<w && v>=0 && v<h)pixels[v*w+u]=new Color(.13f,.9f,.82f);}}
        }
        var start=Map(route.Position(0));for(int x=-5;x<=5;x++)for(int y=-5;y<=5;y++)if(x*x+y*y<26)pixels[(Mathf.RoundToInt(start.y)+y)*w+Mathf.RoundToInt(start.x)+x]=Color.white;
        texture.SetPixels(pixels);texture.Apply();string path=folder+"/Live circuit map.png";System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void Screen(Transform zone,int side)
    {
        var turn=Quaternion.Euler(0,side*35,0);var frame=Group("Trackside screen",zone,Vector3.zero,turn);
        foreach(int columnSide in new[]{-1,1})Beam("Screen support",frame,new Vector3(columnSide*3.2f,0,0),new Vector3(columnSide*3.2f,8,0),.28f,steel);
        Box("Screen backing",frame,new Vector3(0,6.8f,0),new Vector3(9,5.1f,.45f),dark);
        var display=Shape("Circuit map display",frame,cube,new Vector3(0,6.8f,-.25f),new Vector3(8.5f,4.6f,.025f),screen);
        display.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        var text=Text("Live timing board",race.CircuitName,frame,new Vector3(0,3.6f,-.31f),Quaternion.identity,3,new Vector2(11,1.3f),Color.white);boards.Add(text);
    }
    static void ServicePad(Transform zone)
    {
        Box("Team service apron",zone,new Vector3(0,.03f,0),new Vector3(20,.06f,17),concrete);
        Box("Mobile team office",zone,new Vector3(5,1.7f,0),new Vector3(7,3.4f,13),white);Box("Office glazed band",zone,new Vector3(1.45f,2.2f,0),new Vector3(.08f,1.2f,11),glass);
        for(int rack=0;rack<3;rack++)
        {
            float z=-5+rack*5;Box("Tyre rack",zone,new Vector3(-4,.55f,z),new Vector3(3.4f,.12f,1.4f),steel);
            foreach(int x in new[]{-1,0,1})Shape("Spare tyre",zone,cylinder,new Vector3(-4+x,.95f,z),new Vector3(.78f,.34f,.78f),dark,Quaternion.Euler(0,0,90));
            Person(zone,new Vector3(-1,0,z),Quaternion.Euler(0,-90,0),rack%2==0?accent:orange,rack==1);
            Box("Tool trolley",zone,new Vector3(-1,.65f,z+1.5f),new Vector3(1,.9f,.65f),accent);for(int k=0;k<4;k++)Box("Drawer",zone,new Vector3(-1,.45f+k*.18f,z+1.84f),new Vector3(.8f,.04f,.02f),steel);
            Shape("Air bottle",zone,cylinder,new Vector3(-7,.85f,z),new Vector3(.35f,1.7f,.35f),blue);
        }
        Text("Team office branding",race.CircuitName+"\nTEAM OPERATIONS",zone,new Vector3(1.4f,2,0),Quaternion.Euler(0,90,0),3.2f,new Vector2(10,2),Color.white);
        FlagAt(zone,new Vector3(-8,0,7),accent);FlagAt(zone,new Vector3(8,0,7),flagWhite);
    }
    static Transform ExactSite(string name,Vector3 position,Quaternion rotation,float radius,float range=1000)
    {
        if(Distance(position)<motion.protectedHalfWidth+radius || !Ground(position,out float y))return null;
        position.y=y;var zone=Group(name,root,position,rotation);sites.Add(new CircuitSceneryMotion.Placement{label=name,centre=position,radius=radius});zones.Add(new CircuitSceneryMotion.Zone{root=zone.gameObject,centre=position,radius=radius,viewDistance=range});return zone;
    }
    static void Tree(Transform zone,Vector3 pos,float height,bool palm)
    {
        Shape("Tree trunk",zone,cylinder,pos+Vector3.up*height*.45f,new Vector3(.38f,height*.9f,.38f),bark);
        if(palm)
        {for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;Beam("Palm frond",zone,pos+Vector3.up*height,pos+new Vector3(Mathf.Cos(a)*4,height-.8f,Mathf.Sin(a)*4),.65f,leaf);}}
        else
        {for(int i=0;i<3;i++)Shape("Leaf crown",zone,head,pos+new Vector3((i-1)*.7f,height*.72f+(i%2)*.9f,0),new Vector3(height*.6f,height*.65f,height*.57f),i%2==0?leaf:leafLight);}
    }
    static void Garden(Transform zone)
    {
        Shape("Fountain stone basin",zone,cylinder,new Vector3(0,.2f,0),new Vector3(11,.4f,11),white);
        Shape("Fountain water",zone,cylinder,new Vector3(0,.43f,0),new Vector3(10,.08f,10),glass);
        Shape("Fountain centre",zone,cylinder,new Vector3(0,.9f,0),new Vector3(.75f,1,.75f),concrete);
        for(int i=0;i<6;i++){float a=i*Mathf.PI/3;var p=new Vector3(Mathf.Cos(a)*2,.5f,Mathf.Sin(a)*2);Beam("Fountain water jet",zone,p,p+Vector3.up*(1.3f+(i%2)*.3f),.075f,lamp);}
        for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var p=new Vector3(Mathf.Cos(a)*16,0,Mathf.Sin(a)*16);Tree(zone,p,6+(i%3),false);}
        for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;var p=new Vector3(Mathf.Cos(a)*9,0,Mathf.Sin(a)*9);Box("Park bench",zone,p+Vector3.up*.5f,new Vector3(3,.2f,.65f),bark,Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));}
    }
    static void Cargo(Transform zone)
    {
        Box("Logistics yard",zone,new Vector3(0,.03f,0),new Vector3(48,.06f,46),concrete);
        for(int x=0;x<3;x++)for(int z=0;z<3;z++)for(int level=0;level<2;level++)
        {
            var p=new Vector3(-15+x*8,1.45f+level*2.8f,-14+z*14);var material=(x+z)%3==0?orange:(x+z)%3==1?accent:blue;
            Box("Corrugated cargo container",zone,p,new Vector3(6.4f,2.8f,12.5f),material);
            foreach(int side in new[]{-1,1})for(int rib=0;rib<12;rib++)Box("Container stiffener",zone,p+new Vector3(side*3.23f,0,-5.7f+rib*1.04f),new Vector3(.08f,2.75f,.07f),steel);
            foreach(int bar in new[]{-1,1})Box("Container door bar",zone,p+new Vector3(bar*1.5f,0,6.3f),new Vector3(.08f,2.6f,.05f),steel);
        }
        var truck=Group("Port service truck",zone,new Vector3(16,0,0));Box("Truck chassis",truck,new Vector3(0,.8f,0),new Vector3(3.1f,.6f,12),dark);Box("Cargo trailer",truck,new Vector3(0,2.4f,-1),new Vector3(3.3f,3,10),white);Box("Truck cab",truck,new Vector3(0,2,6),new Vector3(3.2f,3,3),accent);Box("Cab windshield",truck,new Vector3(0,2.6f,7.53f),new Vector3(2.8f,1,.04f),glass);
        foreach(int side in new[]{-1,1})foreach(float z in new[]{-4f,-2f,5f})Shape("Truck wheel",truck,cylinder,new Vector3(side*1.7f,.65f,z),new Vector3(1.2f,.35f,1.2f),dark,Quaternion.Euler(0,0,90));
        Person(zone,new Vector3(21,0,10),Quaternion.identity,orange,true);Person(zone,new Vector3(19,0,-11),Quaternion.identity,yellow);
        FlagAt(zone,new Vector3(22,0,-20),accent);
    }
    static void Promenade(Transform zone,float length)
    {
        Box("Marina boardwalk",zone,new Vector3(0,.09f,0),new Vector3(8,.18f,length),concrete);
        for(int i=0;i<length/8;i++)
        {
            float z=-length*.5f+4+i*8;Shape("Mooring bollard",zone,cylinder,new Vector3(-3.3f,.55f,z),new Vector3(.45f,.8f,.45f),dark);
            Box("Bollard cap",zone,new Vector3(-3.3f,.95f,z),new Vector3(.75f,.15f,.55f),steel);
            if(i>0)Beam("Mooring rope",zone,new Vector3(-3.3f,.65f,z-8),new Vector3(-3.3f,.65f,z),.04f,bark);
            if(i%3==0){Beam("Promenade lamp",zone,new Vector3(3.2f,0,z),new Vector3(3.2f,4.5f,z),.12f,steel);Box("Promenade light",zone,new Vector3(3.2f,4.4f,z),new Vector3(.4f,.35f,.4f),lamp);Tree(zone,new Vector3(7,0,z),6.5f,true);}
            if(i%2==0)Person(zone,new Vector3(1,0,z),Quaternion.Euler(0,90,0),i%4==0?white:accent,i%4==0);
        }
    }
    static void HotelLights()
    {
        var zone=Group("Hotel facade light ribbons",root,Vector3.zero);var centre=new Vector3(105,22,-300);
        zones.Add(new CircuitSceneryMotion.Zone{root=zone.gameObject,centre=centre,radius=100,viewDistance=1500});
        for(int ring=1;ring<5;ring++)for(int i=0;i<48;i++)
        {
            float b=ring*Mathf.PI/12,a=i*Mathf.PI/24,next=(i+1)*Mathf.PI/24;
            Vector3 P(float angle)=>centre+new Vector3(Mathf.Cos(angle)*74.15f*Mathf.Cos(b),Mathf.Sin(b)*20+.08f,Mathf.Sin(angle)*48.15f*Mathf.Cos(b));
            Beam("Hotel LED ribbon",zone,P(a),P(next),.18f,ring%2==0?lamp:accent);
        }
        var skyline=ExactSite("Yas evening skyline",new Vector3(620,1,200),Quaternion.identity,110,1800);
        if(skyline)for(int i=0;i<6;i++)
        {
            var p=new Vector3((i%2)*45-20,0,(i/2)*60-60);float height=25+i*6;
            Box("Skyline tower",skyline,p+Vector3.up*height*.5f,new Vector3(24,height,24),blue);Box("Tower crown",skyline,p+Vector3.up*(height+.8f),new Vector3(26,1.6f,26),white);
            for(int floor=1;floor<height/3;floor++)Box("Illuminated window band",skyline,p+Vector3.up*floor*3,new Vector3(24.2f,.55f,24.2f),glass);
        }
    }
    static void RubberMarks()
    {
        if(!race.circuit.harbor && !race.circuit.yasMarina)return; // Apex already has its detailed rubber surface.
        const int w=64,h=128;var texture=new Texture2D(w,h,TextureFormat.RGBA32,true);var pixels=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){float u=(x-31.5f)/31.5f;float alpha=Mathf.Exp(-u*u*5)*(.07f+Mathf.PerlinNoise(x*.2f,y*.1f)*.13f);pixels[y*w+x]=new Color(.025f,.03f,.035f,alpha);}
        texture.SetPixels(pixels);texture.Apply();string tex=Base+"/Shared/Brake rubber.png";System.IO.File.WriteAllBytes(tex,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(tex);
        rubber=Mat("Brake rubber",Color.white,true);rubber.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        var line=Object.FindFirstObjectByType<DrivingGuide>().qualifyingLine;var selected=new List<int>();
        foreach(int candidate in Enumerable.Range(0,route.points.Length).OrderByDescending(i=>route.CurvatureAhead(i+1,1)))
        {
            if(selected.Any(i=>Mathf.Min(Mathf.Abs(i-candidate),route.points.Length-Mathf.Abs(i-candidate))<45))continue;selected.Add(candidate);if(selected.Count==8)break;
        }
        int index=0;foreach(int corner in selected)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int j=-9;j<=2;j++)
            {
                int i=(corner+j+route.points.Length)%route.points.Length;var p=line.Position(i);var forward=(line.Position(i+1)-line.Position(i-1)).normalized;var right=Vector3.Cross(Vector3.up,forward);
                for(int side=-1;side<=1;side+=2)
                {
                    var q=p+right*side*1.1f;
                    var hits=Physics.RaycastAll(q+Vector3.up*8,Vector3.down,16,~0,QueryTriggerInteraction.Ignore).Where(hit=>hit.collider.name=="Asphalt Surface").OrderBy(hit=>hit.distance).ToArray();
                    if(hits.Length>0)q=hits[0].point+hits[0].normal*.009f;v.Add(q);uv.Add(new Vector2(side<0?0:1,(j+9)*.35f));
                }
                if(j<2){int a=(j+9)*2;t.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
            }
            var mesh=new Mesh{name="Braking rubber"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();var saved=SaveMesh(folder+"/Rubber "+index+".asset",mesh);
            var go=Shape("Braking rubber "+index++,root,saved,Vector3.zero,Vector3.one,rubber);go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
    }
    static void Batch(Transform zone)
    {
        var groups=new Dictionary<(Material,int,int),List<CombineInstance>>();var sources=new List<GameObject>();
        foreach(var f in zone.GetComponentsInChildren<MeshFilter>())
        {
            if(dynamicObjects.Contains(f.gameObject) || f.GetComponent<TMP_Text>())continue;
            var renderer=f.GetComponent<MeshRenderer>();if(!renderer || !f.sharedMesh || f.sharedMesh.subMeshCount!=1)continue;
            int x=Mathf.FloorToInt(f.transform.position.x/128),z=Mathf.FloorToInt(f.transform.position.z/128);var id=(renderer.sharedMaterial,x,z);
            if(!groups.ContainsKey(id))groups[id]=new List<CombineInstance>();groups[id].Add(new CombineInstance{mesh=f.sharedMesh,transform=zone.worldToLocalMatrix*f.transform.localToWorldMatrix});sources.Add(f.gameObject);
        }
        int index=0;foreach(var pair in groups)
        {
            var mesh=new Mesh{name=zone.name+" detail batch "+index,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);var saved=SaveMesh(folder+"/"+zone.name+" batch "+index+".asset",mesh);
            Shape("Static detail batch "+index++,zone,saved,Vector3.zero,Vector3.one,pair.Key.Item1);
        }
        foreach(var source in sources)Object.DestroyImmediate(source);
    }
    static string Signature(RaceManager manager)
    {
        var p=manager.ai.path;var hash=new System.Text.StringBuilder();
        foreach(var point in p.points)hash.Append(point.position.ToString("F4")).Append(point.GetComponent<CircuitCheckpoint>().index);
        foreach(var id in Object.FindObjectsByType<RacerIdentity>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(x=>x.gridSlot))hash.Append(id.gridPosition.ToString("F4")).Append(id.gridCheckpoint);
        return Hash128.Compute(hash.ToString()).ToString();
    }
    [MenuItem("Overdrive/Add Current Circuit Environmental Details")]
    public static string ApplyCurrent()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play before editing track details");
        var scene=EditorSceneManager.GetActiveScene();race=Object.FindFirstObjectByType<RaceManager>();if(!race || !race.circuit)throw new System.InvalidOperationException("Open a selectable racing scene");
        if(!new[]{"Assets/Scenes/OverdriveF1.unity","Assets/Scenes/OverdriveHarbor.unity","Assets/Scenes/OverdriveYasMarina.unity"}.Contains(scene.path))throw new System.InvalidOperationException("Use one of the three playable circuits");
        string before=Signature(race);int collidersBefore=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
        key=race.circuit.yasMarina?"YasMarina":race.circuit.harbor?"HarborRun":"ApexPark";folder=Base+"/"+key;Folder(Base+"/Shared");Folder(folder);
        var previous=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==RootName);if(previous)Object.DestroyImmediate(previous);
        root=new GameObject(RootName).transform;motion=root.gameObject.AddComponent<CircuitSceneryMotion>();motion.race=race;motion.theme=key;motion.protectedHalfWidth=race.circuit.yasMarina?10.5f:12.5f;
        route=race.ai.path;font=race.circuit.ui.startLabel.font;random=new System.Random(key=="ApexPark"?104:key=="HarborRun"?208:312);
        zones.Clear();sites.Clear();flags.Clear();arms.Clear();sway.Clear();lamps.Clear();boards.Clear();dynamicObjects.Clear();
        var temporary=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=temporary.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(temporary);cylinder=LowCylinder();head=Head();flagMesh=Flag();
        dark=Mat("Carbon",new Color(.025f,.035f,.05f));steel=Mat("Brushed steel",new Color(.38f,.43f,.46f));white=Mat("Warm white",new Color(.88f,.91f,.9f));concrete=Mat("Paving",new Color(.33f,.35f,.34f));orange=Mat("Marshal orange",new Color(1,.32f,.025f));red=Mat("Signal red",new Color(.82f,.045f,.07f));skin=Mat("Skin warm",new Color(.67f,.43f,.28f));skinDark=Mat("Skin deep",new Color(.31f,.18f,.12f));blue=Mat("Navy clothing",new Color(.035f,.12f,.24f));yellow=Mat("Safety yellow",new Color(.95f,.7f,.08f));bark=Mat("Natural timber",new Color(.30f,.20f,.12f));leaf=Mat("Deep leaves",new Color(.055f,.19f,.085f));leafLight=Mat("Sunlit leaves",new Color(.18f,.31f,.08f));glass=Mat(key+" glazing",new Color(.035f,.22f,.29f),false,race.circuit.yasMarina);lamp=Mat("Status illumination",Color.white,false,true);
        accent=Mat(key+" accent",race.circuit.yasMarina?new Color(.025f,.62f,.72f):race.circuit.harbor?new Color(.015f,.38f,.42f):new Color(.025f,.42f,.27f));accent.SetFloat("_Cull",0);flagWhite=Mat("White flags",new Color(.88f,.9f,.94f));flagWhite.SetFloat("_Cull",0);
        screen=Mat(key+" live display",Color.white,false,true);var map=CircuitMap();screen.SetTexture("_BaseMap",map);screen.SetTexture("_EmissionMap",map);screen.SetColor("_EmissionColor",Color.white*.8f);
        int stands=0,posts=0,screens=0;
        for(int j=0;j<4;j++)
        {
            int i=Mathf.RoundToInt(route.points.Length*(.17f+j*.2f));int side=j%2==0?1:-1;
            var zone=Site("Fan village "+j,i,side,68,31);if(!zone){side=-side;zone=Site("Fan village "+j,i+12,side,72,31);}
            if(zone)Grandstand(zone,side,++stands);
        }
        for(int j=0;j<14;j++)
        {
            int i=12+j*(route.points.Length-20)/14,side=j%2==0?1:-1;var zone=Site("Marshal post "+j,i,side,30,6);
            if(!zone){side=-side;zone=Site("Marshal post "+j,i+5,side,32,6);}
            if(zone){if(side<0)zone.localRotation*=Quaternion.Euler(0,180,0);MarshalPost(zone,++posts);}
        }
        for(int j=0;j<3;j++)
        {
            int i=Mathf.RoundToInt(route.points.Length*(.10f+j*.31f)),side=j%2==0?-1:1;var zone=Site("Live circuit screen "+j,i,side,38,7,1100);
            if(zone){Screen(zone,side);screens++;}
        }
        var team=Site("Team support compound",route.points.Length-12,race.circuit.harbor?-1:1,74,14);if(team)ServicePad(team);
        if(key=="ApexPark")
        {for(int j=0;j<2;j++){var garden=Site("Park garden "+j,(int)(route.points.Length*(.28f+j*.37f)),j==0?-1:1,110,23,1000);if(garden)Garden(garden);}}
        else if(key=="HarborRun")
        {var cargo=Site("Port logistics yard",(int)(route.points.Length*.32f),1,105,34,1300);if(cargo)Cargo(cargo);var quay=ExactSite("Dockside promenade",new Vector3(-242,1,310),Quaternion.identity,36,1100);if(quay)Promenade(quay,60);}
        else
        {var marina=ExactSite("Marina promenade",new Vector3(284,1,-470),Quaternion.identity,68,1200);if(marina)Promenade(marina,125);HotelLights();}
        RubberMarks();
        foreach(var zone in zones)Batch(zone.root.transform);
        motion.zones=zones.ToArray();motion.placements=sites.ToArray();motion.flagSource=flagMesh;motion.flags=flags.ToArray();motion.waveArms=arms.ToArray();motion.swayObjects=sway.ToArray();motion.marshalLamps=lamps.ToArray();motion.timingBoards=boards.ToArray();
        motion.facades=race.circuit.yasMarina?Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.sharedMaterial && r.sharedMaterial.name=="Hotel violet glow").Cast<Renderer>().ToArray():new Renderer[0];
        if(root.GetComponentsInChildren<Collider>(true).Length>0 || root.GetComponentsInChildren<Rigidbody>(true).Length>0)throw new System.InvalidOperationException("Decorative hierarchy must not contain physical bodies");
        string after=Signature(race);int collidersAfter=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
        if(before!=after || collidersBefore!=collidersAfter)throw new System.InvalidOperationException("Track or physics changed during decorative pass");
        var report=new{passed=true,track=race.CircuitName,signature=after,preservedTrackAndGrid=true,collidersBefore,collidersAfter,stands,marshalPosts=posts,liveScreens=screens,zones=zones.Count,placements=sites.Count,animatedFlags=flags.Count,wavingArms=arms.Count,renderers=root.GetComponentsInChildren<Renderer>(true).Length,triangles=root.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh.triangles.Length/3)};
        EditorUtility.SetDirty(motion);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();System.IO.File.WriteAllText("Docs/"+key+"_EnvironmentalDetails.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return Newtonsoft.Json.JsonConvert.SerializeObject(report);
    }
}
