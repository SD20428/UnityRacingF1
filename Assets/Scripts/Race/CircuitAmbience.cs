using UnityEngine;

namespace Overdrive
{
    // Two voices regardless of grandstand count; the nearest stand provides localized crowd sound.
    public sealed class CircuitAmbience : MonoBehaviour
    {
        public RaceManager race;
        public AudioClip crowdClip,environmentClip;
        public Vector3[] grandstands;
        public float environmentVolume=.035f;
        public float CrowdVolume => crowd?crowd.volume:0;
        public bool Paused {get;private set;}
        AudioSource crowd,environment;
        Transform audience;
        float nextScan;Vector3 destination;
        void Awake()
        {
            audience=new GameObject("Nearest crowd sound").transform;audience.SetParent(transform,false);
            crowd=audience.gameObject.AddComponent<AudioSource>();crowd.clip=crowdClip;crowd.loop=true;crowd.playOnAwake=false;
            crowd.spatialBlend=1;crowd.minDistance=15;crowd.maxDistance=170;crowd.dopplerLevel=0;crowd.priority=200;crowd.volume=0;
            environment=gameObject.AddComponent<AudioSource>();environment.clip=environmentClip;environment.loop=true;
            environment.playOnAwake=false;environment.spatialBlend=0;environment.priority=220;environment.volume=0;
            if(crowdClip)crowd.Play();if(environmentClip)environment.Play();
        }
        void Update()
        {
            if(!race || !race.player)return;
            bool paused=Time.timeScale<=0;
            if(paused!=Paused)
            {Paused=paused;if(paused){crowd.Pause();environment.Pause();}else{crowd.UnPause();environment.UnPause();}}
            if(paused)return;
            float distance=float.PositiveInfinity;
            if(grandstands!=null)foreach(var stand in grandstands)
            {
                float d=(stand-race.player.transform.position).sqrMagnitude;
                if(d<distance){distance=d;destination=stand;}
            }
            if(Time.time>=nextScan){nextScan=Time.time+.3f;audience.position=destination;}
            bool racing=race.State==RaceState.Racing;
            float activity=racing?.10f:race.State==RaceState.Finished?.12f:.025f;
            float blend=1-Mathf.Exp(-3*Time.deltaTime);
            crowd.volume=Mathf.Lerp(crowd.volume,distance<170*170?activity:0,blend);
            environment.volume=Mathf.Lerp(environment.volume,environmentVolume*(racing?1:.4f),blend);
        }
        void OnDisable(){if(crowd)crowd.Stop();if(environment)environment.Stop();}
        void OnEnable(){if(crowd && crowdClip)crowd.Play();if(environment && environmentClip)environment.Play();Paused=false;}
        void OnDestroy(){if(audience)Destroy(audience.gameObject);if(environment)Destroy(environment);}
    }
}
