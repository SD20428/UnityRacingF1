using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Overdrive;
using Object=UnityEngine.Object;

// Trackside visual detail. Never modifies race logic, track surfaces or vehicle tuning.
public static class OverdriveEnvironmentArt
{
    const string Folder="Assets/Art/GrandPrix/Environment";
    static Material concrete, metal, dark, glass, white, red, teal, orange, yellow, lamp, skin;
    static Transform[] route;
    static readonly List<Transform> garagesInScene=new List<Transform>();
    static readonly Dictionary<Transform,float> checkedPlacements=new Dictionary<Transform,float>();
    static readonly HashSet<Transform> newPlacements=new HashSet<Transform>();
    static float Clearance(Vector3 p)
    {
        float best=float.PositiveInfinity; p.y=0;
        for(int i=0;i<route.Length;i++)
        {
            var a=route[i].position; var b=route[(i+1)%route.Length].position; a.y=b.y=0;
            var d=b-a; float u=Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.001f,d.sqrMagnitude));
            best=Mathf.Min(best,Vector3.Distance(p,a+d*u));
        }
        return best;
    }
    static Transform SafeGroup(string name,Transform parent,Vector3 p,Quaternion rotation,float radius)
    {
        // Use segment distance and a footprint radius, including between checkpoint samples.
        if(Clearance(p)<19+radius) return null;
        foreach(var pair in checkedPlacements) if(Vector3.Distance(pair.Key.position,p)<radius+pair.Value+2) return null;
        var result=Group(name,parent,p); result.rotation=rotation; checkedPlacements.Add(result,radius); newPlacements.Add(result); return result;
    }
    static Transform Facility(string name,Transform root,Transform source)
    {
        var t=Group(name,root,source.position-Vector3.up*(source.lossyScale.y*.5f));
        t.rotation=source.rotation; checkedPlacements.Add(t,name.StartsWith("Pit suite")?20:12); return t;
    }
    static Material Mat(string name,Color color,float metallic=0,float smooth=.3f)
    {
        var path=Folder+"/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
        m.color=color; m.SetFloat("_Metallic",metallic); m.SetFloat("_Smoothness",smooth); EditorUtility.SetDirty(m); return m;
    }
    static Transform Group(string name,Transform parent,Vector3 p)
    { var g=new GameObject(name).transform; g.SetParent(parent,false); g.localPosition=p; return g; }
    static Transform Box(string name,Transform parent,Vector3 p,Vector3 scale,Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name; g.transform.SetParent(parent,false); g.transform.localPosition=p; g.transform.localScale=scale;
        g.GetComponent<Renderer>().sharedMaterial=mat; Object.DestroyImmediate(g.GetComponent<Collider>()); return g.transform;
    }
    static void Beam(Transform parent,Vector3 a,Vector3 b,float width,Material mat)
    { var t=Box("Structural beam",parent,(a+b)*.5f,new Vector3(width,(a-b).magnitude,width),mat); t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a); }
    static void Text(string value,Transform parent,Vector3 p,Quaternion rotation,float size,Color color)
    {
        var t=Group(value,parent,p); t.localRotation=rotation; var text=t.gameObject.AddComponent<TextMesh>(); text.text=value; text.fontSize=64;
        text.characterSize=size; text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center; text.color=color;
    }
    static void Garage(Transform root,Transform source,int index)
    {
        var t=Facility("Pit suite "+(index+1),root,source);
        var team=index%2==0?teal:orange;
        Box("Roof parapet",t,new Vector3(0,6.24f,0),new Vector3(20.6f,.5f,18.6f),white);
        Box("Team fascia",t,new Vector3(10.25f,5.72f,0),new Vector3(.3f,.65f,17.8f),team);
        Box("Rebuilt roller shutter",t,new Vector3(10.12f,1.8f,0),new Vector3(.16f,3.5f,14.5f),metal);
        for(int slat=0;slat<15;slat++) Box("Roller shutter slat",t,new Vector3(10.215f,.15f+slat*.235f,0),new Vector3(.035f,.028f,14.4f),dark);
        Text((index+1).ToString("00")+"  /  "+(index%2==0?"APEX MOTORSPORT":"ORBIT RACING"),t,new Vector3(10.42f,5.72f,0),Quaternion.Euler(0,-90,0),.16f,Color.white);
        for(int j=0;j<4;j++)
        {
            float z=-6.5f+j*4.3f;
            Box("Window frame",t,new Vector3(10.22f,4.65f,z),new Vector3(.2f,1.3f,4),metal);
            Box("Glazed pane",t,new Vector3(10.34f,4.65f,z),new Vector3(.08f,1.08f,3.76f),glass);
            Box("Mullion",t,new Vector3(10.4f,4.65f,z),new Vector3(.06f,1.15f,.06f),metal);
        }
        for(int s=-1;s<=1;s+=2)
        {
            Box("Garage pillar",t,new Vector3(10.35f,1.9f,s*7.8f),new Vector3(.5f,3.8f,.45f),white);
            Box("Work light",t,new Vector3(10.58f,3.7f,s*4),new Vector3(.1f,.08f,5.8f),lamp);
            Box("Tool cabinet",t,new Vector3(11.1f,.55f,s*6.5f),new Vector3(.8f,1.1f,1.3f),team);
            for(int drawer=0;drawer<4;drawer++) Box("Drawer handle",t,new Vector3(11.52f,.24f+drawer*.23f,s*6.5f),new Vector3(.035f,.035f,1),metal);
            Box("Pit apron edge",t,new Vector3(13,.025f,s*8.3f),new Vector3(5.2f,.012f,.09f),yellow);
            Box("Roof HVAC",t,new Vector3(-3,6.8f,s*5),new Vector3(3.2f,.8f,2.4f),metal);
            for(int fin=0;fin<7;fin++) Box("HVAC louvre",t,new Vector3(-3,7.22f,s*5-.85f+fin*.28f),new Vector3(2.9f,.03f,.055f),dark);
        }
        Box("Roof solar panel",t,new Vector3(3,6.58f,0),new Vector3(4,.12f,12),glass);
        for(int k=0;k<6;k++) Box("Panel frame",t,new Vector3(3,6.66f,-5+k*2),new Vector3(4.05f,.04f,.035f),metal);
        Box("Apron",t,new Vector3(13,-.025f,0),new Vector3(5.5f,.04f,17.8f),concrete);
        Box("Pit stop line",t,new Vector3(14,.005f,0),new Vector3(3.7f,.012f,.1f),white);
        Text("PIT "+(index+1).ToString("00"),t,new Vector3(14,.015f,-2),Quaternion.Euler(90,-90,0),.20f,Color.white);
        // Pit gantry, overhead air lines, telemetry screens and fire-point signs.
        Beam(t,new Vector3(10.8f,0,-5),new Vector3(10.8f,3.5f,-5),.09f,metal);
        Beam(t,new Vector3(10.8f,3.5f,-5),new Vector3(15,3.5f,-5),.09f,metal);
        Beam(t,new Vector3(14.8f,3.45f,-5),new Vector3(14.8f,1.05f,-5),.035f,dark);
        Box("Telemetry screen frame",t,new Vector3(10.65f,2.2f,6.1f),new Vector3(.18f,.7f,1.1f),dark);
        Box("Telemetry screen",t,new Vector3(10.76f,2.2f,6.1f),new Vector3(.025f,.55f,.94f),glass);
        for(int row=0;row<3;row++) Box("Telemetry trace",t,new Vector3(10.78f,2.02f+row*.16f,6.1f),new Vector3(.015f,.025f,.70f-row*.13f),teal);
        Box("Extinguisher station",t,new Vector3(10.8f,.8f,-7),new Vector3(.25f,.8f,.45f),red);
        Text("F",t,new Vector3(10.94f,.85f,-7),Quaternion.Euler(0,-90,0),.11f,Color.white);
        for(int line=0;line<14;line++)
            Box("Drainage grate",t,new Vector3(10.4f,.018f,-7.5f+line*1.1f),new Vector3(.34f,.012f,.045f),dark);
        Transporter(root,source,index);
    }
    static void Stand(Transform root,Transform source,int index)
    {
        source.GetComponent<Renderer>().enabled=false;
        var t=Facility("Spectator stand "+(index+1),root,source);
        for(int row=0;row<6;row++)
        {
            float x=-5.3f+row*1.85f,y=.45f+row*.64f;
            Box("Concrete terrace",t,new Vector3(x,y-.18f,0),new Vector3(1.85f,.36f,16),concrete);
            for(int seat=0;seat<18;seat++)
            {
                if(seat==8||seat==9) continue;
                float z=-7.4f+seat*.86f; var paint=(seat+row+index)%5==0?white:(index%2==0?teal:red);
                Box("Seat pan",t,new Vector3(x,y+.19f,z),new Vector3(.55f,.12f,.57f),paint);
                Box("Seat back",t,new Vector3(x+.27f,y+.46f,z),new Vector3(.1f,.57f,.57f),paint);
                // Sparse stylized spectators remain static, deterministic scenery.
                if((seat*7+row*3+index)%4==0)
                {
                    Box("Spectator jacket",t,new Vector3(x,y+.55f,z),new Vector3(.30f,.52f,.34f),(seat%3==0?orange:dark));
                    var head=GameObject.CreatePrimitive(PrimitiveType.Sphere); head.name="Spectator head"; head.transform.SetParent(t,false);
                    head.transform.localPosition=new Vector3(x-.015f,y+.91f,z); head.transform.localScale=new Vector3(.23f,.26f,.23f); head.GetComponent<Renderer>().sharedMaterial=skin; Object.DestroyImmediate(head.GetComponent<Collider>());
                    for(int arm=-1;arm<=1;arm+=2) Beam(t,new Vector3(x,y+.71f,z+arm*.2f),new Vector3(x-.23f,y+.42f,z+arm*.19f),.095f,(seat%3==0?orange:dark));
                }
            }
            Box("Central stair",t,new Vector3(x,y+.025f,0),new Vector3(1.85f,.05f,1.2f),white);
        }
        for(int s=-1;s<=1;s+=2)
        {
            for(int j=0;j<4;j++) Beam(t,new Vector3(-5+j*3.4f,0,s*8.2f),new Vector3(-5+j*3.4f,6.4f,s*8.2f),.14f,metal);
            Beam(t,new Vector3(-5.5f,1.3f,s*8.1f),new Vector3(5.5f,5.1f,s*8.1f),.09f,metal);
            Beam(t,new Vector3(-5.5f,0,s*8.2f),new Vector3(5.5f,6.4f,s*8.2f),.10f,metal);
        }
        Box("Canopy roof",t,new Vector3(0,6.6f,0),new Vector3(13.5f,.18f,18.6f),white);
        Box("Canopy edge",t,new Vector3(-6.8f,6.35f,0),new Vector3(.16f,.5f,18.6f),red);
        Text("APEX PARK  /  "+(index+1).ToString("00"),t,new Vector3(-6.9f,6.35f,0),Quaternion.Euler(0,90,0),.15f,Color.white);
        for(int j=0;j<6;j++) Beam(t,new Vector3(-6.6f,6.47f,-8+j*3.2f),new Vector3(6.6f,6.47f,-8+j*3.2f),.1f,metal);
        for(int side=-1;side<=1;side+=2)
        {
            for(int step=0;step<12;step++)
                Box("Aisle step nosing",t,new Vector3(-5.65f+step*.93f,.53f+step*.32f,side*.57f),new Vector3(.07f,.03f,.05f),yellow);
            Beam(t,new Vector3(-5.5f,1.4f,side*.72f),new Vector3(5,5.1f,side*.72f),.045f,metal);
            for(int k=0;k<6;k++) Beam(t,new Vector3(-5.5f+k*2.1f,.5f+k*.72f,side*.72f),new Vector3(-5.5f+k*2.1f,1.4f+k*.72f,side*.72f),.035f,metal);
            Box("Exit sign",t,new Vector3(5.7f,5.7f,side*6.8f),new Vector3(.1f,.45f,1.6f),teal);
            Text("EXIT >",t,new Vector3(5.64f,5.7f,side*6.8f),Quaternion.Euler(0,90,0),.085f,Color.white);
        }
        for(int k=0;k<4;k++) Box("Canopy light",t,new Vector3(-2.8f,6.47f,-6+k*4),new Vector3(2,.035f,.14f),lamp);
    }
    static void Tower(Transform root)
    {
        var last=garagesInScene[garagesInScene.Count-1];
        var t=SafeGroup("Race control tower",root,last.position+last.forward*32-Vector3.up*3,last.rotation,10);
        if(!t) return;
        Box("Tower core",t,new Vector3(0,5.5f,0),new Vector3(9,11,10),concrete);
        Box("Control room",t,new Vector3(0,11.7f,0),new Vector3(13,3.4f,13),dark);
        foreach(int s in new[]{-1,1})
        {
            Box("Panoramic glazing",t,new Vector3(s*6.55f,11.8f,0),new Vector3(.08f,2.5f,12.5f),glass);
            Box("Panoramic glazing",t,new Vector3(0,11.8f,s*6.55f),new Vector3(12.5f,2.5f,.08f),glass);
            for(int k=0;k<6;k++) { Box("Window frame",t,new Vector3(s*6.62f,11.8f,-5+k*2),new Vector3(.08f,2.7f,.065f),metal); }
        }
        Box("Roof visor",t,new Vector3(0,13.6f,0),new Vector3(14,.3f,14),white);
        Beam(t,new Vector3(-2,13.8f,0),new Vector3(-2,18,0),.09f,metal);
        Beam(t,new Vector3(-3,17,0),new Vector3(-1,17,0),.06f,metal);
        Text("RACE CONTROL",t,new Vector3(4.56f,8.4f,0),Quaternion.Euler(0,-90,0),.22f,Color.white);
        Box("Timing display",t,new Vector3(4.65f,5.2f,0),new Vector3(.2f,4.5f,7),dark);
        Text("RACE CONTROL\nCIRCUIT STATUS\nTRACK CLEAR",t,new Vector3(4.78f,5.2f,0),Quaternion.Euler(0,-90,0),.17f,new Color(.7f,1,.7f));
    }
    static void Cylinder(string name,Transform parent,Vector3 p,Vector3 size,Material mat,Quaternion rotation)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name=name; go.transform.SetParent(parent,false);
        go.transform.localPosition=p; go.transform.localScale=size; go.transform.localRotation=rotation;
        go.GetComponent<Renderer>().sharedMaterial=mat; Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static void Transporter(Transform root,Transform garage,int index)
    {
        var pos=garage.position-garage.right*38-Vector3.up*3;
        var t=SafeGroup("Team transporter "+index,root,pos,garage.rotation,12); if(!t) return;
        var team=index%2==0?teal:orange;
        Box("Service parking bay",t,new Vector3(0,.005f,0),new Vector3(11,.02f,20),concrete);
        Box("Transporter trailer",t,new Vector3(0,2.1f,-1.5f),new Vector3(2.6f,3.3f,12),white);
        Box("Trailer colour band",t,new Vector3(1.315f,2.5f,-1.5f),new Vector3(.03f,.65f,11.8f),team);
        Text(index%2==0?"APEX MOTORSPORT":"ORBIT RACING",t,new Vector3(1.34f,2.5f,-1.5f),Quaternion.Euler(0,-90,0),.10f,Color.white);
        Box("Tractor cab",t,new Vector3(0,1.7f,6.25f),new Vector3(2.5f,2.6f,2.8f),team);
        Box("Cab windshield",t,new Vector3(0,2.15f,7.67f),new Vector3(2.2f,.95f,.04f),glass);
        Box("Front grille",t,new Vector3(0,1,7.68f),new Vector3(1.5f,.45f,.05f),dark);
        for(int s=-1;s<=1;s+=2)
        {
            Box("Headlight",t,new Vector3(s*.9f,1,7.71f),new Vector3(.3f,.17f,.05f),lamp);
            foreach(float z in new[]{-5.5f,-4.1f,4.8f,6.5f}) Cylinder("Transporter wheel",t,new Vector3(s*1.28f,.6f,z),new Vector3(1.05f,.16f,1.05f),dark,Quaternion.Euler(0,0,90));
        }
        Box("Equipment flight case",t,new Vector3(3,.6f,-4),new Vector3(1.1f,1.2f,2.2f),dark);
        for(int side=-1;side<=1;side+=2) Box("Case corner guard",t,new Vector3(3+side*.52f,.6f,-4),new Vector3(.08f,1.25f,2.25f),metal);
    }
    static void SupportCampus(Transform root)
    {
        var garage=garagesInScene[garagesInScene.Count/2];
        var t=SafeGroup("Medical centre and helipad",root,garage.position-garage.right*72+garage.forward*28-Vector3.up*3,garage.rotation,25);
        if(!t) return;
        Box("Medical centre",t,new Vector3(0,2.2f,0),new Vector3(13,4.4f,10),white);
        Box("Medical canopy",t,new Vector3(8,3.7f,0),new Vector3(5,.18f,11),metal);
        Box("Medical entrance",t,new Vector3(6.56f,1.4f,0),new Vector3(.08f,2.8f,3.2f),glass);
        Box("Medical cross vertical",t,new Vector3(6.63f,3.5f,0),new Vector3(.04f,1.1f,.32f),red);
        Box("Medical cross horizontal",t,new Vector3(6.65f,3.5f,0),new Vector3(.04f,.32f,1.1f),red);
        Box("Medical canopy fascia",t,new Vector3(10.55f,3.65f,0),new Vector3(.14f,.55f,11),white);
        Text("MEDICAL CENTRE",t,new Vector3(10.64f,3.65f,0),Quaternion.Euler(0,-90,0),.14f,Color.black);
        for(int sign=-1;sign<=1;sign+=2)
        {
            Box("Medical canopy cross",t,new Vector3(10.66f,3.65f,sign*4.3f),new Vector3(.03f,.34f,.1f),red);
            Box("Medical canopy cross",t,new Vector3(10.67f,3.65f,sign*4.3f),new Vector3(.03f,.1f,.34f),red);
            Beam(t,new Vector3(10.1f,0,sign*4.7f),new Vector3(10.1f,3.5f,sign*4.7f),.12f,metal);
            Box("Medical side glazing",t,new Vector3(0,2.5f,sign*5.06f),new Vector3(8,1.2f,.06f),glass);
            for(int frame=0;frame<5;frame++) Box("Medical window mullion",t,new Vector3(-4+frame*2,2.5f,sign*5.11f),new Vector3(.055f,1.3f,.06f),metal);
        }
        Box("Access apron",t,new Vector3(7,.005f,0),new Vector3(12,.02f,16),concrete);
        var ambulance=Group("Standby ambulance",t,new Vector3(10,0,3));
        Box("Ambulance body",ambulance,new Vector3(0,1.2f,0),new Vector3(2,2.1f,4.8f),white);
        Box("Ambulance windshield",ambulance,new Vector3(0,1.6f,2.42f),new Vector3(1.8f,.7f,.04f),glass);
        Box("Ambulance light bar",ambulance,new Vector3(0,2.35f,1.5f),new Vector3(1.5f,.15f,.25f),teal);
        for(int side=-1;side<=1;side+=2)
        {
            Box("Ambulance safety stripe",ambulance,new Vector3(side*1.02f,.9f,0),new Vector3(.03f,.35f,4.6f),yellow);
            foreach(float z in new[]{-1.5f,1.5f}) Cylinder("Ambulance wheel",ambulance,new Vector3(side*.95f,.42f,z),new Vector3(.72f,.13f,.72f),dark,Quaternion.Euler(0,0,90));
        }
        Cylinder("Helipad paving",t,new Vector3(-14,.012f,0),new Vector3(18,.015f,18),concrete,Quaternion.identity);
        for(int i=0;i<64;i++)
        {
            float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;
            Beam(t,new Vector3(-14+Mathf.Cos(a)*7,.035f,Mathf.Sin(a)*7),new Vector3(-14+Mathf.Cos(b)*7,.035f,Mathf.Sin(b)*7),.13f,yellow);
        }
        Box("Helipad H upright",t,new Vector3(-16,.045f,0),new Vector3(.4f,.025f,5),white);
        Box("Helipad H upright",t,new Vector3(-12,.045f,0),new Vector3(.4f,.025f,5),white);
        Box("Helipad H crossbar",t,new Vector3(-14,.045f,0),new Vector3(4,.025f,.4f),white);
    }
    static void Screen(Transform root,int index)
    {
        var p=route[index];
        var t=SafeGroup("Spectator video screen "+index,root,p.position+p.right*34,Quaternion.LookRotation(p.right,Vector3.up),9);
        if(!t) return;
        for(int s=-1;s<=1;s+=2) Box("Screen column",t,new Vector3(s*3,3,0),new Vector3(.35f,6,.4f),metal);
        Box("LED screen surround",t,new Vector3(0,7,0),new Vector3(12,6,.55f),dark);
        Box("Display glass",t,new Vector3(0,7,-.3f),new Vector3(11.5f,5.5f,.03f),glass);
        Text("APEX PARK",t,new Vector3(0,8.6f,-.33f),Quaternion.identity,.32f,Color.white);
        Text("GRAND PRIX\nRACE WEEKEND",t,new Vector3(0,7,-.33f),Quaternion.identity,.22f,Color.white);
        for(int stripe=0;stripe<12;stripe++) Box("LED display grille",t,new Vector3(0,4.6f+stripe*.4f,-.35f),new Vector3(11.4f,.015f,.012f),dark);
        Box("Screen lower accent",t,new Vector3(0,4.35f,-.36f),new Vector3(11.5f,.16f,.02f),red);
        Beam(t,new Vector3(-3,0,.4f),new Vector3(3,5,.4f),.13f,metal);
    }
    static void ParcFerme(Transform root)
    {
        var anchor=garagesInScene[0];
        var t=SafeGroup("Parc ferme and scrutineering",root,anchor.position-anchor.right*36-anchor.forward*35-Vector3.up*3,anchor.rotation,16);
        if(!t) return;
        Box("Inspection compound paving",t,new Vector3(0,.005f,0),new Vector3(20,.02f,23),concrete);
        for(int side=-1;side<=1;side+=2)
        {
            Beam(t,new Vector3(side*10,1.2f,-11.5f),new Vector3(side*10,1.2f,11.5f),.055f,metal);
            Beam(t,new Vector3(side*10,.65f,-11.5f),new Vector3(side*10,.65f,11.5f),.055f,metal);
            for(int k=0;k<10;k++) Beam(t,new Vector3(side*10,0,-11.5f+k*2.55f),new Vector3(side*10,1.5f,-11.5f+k*2.55f),.045f,metal);
        }
        for(int bay=0;bay<3;bay++)
        {
            Box("Weighbridge platform",t,new Vector3(-6+bay*6,.08f,-1),new Vector3(3,.14f,6),metal);
            Box("Weighbridge edge",t,new Vector3(-7.55f+bay*6,.09f,-1),new Vector3(.08f,.02f,6),yellow);
            Box("Inspection display",t,new Vector3(-6+bay*6,1.35f,-5),new Vector3(.8f,.5f,.14f),dark);
            Beam(t,new Vector3(-6+bay*6,0,-5),new Vector3(-6+bay*6,1.2f,-5),.08f,metal);
            Text("BAY "+(bay+1),t,new Vector3(-6+bay*6,.03f,5),Quaternion.Euler(90,0,0),.14f,Color.white);
        }
        for(int side=-1;side<=1;side+=2) Beam(t,new Vector3(side*8,0,10),new Vector3(side*8,3.8f,10),.14f,metal);
        Box("Inspection entry sign",t,new Vector3(0,3.5f,10),new Vector3(16,.65f,.2f),dark);
        Text("PARC FERME / SCRUTINEERING",t,new Vector3(0,3.5f,10.12f),Quaternion.Euler(0,180,0),.16f,Color.white);
    }
    static void ServiceRoad(Transform root)
    {
        for(int i=0;i<garagesInScene.Count-1;i++)
        {
            var a=garagesInScene[i].position-garagesInScene[i].right*18;
            var b=garagesInScene[i+1].position-garagesInScene[i+1].right*18; a.y=b.y=.015f;
            bool clear=true; int steps=Mathf.CeilToInt(Vector3.Distance(a,b)/4);
            for(int s=0;s<=steps;s++) if(Clearance(Vector3.Lerp(a,b,s/(float)steps))<24) clear=false;
            if(!clear) continue;
            var t=Group("Paddock service road "+i,root,(a+b)*.5f); t.rotation=Quaternion.LookRotation(b-a,Vector3.up);
            float length=Vector3.Distance(a,b); Box("Service tarmac",t,Vector3.zero,new Vector3(6,.025f,length),dark);
            for(float z=-length*.5f+2;z<length*.5f;z+=6) Box("Service road dashed line",t,new Vector3(0,.021f,z),new Vector3(.10f,.012f,2.5f),white);
            for(int side=-1;side<=1;side+=2) Box("Service road edging",t,new Vector3(side*2.8f,.023f,0),new Vector3(.09f,.012f,length),yellow);
        }
    }
    // Merge within each facility, keeping its pivot and allowing independent relocation/culling.
    static int Batch(Transform root)
    {
        var groups=new Dictionary<Material,List<CombineInstance>>(); var remove=new List<GameObject>();
        foreach(var f in root.GetComponentsInChildren<MeshFilter>())
        {
            var r=f.GetComponent<MeshRenderer>(); if(!r || !f.sharedMesh) continue;
            var m=r.sharedMaterial; if(!groups.ContainsKey(m)) groups[m]=new List<CombineInstance>();
            groups[m].Add(new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}); remove.Add(f.gameObject);
        }
        foreach(var pair in groups)
        {
            var mesh=new Mesh {name="Environment "+pair.Key.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32}; mesh.CombineMeshes(pair.Value.ToArray(),true,true); mesh.RecalculateBounds();
            string path=Folder+"/Batch "+root.name+" "+pair.Key.name+".asset"; var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved) { EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh); mesh=saved; } else AssetDatabase.CreateAsset(mesh,path);
            var g=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer)); g.transform.SetParent(root,false); g.GetComponent<MeshFilter>().sharedMesh=mesh; g.GetComponent<MeshRenderer>().sharedMaterial=pair.Key; g.isStatic=true;
        }
        foreach(var go in remove) Object.DestroyImmediate(go); return groups.Count;
    }
    [MenuItem("Overdrive/Detail Circuit Environment")]
    public static string Apply()
    {
        var scene=EditorSceneManager.GetActiveScene(); if(EditorApplication.isPlaying || scene.path!="Assets/Scenes/OverdriveF1.unity") throw new System.InvalidOperationException("Open F1 scene in edit mode.");
        var race=Object.FindFirstObjectByType<RaceManager>();
        var circuit=GameObject.Find("F1 Circuit - Apex Park");
        if(!race || !race.ai || !race.ai.path || race.ai.path.points.Length<3 || !circuit) throw new System.InvalidOperationException("Complete track layout required before detailing.");
        route=race.ai.path.points; garagesInScene.Clear(); checkedPlacements.Clear(); newPlacements.Clear();
        foreach(Transform t in circuit.transform) if(t.name=="Pit garage") garagesInScene.Add(t);
        if(garagesInScene.Count==0) throw new System.InvalidOperationException("Pit building anchors required.");
        System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        concrete=Mat("Architectural concrete",new Color(.37f,.4f,.42f)); metal=Mat("Brushed steel",new Color(.33f,.38f,.42f),.65f,.42f);
        dark=Mat("Graphite",new Color(.025f,.035f,.045f)); glass=Mat("Smoked blue glass",new Color(.055f,.15f,.21f),.4f,.7f);
        white=Mat("Porcelain",new Color(.85f,.89f,.9f)); red=Mat("Circuit red",new Color(.78f,.025f,.045f)); teal=Mat("Team teal",new Color(.02f,.43f,.46f)); orange=Mat("Team orange",new Color(.95f,.22f,.035f)); yellow=Mat("Safety yellow",new Color(1,.7f,.03f));
        skin=Mat("Spectator skin",new Color(.65f,.43f,.30f));
        lamp=Mat("Work light",new Color(.82f,.92f,1)); lamp.EnableKeyword("_EMISSION"); lamp.SetColor("_EmissionColor",new Color(.6f,.8f,1)*1.4f);
        var old=GameObject.Find("Detailed Circuit Facilities"); if(old) Object.DestroyImmediate(old);
        var root=Group("Detailed Circuit Facilities",null,Vector3.zero);
        int garages=0,stands=0;
        foreach(Transform child in circuit.transform)
        { if(child.name=="Pit garage") Garage(root,child,garages++); if(child.name=="Grandstand") Stand(root,child,stands++); }
        foreach(Transform child in circuit.transform) if(child.name=="Garage trim") { var r=child.GetComponent<Renderer>(); if(r) r.enabled=false; }
        var refined=GameObject.Find("Refined Circuit");
        if(refined) foreach(Transform t in refined.transform) if(t.name=="Seating Tier"||t.name=="Stand Canopy"||t.name=="Pit Window"||t.name=="Garage Shutter"||t.name=="Shutter Rib") { var r=t.GetComponent<Renderer>(); if(r) r.enabled=false; }
        int fenceIndex=0; Transform fenceGroup=null;
        if(refined) foreach(Transform post in refined.transform) if(post.name=="Fence Post")
        {
            if(fenceIndex%16==0) fenceGroup=Group("Fence furniture section "+(fenceIndex/16),root,post.position);
            var top=fenceGroup.InverseTransformPoint(post.position+post.up*post.lossyScale.y);
            Beam(fenceGroup,top,top+Vector3.up*.42f+post.right*.32f,.045f,metal);
            Box("Fence mounting bracket",fenceGroup,fenceGroup.InverseTransformPoint(post.position),new Vector3(.14f,.10f,.14f),metal); fenceIndex++;
        }
        Tower(root); SupportCampus(root); ParcFerme(root); ServiceRoad(root);
        var points=route;
        int posts=0;
        for(int i=12;i<points.Length;i+=32)
        {
            var p=points[i]; var pos=p.position+p.right*26; pos.y=0;
            var t=SafeGroup("Marshal and lighting post "+posts,root,pos,p.rotation,5); if(!t) continue;
            Box("Marshal hut",t,new Vector3(0,1.2f,0),new Vector3(2.3f,2.4f,2),white);
            Box("Viewing window",t,new Vector3(-1.17f,1.6f,0),new Vector3(.05f,.8f,1.6f),glass);
            Box("Orange roof",t,new Vector3(0,2.5f,0),new Vector3(2.8f,.18f,2.5f),orange);
            Box("Safety stripe",t,new Vector3(-1.2f,.55f,0),new Vector3(.04f,.25f,2),red);
            Box("Fire equipment cabinet",t,new Vector3(-1.2f,.7f,.5f),new Vector3(.16f,.7f,.4f),red);
            Box("Cabinet latch",t,new Vector3(-1.3f,.7f,.5f),new Vector3(.03f,.14f,.035f),metal);
            Beam(t,new Vector3(2,0,0),new Vector3(2,9,0),.13f,metal);
            Beam(t,new Vector3(2,9,0),new Vector3(-1,9,0),.12f,metal);
            Box("Floodlight housing",t,new Vector3(-1,8.9f,0),new Vector3(1.3f,.22f,.7f),dark);
            Box("LED array",t,new Vector3(-1,8.77f,0),new Vector3(1.1f,.03f,.52f),lamp);
            for(int stack=0;stack<4;stack++) for(int layer=0;layer<3;layer++)
                Cylinder("Recovery tyre stack",t,new Vector3(-1.3f+stack*.8f,.16f+layer*.29f,2.5f),new Vector3(.76f,.14f,.76f),dark,Quaternion.identity);
            Box("Tyre retaining strap",t,new Vector3(.0f,.48f,2.08f),new Vector3(3.3f,.08f,.035f),white);
            Beam(t,new Vector3(1.3f,1.5f,0),new Vector3(1.3f,3.9f,0),.045f,metal);
            Box("CCTV housing",t,new Vector3(1.3f,4,.1f),new Vector3(.24f,.20f,.5f),white);
            Box("CCTV lens",t,new Vector3(1.3f,4,.36f),new Vector3(.14f,.13f,.025f),glass);
            Text("M"+(posts+1).ToString("00"),t,new Vector3(-1.22f,2.13f,0),Quaternion.Euler(0,90,0),.13f,Color.black); posts++;
        }
        for(int i=65;i<points.Length;i+=100) Screen(root,i);
        int batches=0; var scopes=new List<Transform>(); foreach(Transform scope in root) scopes.Add(scope);
        foreach(var scope in scopes) batches+=Batch(scope);
        float length=0; for(int i=0;i<route.Length;i++) length+=Vector3.Distance(route[i].position,route[(i+1)%route.Length].position);
        float minimum=float.PositiveInfinity; foreach(var t in newPlacements) minimum=Mathf.Min(minimum,Clearance(t.position)-checkedPlacements[t]);
        string result=$"lengthM={length:F2}; gates={route.Length}; pitSuites={garages}; stands={stands}; seats={stands*96}; marshalPosts={posts}; facilityGroups={scopes.Count}; spatialMaterialBatches={batches}; addedColliders={root.GetComponentsInChildren<Collider>().Length}; minimumNewFacilityFootprintClearanceM={minimum:F2}";
        System.IO.File.WriteAllText("Docs/EnvironmentArt_LayoutAudit.txt",result);
        Group("Layout signature "+route.Length+" gates",root,Vector3.zero);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); return result;
    }
    public static string Preview(bool stands=false)
    {
        var scene=EditorSceneManager.GetActiveScene(); Transform anchor=null; int found=0;
        foreach(Transform t in GameObject.Find("F1 Circuit - Apex Park").transform) if(t.name==(stands?"Grandstand":"Pit garage")) { anchor=t; if(found++==4) break; }
        var basePos=anchor.position-Vector3.up*(anchor.lossyScale.y*.5f);
        var go=new GameObject("Facility preview"); var cam=go.AddComponent<Camera>(); cam.transform.position=basePos+anchor.rotation*(stands?new Vector3(-28,13,-22):new Vector3(33,17,-26)); cam.transform.LookAt(basePos+Vector3.up*3); cam.fieldOfView=57; cam.farClipPlane=2500;
        var rt=RenderTexture.GetTemporary(1440,900,24); var previous=RenderTexture.active;
        try { cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt; var tex=new Texture2D(1440,900,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,1440,900),0,0); tex.Apply(); System.IO.File.WriteAllBytes(stands?"Docs/Circuit_Stands_Preview.png":"Docs/Circuit_Facilities_Preview.png",tex.EncodeToPNG()); Object.DestroyImmediate(tex); }
        finally { RenderTexture.active=previous; cam.targetTexture=null; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(go); }
        return "Docs/Circuit_Facilities_Preview.png";
    }
    public static string PreviewSupport()
    {
        var target=GameObject.Find("Detailed Circuit Facilities").transform.Find("Medical centre and helipad");
        if(!target) return "Medical campus skipped because no clear footprint was available.";
        var go=new GameObject("Support facility preview"); var cam=go.AddComponent<Camera>();
        cam.transform.position=target.TransformPoint(new Vector3(32,28,32)); cam.transform.LookAt(target.TransformPoint(new Vector3(-5,0,0))); cam.fieldOfView=52; cam.farClipPlane=2500;
        var rt=RenderTexture.GetTemporary(1440,900,24); var previous=RenderTexture.active;
        try { cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt; var tex=new Texture2D(1440,900,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,1440,900),0,0); tex.Apply(); System.IO.File.WriteAllBytes("Docs/Circuit_Support_Preview.png",tex.EncodeToPNG()); Object.DestroyImmediate(tex); }
        finally { RenderTexture.active=previous; cam.targetTexture=null; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(go); }
        return "Support preview saved";
    }
}
