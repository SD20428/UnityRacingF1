using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using Overdrive;

// Incremental repair of the generated circuit; preserves buildings, race gates and layout.
public static class OverdriveCurveSmooth
{
    static int Wrap(int i,int n)=>(i%n+n)%n;
    static Vector3 Cubic(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
        =>.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
    static float Sample(float[] values,int i,float t)
        =>Mathf.Lerp(values[Wrap(i,values.Length)],values[Wrap(i+1,values.Length)],t);
    public static string Apply()
    {
        if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first.");
        var root=GameObject.Find("Refined Circuit");
        var road=root.transform.Find("Asphalt Surface").GetComponent<MeshFilter>();
        if(root.transform.Find("Curve Smooth v2 Applied")) return "Already repaired; original source meshes preserved.";
        var originalRoad=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/OverdriveRefined/Asphalt Surface.asset");
        var old=originalRoad.vertices; int n=old.Length/2-1, subdivisions=4;
        var centers=new Vector3[n]; var rightOld=new Vector3[n];
        var heights=new float[n];
        for(int i=0;i<n;i++) { centers[i]=(old[i*2]+old[i*2+1])*.5f; rightOld[i]=(old[i*2+1]-old[i*2]).normalized; }
        float maximumBankChange=0;
        for(int i=0;i<n;i++)
        {
            maximumBankChange=Mathf.Max(maximumBankChange,Vector3.Angle(rightOld[i],rightOld[(i+1)%n]));
            float sum=0,weight=0;
            for(int j=-12;j<=12;j++) { float w=Mathf.Exp(-j*j/50f); sum+=centers[Wrap(i+j,n)].y*w; weight+=w; }
            heights[i]=sum/weight;
        }
        var originalCenters=(Vector3[])centers.Clone();
        for(int i=0;i<n;i++)
        {
            Vector3 sum=Vector3.zero; float weight=0;
            for(int j=-24;j<=24;j++) { float w=Mathf.Exp(-j*j/128f); sum+=originalCenters[Wrap(i+j,n)]*w; weight+=w; }
            centers[i]=sum/weight; centers[i].y=heights[i];
        }
        int rings=n*subdivisions; var smooth=new Vector3[rings+1]; var rights=new Vector3[rings+1];
        for(int s=0;s<rings;s++) { int i=s/subdivisions; float t=(s%subdivisions)/(float)subdivisions; smooth[s]=Cubic(centers[Wrap(i-1,n)],centers[i],centers[(i+1)%n],centers[(i+2)%n],t); }
        smooth[rings]=smooth[0];
        for(int s=0;s<rings;s++) { var tangent=smooth[(s+1)%rings]-smooth[Wrap(s-1,rings)]; tangent.y=0; rights[s]=Vector3.Cross(Vector3.up,tangent.normalized); }
        rights[rings]=rights[0];
        float length=0,maxGradeChange=0;
        for(int s=0;s<rings;s++) {
            length+=Vector3.Distance(smooth[s],smooth[s+1]);
            var a=smooth[s]-smooth[Wrap(s-1,rings)]; var b=smooth[s+1]-smooth[s];
            float ga=Mathf.Atan2(a.y,new Vector2(a.x,a.z).magnitude),gb=Mathf.Atan2(b.y,new Vector2(b.x,b.z).magnitude);
            maxGradeChange=Mathf.Max(maxGradeChange,Mathf.Abs(ga-gb)*Mathf.Rad2Deg);
        }
        int changed=0;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/OverdriveRefined/"+filter.name+".asset");
            if(!source || source.vertexCount!=old.Length || !AssetDatabase.GetAssetPath(source).StartsWith("Assets/Meshes/OverdriveRefined/")) continue;
            var original=source.vertices; var offsetsA=new float[n]; var offsetsB=new float[n]; var heightA=new float[n]; var heightB=new float[n];
            for(int i=0;i<n;i++)
            {
                var originalCenter=(old[i*2]+old[i*2+1])*.5f;
                var up=Vector3.Cross((old[((i+1)%n)*2]+old[((i+1)%n)*2+1])*.5f-originalCenter,rightOld[i]).normalized;
                if(up.y<0) up=-up;
                offsetsA[i]=Vector3.Dot(original[i*2]-originalCenter,rightOld[i]); offsetsB[i]=Vector3.Dot(original[i*2+1]-originalCenter,rightOld[i]);
                heightA[i]=Vector3.Dot(original[i*2]-originalCenter,up); heightB[i]=Vector3.Dot(original[i*2+1]-originalCenter,up);
            }
            bool kerb=filter.name.Contains("Kerb"); bool vertical=filter.name.Contains("Fence") || filter.name.Contains("Concrete");
            int across=kerb?4:(vertical?1:4), stride=across+1;
            var vertices=new Vector3[(rings+1)*stride]; var uv=new Vector2[vertices.Length];
            var first=new List<int>(); var second=new List<int>(); float distance=0;
            for(int s=0;s<=rings;s++)
            {
                int segment=(s%rings)/subdivisions; float t=(s%subdivisions)/(float)subdivisions;
                if(s>0) distance+=Vector3.Distance(smooth[s-1],smooth[s]);
                for(int x=0;x<=across;x++)
                {
                    float u=x/(float)across;
                    float offset=Mathf.Lerp(Sample(offsetsA,segment,t),Sample(offsetsB,segment,t),u);
                    float h=Mathf.Lerp(Sample(heightA,segment,t),Sample(heightB,segment,t),u);
                    // Flush at both kerb borders, rounded 30mm crown; no exposed 65mm lip.
                    if(kerb) h=.03f*Mathf.Sin(u*Mathf.PI)*Mathf.Sin(u*Mathf.PI);
                    vertices[s*stride+x]=smooth[s]+rights[s]*offset+Vector3.up*h;
                    uv[s*stride+x]=vertical?new Vector2(distance/2,h/2):new Vector2(u,distance/16);
                }
                if(s==rings) continue;
                var triangles=((int)(distance/3)%2==0)?first:second;
                for(int x=0;x<across;x++) { int a=s*stride+x,b=a+stride; triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1}); }
            }
            if(vertical && offsetsA[0]<0)
            {
                foreach(var list in new[]{first,second}) for(int j=0;j<list.Count;j+=3) { int a=list[j]; list[j]=list[j+2]; list[j+2]=a; }
            }
            var mesh=new Mesh { name=filter.name+" CurveSmooth v2",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices=vertices; mesh.uv=uv; mesh.subMeshCount=2; mesh.SetTriangles(first,0); mesh.SetTriangles(second,1); mesh.RecalculateNormals();
            var normals=mesh.normals;
            for(int x=0;x<=across;x++) { var normal=(normals[x]+normals[rings*stride+x]).normalized; normals[x]=normals[rings*stride+x]=normal; }
            mesh.normals=normals; mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath("Assets/Meshes/OverdriveRefined/"+filter.name+"_CurveSmooth.asset"));
            filter.sharedMesh=mesh;
            var collider=filter.GetComponent<MeshCollider>();
            if(collider)
            {
                collider.sharedMesh=null;
                collider.cookingOptions=MeshColliderCookingOptions.CookForFasterSimulation|MeshColliderCookingOptions.EnableMeshCleaning|MeshColliderCookingOptions.WeldColocatedVertices|MeshColliderCookingOptions.UseFastMidphase;
                if(vertical)
                {
                    var collision=Object.Instantiate(mesh); var triangles=new List<int>(mesh.triangles); var front=mesh.triangles;
                    for(int j=0;j<front.Length;j+=3) triangles.AddRange(new[]{front[j+2],front[j+1],front[j]});
                    collision.subMeshCount=1; collision.SetTriangles(triangles,0);
                    AssetDatabase.CreateAsset(collision,AssetDatabase.GenerateUniqueAssetPath("Assets/Meshes/OverdriveRefined/"+filter.name+"_CurveSmoothCollision.asset")); collider.sharedMesh=collision;
                } else collider.sharedMesh=mesh;
            }
            changed++;
        }
        // Keep gates and recovery poses on the repaired centreline heights.
        foreach(var cp in root.GetComponentsInChildren<CircuitCheckpoint>())
        {
            int best=0; float nearest=float.PositiveInfinity;
            for(int s=0;s<n;s++) { var delta=originalCenters[s]-cp.transform.position; delta.y=0; if(delta.sqrMagnitude<nearest) {nearest=delta.sqrMagnitude; best=s;} }
            cp.transform.position=centers[best];
        }
        var race=Object.FindFirstObjectByType<RaceManager>(); var gates=race.ai.path.points;
        for(int i=0;i<gates.Length;i++) gates[i].rotation=Quaternion.LookRotation(gates[(i+1)%gates.Length].position-gates[i].position,Vector3.up);
        foreach(Transform child in root.transform)
        {
            if(child.name!="Fence Post") continue;
            int best=0; float nearest=float.PositiveInfinity;
            for(int i=0;i<n;i++) { var d=originalCenters[i]-child.position; d.y=0; if(d.sqrMagnitude<nearest) {nearest=d.sqrMagnitude;best=i;} }
            float offset=Vector3.Dot(child.position-originalCenters[best],rightOld[best]);
            child.position=centers[best]+rights[best*subdivisions]*offset+Vector3.up*2.2f;
            child.rotation=Quaternion.identity;
        }
        new GameObject("Curve Smooth v2 Applied").transform.SetParent(root.transform,false);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        string report=$"strips={changed}; longitudinalRings={rings}; spacing~={length/rings:F3}m; length={length:F2}m; maximumGradeChange={maxGradeChange:F4}deg; previousCrossSectionDirectionChange={maximumBankChange:F3}deg; bank=0; kerbCrown=0.03m; roadColliderMatchesRender=True";
        System.IO.File.WriteAllText("Docs/CurveSmooth_Geometry.txt",report); return report;
    }
}
