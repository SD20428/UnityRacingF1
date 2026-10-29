using UnityEngine;

namespace Overdrive
{
    // Bounded minimum-curvature approximation. Uses actual path spacing, not a fixed inside offset.
    public static class RacingLinePlanner
    {
        static int Wrap(int i,int n)=>(i%n+n)%n;
        static float BendCost(Vector3 a,Vector3 b,Vector3 c)
        {
            var incoming=b-a;var outgoing=c-b;float x=incoming.magnitude,y=outgoing.magnitude;
            if(x<.1f || y<.1f)return 1e3f;
            return 2*(1-Mathf.Clamp(Vector3.Dot(incoming/x,outgoing/y),-1,1))/((x+y)*.5f);
        }
        static float LocalCost(Vector3[] p,int i,Vector3 candidate)
        {
            int n=p.Length;
            return BendCost(p[Wrap(i-2,n)],p[Wrap(i-1,n)],candidate)+BendCost(p[Wrap(i-1,n)],candidate,p[Wrap(i+1,n)])+BendCost(candidate,p[Wrap(i+1,n)],p[Wrap(i+2,n)]);
        }
        public static Vector3[] Build(WaypointPath centre,float halfWidth=6,int iterations=1800)
        {
            if(!centre || !centre.Valid)throw new System.ArgumentException("Valid centre path required");
            int n=centre.points.Length;var points=new Vector3[n];var rights=new Vector3[n];var offsets=new float[n];var locked=new bool[n];var curvature=new float[n];
            halfWidth=Mathf.Clamp(halfWidth,0,6);
            for(int i=0;i<n;i++)
            {
                points[i]=centre.Position(i);var tangent=centre.Position(i+1)-centre.Position(i-1);tangent.y=0;
                rights[i]=Vector3.Cross(Vector3.up,tangent.normalized).normalized;
                var a=centre.Position(i)-centre.Position(i-1);var b=centre.Position(i+1)-centre.Position(i);
                curvature[i]=Vector3.SignedAngle(a,b,Vector3.up)*Mathf.Deg2Rad/Mathf.Max(.1f,(a.magnitude+b.magnitude)*.5f);
            }
            // Anchor outside entry / inside apex / outside exit before smoothing. Pure minimum
            // curvature alone favours staying outside an entire hairpin and is not a racing line.
            void Anchor(int index,float value){index=Wrap(index,n);offsets[index]=value;locked[index]=true;}
            for(int first=0;first<n;first++)
            {
                if(Mathf.Abs(curvature[first])<.0015f)continue;
                float sign=Mathf.Sign(curvature[first]),total=0;int last=first;
                while(last<n && Mathf.Abs(curvature[last])>=.0015f && Mathf.Sign(curvature[last])==sign){total+=Mathf.Abs(curvature[last]);last++;}
                if(last-first>=4)
                {
                    float sum=0;int apex=first;
                    for(int i=first;i<last;i++){sum+=Mathf.Abs(curvature[i]);if(sum>=total*.5f){apex=i;break;}}
                    float lane=halfWidth*.88f;
                    Anchor(first-8,-sign*lane);Anchor(last+7,-sign*lane);Anchor(apex,sign*lane);
                }
                first=last-1;
            }
            int firstAnchor=System.Array.FindIndex(locked,x=>x);
            if(firstAnchor>=0)
            {
                int previous=firstAnchor;
                for(int step=1;step<=n;step++)
                {
                    int next=Wrap(firstAnchor+step,n);if(!locked[next])continue;
                    int length=Wrap(next-previous,n);if(length==0)length=n;
                    for(int j=1;j<length;j++){float t=j/(float)length;t=t*t*(3-2*t);offsets[Wrap(previous+j,n)]=Mathf.Lerp(offsets[previous],offsets[next],t);}
                    previous=next;
                }
            }
            for(int i=0;i<n;i++)points[i]+=rights[i]*offsets[i];
            const float epsilon=.35f;
            for(int pass=0;pass<Mathf.Clamp(iterations,0,3000);pass++)for(int k=0;k<n;k++)
            {
                int i=pass%2==0?k:n-1-k;var original=points[i];var right=rights[i];
                if(locked[i])continue;
                float current=LocalCost(points,i,original),plus=LocalCost(points,i,original+right*epsilon),minus=LocalCost(points,i,original-right*epsilon);
                float gradient=(plus-minus)/(2*epsilon),hessian=(plus+minus-2*current)/(epsilon*epsilon);
                if(hessian<=1e-8f)continue;
                float shift=Mathf.Clamp(-gradient/hessian,-.12f,.12f);
                float target=Mathf.Clamp(offsets[i]+shift,-halfWidth,halfWidth);shift=target-offsets[i];
                var candidate=original+right*shift;
                if(LocalCost(points,i,candidate)>current+1e-7f)continue;
                offsets[i]=target;points[i]=candidate;
            }
            return points;
        }
    }
}
