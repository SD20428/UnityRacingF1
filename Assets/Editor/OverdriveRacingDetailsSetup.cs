using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Overdrive;
using Object=UnityEngine.Object;

public static class OverdriveRacingDetailsSetup
{
    const string Folder="Assets/Art/RacingDetails";
    static TMP_FontAsset font;
    static readonly Color White=new Color(.9f,.94f,.98f),Muted=new Color(.55f,.64f,.73f),Red=new Color(.96f,.065f,.13f);
    static T Ensure<T>(GameObject go) where T:Component {var t=go.GetComponent<T>();return t?t:go.AddComponent<T>();}
    static Transform Group(string name,Transform parent,bool ui=false)
    {
        var found=parent.Find(name);if(found)return found;
        var go=ui?new GameObject(name,typeof(RectTransform)):new GameObject(name);go.transform.SetParent(parent,false);return go.transform;
    }
    static void Place(RectTransform r,float x,float y,float w,float h)
    {r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    static UnityEngine.UI.Image Image(string name,Transform parent,Color color,float x,float y,float w,float h)
    {var t=Group(name,parent,true);Place((RectTransform)t,x,y,w,h);var image=Ensure<UnityEngine.UI.Image>(t.gameObject);image.color=color;image.raycastTarget=false;return image;}
    static TMP_Text Text(string name,Transform parent,string value,float x,float y,float w,float h,float size,Color color)
    {
        var t=Group(name,parent,true);Place((RectTransform)t,x,y,w,h);var text=Ensure<TextMeshProUGUI>(t.gameObject);
        text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;return text;
    }
    static Texture2D Texture(string name,bool stripe)
    {
        string path=Folder+"/"+name+".png";var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing)return existing;
        var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
        {
            float u=(x+.5f)/32*2-1,v=(y+.5f)/32*2-1;
            float alpha=stripe?Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(v)),1.5f):Mathf.Pow(Mathf.Clamp01(1-Mathf.Sqrt(u*u+v*v)),1.5f);
            texture.SetPixel(x,y,new Color(1,1,1,alpha));
        }
        texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Material Material(string name,bool particle,bool additive,Color color,Texture2D texture=null)
    {
        string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material)return material;
        material=new Material(Shader.Find(particle?"Universal Render Pipeline/Particles/Unlit":"Universal Render Pipeline/Unlit"));material.name=name;
        material.SetFloat("_Surface",1);material.SetFloat("_Blend",additive?2:0);material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",additive?1:10);material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);
        material.SetColor("_BaseColor",color);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;
        if(texture)material.SetTexture("_BaseMap",texture);material.enableInstancing=!particle;AssetDatabase.CreateAsset(material,path);return material;
    }
    static Mesh Disc()
    {
        string path=Folder+"/Brake heat ring.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved)return saved;
        var mesh=new Mesh{name="Brake heat ring"};var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int i=0;i<33;i++){float angle=i*Mathf.PI*2/32;var point=new Vector3(0,Mathf.Cos(angle),Mathf.Sin(angle));vertices.Add(point);vertices.Add(point*.74f);}
        for(int i=0;i<32;i++){int v=i*2;triangles.AddRange(new[]{v,v+2,v+1,v+1,v+2,v+3});}
        mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static ParticleSystem Particles(string name,Transform parent,Material material,bool dust)
    {
        var t=Group(name,parent);var particles=Ensure<ParticleSystem>(t.gameObject);particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.playOnAwake=false;main.loop=dust;main.duration=dust?3:1;main.maxParticles=dust?96:80;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(dust?.65f:.12f,dust?1.1f:.32f);main.startSpeed=new ParticleSystem.MinMaxCurve(dust?.8f:3,dust?2.8f:7);
        main.startSize=new ParticleSystem.MinMaxCurve(dust?.12f:.02f,dust?.36f:.045f);main.gravityModifier=dust?-.04f:1.2f;main.startColor=Color.white;
        var emission=particles.emission;emission.rateOverTime=0;var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=dust?20:25;shape.radius=dust?.45f:.015f;
        var color=particles.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
        gradient.SetKeys(dust?new[]{new GradientColorKey(new Color(.65f,.54f,.36f),0),new GradientColorKey(new Color(.43f,.38f,.28f),1)}:new[]{new GradientColorKey(new Color(1,.95f,.6f),0),new GradientColorKey(new Color(1,.28f,.025f),1)},new[]{new GradientAlphaKey(dust?0:1,0),new GradientAlphaKey(dust?.28f:1,.15f),new GradientAlphaKey(0,1)});color.color=gradient;
        if(dust){var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,3));}
        var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        renderer.renderMode=dust?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Stretch;renderer.lengthScale=2;renderer.velocityScale=.15f;
        EditorUtility.SetDirty(particles);return particles;
    }
    [MenuItem("Overdrive/Add Racing Surface And Telemetry Details")]
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first");
        var ui=Object.FindFirstObjectByType<RaceUI>();var scene=EditorSceneManager.GetActiveScene();
        if(!ui || scene.path!="Assets/Scenes/OverdriveF1.unity")throw new System.InvalidOperationException("Open F1 scene");
        font=ui.startButton.GetComponentInChildren<TMP_Text>().font;
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Art","RacingDetails");
        var soft=Texture("Soft particle",false);var strip=Texture("Soft tyre stripe",true);
        var dustMaterial=Material("Gravel dust",true,false,Color.white,soft);var sparkMaterial=Material("Underbody sparks",true,true,Color.white,soft);
        var trailMaterial=Material("Rubber marks",true,false,new Color(.035f,.028f,.025f,.45f),strip);var glowMaterial=Material("Hot brake discs",false,true,Color.white);var disc=Disc();
        int cars=0;
        foreach(var car in Object.FindObjectsByType<OverdriveVehicle>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var detail=Ensure<VehicleSurfaceDetails>(car.gameObject);var root=Group("Surface details",car.transform);
            detail.dust=Particles("Loose surface dust",root,dustMaterial,true);detail.sparks=Particles("Chassis sparks",root,sparkMaterial,false);
            detail.tyreTrails=new TrailRenderer[4];detail.brakeGlows=new MeshRenderer[4];
            for(int i=0;i<4;i++)
            {
                var mark=Group("Tyre trace "+i,root);var trail=Ensure<TrailRenderer>(mark.gameObject);trail.sharedMaterial=trailMaterial;trail.time=7.5f;trail.minVertexDistance=.3f;trail.startWidth=.24f;trail.endWidth=.18f;trail.emitting=false;
                trail.alignment=LineAlignment.TransformZ;trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;trail.receiveShadows=false;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.65f,0),new GradientAlphaKey(0,1)});trail.colorGradient=gradient;detail.tyreTrails[i]=trail;
                var glow=Group("Brake glow "+i,root);Ensure<MeshFilter>(glow.gameObject).sharedMesh=disc;
                var renderer=Ensure<MeshRenderer>(glow.gameObject);renderer.sharedMaterial=glowMaterial;renderer.enabled=false;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                glow.localScale=Vector3.one*car.wheels[i].radius*.62f;detail.brakeGlows[i]=renderer;
            }
            EditorUtility.SetDirty(detail);cars++;
        }
        var telemetry=Ensure<RaceDetailTelemetry>(ui.gameObject);telemetry.ui=ui;telemetry.dashboard=ui.hud.GetComponent<OverdriveRacingHUD>();
        var timing=ui.hud.transform.Find("Timing card");((RectTransform)timing).sizeDelta=new Vector2(340,218);
        telemetry.sectors=new TMP_Text[3];
        for(int i=0;i<3;i++)
        {
            Image("Sector backing "+i,timing,new Color(.065f,.085f,.115f,.9f),20+i*102,139,96,27);
            telemetry.sectors[i]=Text("Sector time "+i,timing,$"S{i+1}  --.---",26+i*102,139,91,27,12,Muted);
        }
        telemetry.delta=Text("Lap delta",timing,"DELTA  --.--- / FIRST LAP",20,177,310,25,12,Muted);
        var card=Group("Telemetry detail card",ui.hud.transform,true);var rect=(RectTransform)card;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0);rect.anchoredPosition=new Vector2(-48,350);rect.sizeDelta=new Vector2(420,108);
        var backing=Ensure<UnityEngine.UI.Image>(card.gameObject);backing.color=new Color(.018f,.028f,.044f,.92f);backing.raycastTarget=false;Image("Detail accent",card,Red,0,0,76,3);
        telemetry.fuel=Text("Fuel",card,"FUEL  10.0 KG",18,12,220,23,13,White);telemetry.topSpeed=Text("Peak speed",card,"TOP SPEED 000 KM/H",18,37,240,23,13,White);telemetry.status=Text("Power status",card,"ENERGY SAVING",18,65,230,24,12,Muted);
        Text("G caption",card,"G FORCE",290,7,110,18,10,Muted);
        Image("G horizontal",card,new Color(.4f,.6f,.7f,.3f),310,52,60,1);Image("G vertical",card,new Color(.4f,.6f,.7f,.3f),340,24,1,58);
        telemetry.gDot=Image("G dot",card,new Color(.24f,.94f,.72f),337,49,6,6).rectTransform;
        telemetry.gText=Text("G readout",card,"0.0 / 0.0 G",280,84,132,18,10,White);telemetry.gText.alignment=TextAlignmentOptions.Center;
        var dashboard=ui.hud.transform.Find("Driver dashboard");Image("RPM rail",dashboard,new Color(.15f,.2f,.26f),20,31,380,2);telemetry.rpmFill=Image("RPM sweep",dashboard,new Color(.3f,.95f,.77f),20,31,0,2).rectTransform;
        telemetry.resultStats=Text("Race telemetry summary",ui.results.transform,"TOP SPEED 000 KM/H",64,568,570,35,13,Muted);
        telemetry.leftArrow=Text("Left proximity",ui.hud.transform,"<",0,0,65,80,62,new Color(1,.7f,.24f,0));
        telemetry.rightArrow=Text("Right proximity",ui.hud.transform,">",0,0,65,80,62,new Color(1,.7f,.24f,0));
        foreach(var arrow in new[]{telemetry.leftArrow,telemetry.rightArrow})
        {arrow.rectTransform.anchorMin=arrow.rectTransform.anchorMax=arrow.rectTransform.pivot=new Vector2(.5f,.5f);arrow.alignment=TextAlignmentOptions.Center;arrow.rectTransform.anchoredPosition=new Vector2(arrow==telemetry.leftArrow?-185:185,-170);}
        telemetry.rearWarning=Text("Rear proximity",ui.hud.transform,"CAR BEHIND",0,0,220,24,13,new Color(1,.7f,.24f,0));
        telemetry.rearWarning.rectTransform.anchorMin=telemetry.rearWarning.rectTransform.anchorMax=telemetry.rearWarning.rectTransform.pivot=new Vector2(.5f,.5f);telemetry.rearWarning.rectTransform.anchoredPosition=new Vector2(0,-115);telemetry.rearWarning.alignment=TextAlignmentOptions.Center;
        for(int i=0;i<3;i++)
        {
            var chip=Group("Session chip "+i,ui.menu.transform,true);Place((RectTransform)chip,1160+i*207,178,194,34);var bg=Ensure<UnityEngine.UI.Image>(chip.gameObject);bg.color=new Color(.018f,.025f,.04f,.8f);bg.raycastTarget=false;
            Image("Chip accent",chip,Red,0,0,2,34);var label=Text("Chip text",chip,i==0?"20-CAR GRID":i==1?"2-LAP SPRINT":"NORMAL RACE",13,0,175,34,12,White);if(i==2)telemetry.modeChip=label;
        }
        EditorUtility.SetDirty(telemetry);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return $"Saved telemetry, sector/delta timing, proximity indicators and surface effects for {cars} vehicles";
    }
}
