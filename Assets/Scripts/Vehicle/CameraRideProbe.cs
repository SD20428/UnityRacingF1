#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using System.IO;
namespace Overdrive
{
    [DefaultExecutionOrder(200)]
    public sealed class CameraRideProbe : MonoBehaviour
    {
        RaceManager race;Camera cameraView;readonly List<float> frames=new List<float>();
        float maximumStraightError;int straightSamples;Vector3 previous;int repeatedPoses;
        void Start(){race=FindFirstObjectByType<RaceManager>();cameraView=Camera.main;}
        void LateUpdate()
        {
            if(!race || !cameraView)return;
            if(race.State==RaceState.Racing)
            {
                frames.Add(Time.unscaledDeltaTime*1000);
                var c=race.player;
                if(c.SpeedKmh>50 && Mathf.Abs(c.Body.angularVelocity.y)<.02f)
                {
                    var expected=c.transform.position+Quaternion.Euler(0,c.transform.eulerAngles.y,0)*new Vector3(0,3.1f,-8);
                    var error=cameraView.transform.position-expected;error.y=0;
                    maximumStraightError=Mathf.Max(maximumStraightError,error.magnitude);straightSamples++;
                    if(Time.unscaledDeltaTime>.001f && (c.transform.position-previous).sqrMagnitude<1e-10f)repeatedPoses++;
                }
                previous=c.transform.position;
            }
            if((race.State==RaceState.Finished || (race.State==RaceState.Racing && race.Elapsed>45)) && frames.Count>0)
            {
                frames.Sort();int n=frames.Count;
                File.WriteAllText("Docs/Handling_CameraRide.txt",$"renderFrames={n}; medianFrameMs={frames[n/2]:F3}; p95FrameMs={frames[Mathf.Min(n-1,(int)(n*.95f))]:F3}; maximumFrameMs={frames[n-1]:F3}; straightSamples={straightSamples}; maximumStraightXZTrackingErrorM={maximumStraightError:F3}; repeatedMovingRenderPoseSamples={repeatedPoses}. Editor desktop observations, not booth-PC or player-build FPS acceptance; may include tool-induced stalls.");
                Destroy(this);
            }
        }
    }
}
#endif
