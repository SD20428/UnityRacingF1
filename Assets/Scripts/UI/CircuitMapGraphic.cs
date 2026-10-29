using UnityEngine;
namespace Overdrive
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CircuitMapGraphic : UnityEngine.UI.MaskableGraphic
    {
        public RaceManager race;
        public float stroke=3;
        public bool showRacers=true;
        Vector3 lastPlayer,lastAI;
        float nextRefresh;
        void LateUpdate()
        {
            if(!race)return;
            if(showRacers){if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.1f;SetVerticesDirty();}return;}
            var p=race.player.transform.position;var a=race.opponent.transform.position;
            if((p-lastPlayer).sqrMagnitude>.01f || (a-lastAI).sqrMagnitude>.01f){lastPlayer=p;lastAI=a;SetVerticesDirty();}
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();if(!race || !race.ai || !race.ai.path || !race.ai.path.Valid)return;
            var points=race.ai.path.points;var min=new Vector2(float.MaxValue,float.MaxValue);var max=new Vector2(float.MinValue,float.MinValue);
            foreach(var p in points){var v=new Vector2(p.position.z,-p.position.x);min=Vector2.Min(min,v);max=Vector2.Max(max,v);}
            var rect=rectTransform.rect;float scale=Mathf.Min((rect.width-28)/(max.x-min.x),(rect.height-28)/(max.y-min.y));
            Vector2 Map(Vector3 p)=>rect.center+(new Vector2(p.z,-p.x)-(min+max)*.5f)*scale;
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
            {int i=vh.currentVertCount;vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);vh.AddVert(d,tint,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
            for(int j=0;j<points.Length;j++)
            {var a=Map(points[j].position);var b=Map(points[(j+1)%points.Length].position);var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*stroke*.5f;Quad(a-n,a+n,b+n,b-n,color);}
            void Dot(Vector2 p,float radius,Color tint)
            {for(int j=0;j<16;j++){float a=j*Mathf.PI/8,b=(j+1)*Mathf.PI/8;int i=vh.currentVertCount;vh.AddVert(p,tint,Vector2.zero);vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,tint,Vector2.zero);vh.AddVert(p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,tint,Vector2.zero);vh.AddTriangle(i,i+1,i+2);}}
            Dot(Map(points[0].position),3,Color.white);
            if(showRacers){foreach(var racer in race.Racers)if(racer.vehicle!=race.player && racer.vehicle.gameObject.activeInHierarchy)Dot(Map(racer.vehicle.transform.position),3,racer.identity?racer.identity.teamColor:new Color(1,.4f,.2f));Dot(Map(race.player.transform.position),6,new Color(.25f,.94f,.85f));}
        }
    }
}
