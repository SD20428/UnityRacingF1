using System.Collections.Generic;
using UnityEngine;

namespace Overdrive
{
    // Cosmetic observers only. No colliders, vehicle input, rigidbody or tyre settings are changed.
    [DefaultExecutionOrder(180)]
    [RequireComponent(typeof(OverdriveVehicle))]
    public sealed class VehicleSurfaceDetails : MonoBehaviour
    {
        public ParticleSystem dust,sparks;
        public TrailRenderer[] tyreTrails;
        public MeshRenderer[] brakeGlows;
        public float BrakeHeat {get;private set;}
        public bool DustActive {get;private set;}
        public int SkidEmittingWheels {get;private set;}
        public int SparkBursts {get;private set;}
        OverdriveVehicle car;Vector3 lastPosition;bool positionKnown,lastControls;
        float nextSpark;MaterialPropertyBlock glowBlock;
        readonly Dictionary<Collider,TrackSurface> surfaces=new Dictionary<Collider,TrackSurface>();
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        void Awake(){car=GetComponent<OverdriveVehicle>();glowBlock=new MaterialPropertyBlock();}
        void OnEnable(){positionKnown=false;if(!car)car=GetComponent<OverdriveVehicle>();if(glowBlock==null)glowBlock=new MaterialPropertyBlock();}
        TrackSurface Surface(Collider collider)
        {
            if(!surfaces.TryGetValue(collider,out var surface)){surface=collider.GetComponentInParent<TrackSurface>();surfaces[collider]=surface;}
            return surface;
        }
        void Clear()
        {
            BrakeHeat=0;DustActive=false;SkidEmittingWheels=0;
            if(dust)dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(sparks)sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(tyreTrails!=null)foreach(var trail in tyreTrails)if(trail){trail.emitting=false;trail.Clear();}
            if(brakeGlows!=null)foreach(var glow in brakeGlows)if(glow)glow.enabled=false;
        }
        void LateUpdate()
        {
            if(!car || !car.Body || Time.timeScale<=0)return;
            if(!positionKnown || Vector3.Distance(lastPosition,transform.position)>15)Clear();
            lastPosition=transform.position;positionKnown=true;
            if(!car.ControlsEnabled){if(lastControls)Clear();lastControls=false;return;}lastControls=true;
            float target=Mathf.Clamp01(car.Brake*car.SpeedKmh/80);
            BrakeHeat=Mathf.MoveTowards(BrakeHeat,target,Time.deltaTime*(target>BrakeHeat?.7f:.12f));
            int offroad=0;SkidEmittingWheels=0;
            for(int i=0;i<car.wheels.Length;i++)
            {
                bool contact=car.wheels[i].GetGroundHit(out var hit);
                var surface=contact?Surface(hit.collider):null;bool loose=surface && surface.grip<.9f;
                if(contact && loose)offroad++;
                bool skid=contact && !loose && car.SpeedKmh>25 && (Mathf.Abs(hit.sidewaysSlip)>.25f || Mathf.Abs(hit.forwardSlip)>.35f);
                if(tyreTrails!=null && i<tyreTrails.Length && tyreTrails[i])
                {
                    var trail=tyreTrails[i];if(contact)trail.transform.SetPositionAndRotation(hit.point+hit.normal*.024f,Quaternion.LookRotation(hit.normal,transform.forward));
                    if(skid && !trail.emitting)trail.Clear();trail.emitting=skid;
                    if(skid)SkidEmittingWheels++;
                }
                if(brakeGlows!=null && i<brakeGlows.Length && brakeGlows[i])
                {
                    var glow=brakeGlows[i];glow.enabled=BrakeHeat>.18f;
                    if(glow.enabled)
                    {
                        car.wheels[i].GetWorldPose(out var position,out var rotation);
                        glow.transform.SetPositionAndRotation(position+rotation*Vector3.right*(i%2==0?-.19f:.19f),rotation);
                        glowBlock.SetColor(BaseColor,new Color(1.7f,Mathf.Lerp(.035f,.5f,BrakeHeat),.012f,BrakeHeat*.65f));glow.SetPropertyBlock(glowBlock);
                    }
                }
            }
            DustActive=offroad>0 && car.SpeedKmh>20;
            if(dust)
            {
                dust.transform.position=transform.TransformPoint(new Vector3(0,.03f,-1.6f));
                dust.transform.rotation=Quaternion.LookRotation(-transform.forward+Vector3.up*.18f);
                var emission=dust.emission;emission.rateOverTime=DustActive?Mathf.Min(60,car.SpeedKmh*.35f):0;
                if(DustActive && !dust.isPlaying)dust.Play();
            }
        }
        void EmitSparks(Vector3 position,int count)
        {
            if(!sparks || Time.time<nextSpark)return;nextSpark=Time.time+.08f;
            sparks.transform.SetPositionAndRotation(position+Vector3.up*.03f,Quaternion.LookRotation(-transform.forward+Vector3.up*.28f));
            if(!sparks.isPlaying)sparks.Play();
            sparks.Emit(count);SparkBursts++;
        }
        void OnCollisionEnter(Collision collision)
        {
            if(car && car.ControlsEnabled && collision.relativeVelocity.magnitude>11 && collision.contactCount>0)
                EmitSparks(collision.GetContact(0).point,10);
        }
        void OnCollisionStay(Collision collision)
        {
            if(!car || !car.ControlsEnabled || car.SpeedKmh<80)return;
            for(int i=0;i<collision.contactCount;i++)
            {
                var contact=collision.GetContact(i);
                if(contact.normal.y>.55f && transform.InverseTransformPoint(contact.point).y<-.2f){EmitSparks(contact.point,3);break;}
            }
        }
        void OnDisable(){Clear();positionKnown=false;lastControls=false;}
    }
}
