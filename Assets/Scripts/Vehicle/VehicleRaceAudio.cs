using UnityEngine;
using System.Collections.Generic;

namespace Overdrive
{
    [RequireComponent(typeof(OverdriveVehicle))]
    public sealed class VehicleRaceAudio : MonoBehaviour
    {
        public AudioClip engineLoop, tyreLoop, windLoop, impactClip, countdownClip, startClip;
        public AudioClip intakeLoop, transmissionLoop, kerbLoop, gravelLoop, shiftClip, liftClip;
        [Range(0,1)] public float volume = .7f;
        public float EnginePitch => engine ? engine.pitch : 0;
        public float TyreVolume => tyres ? tyres.volume : 0;
        public int StartSignals {get;private set;}
        public int CountdownSignals {get;private set;}
        public bool AudioPaused {get;private set;}
        public float KerbVolume => kerbs ? kerbs.volume : 0;
        public float GravelVolume => gravel ? gravel.volume : 0;
        public float IntakeVolume => intake ? intake.volume : 0;
        public int ShiftSounds {get;private set;}
        public int LiftSounds {get;private set;}
        OverdriveVehicle car;
        RaceManager race;
        AudioSource engine, tyres, wind, effects;
        AudioSource intake, transmission, kerbs, gravel;
        AudioSource[] owned;
        VehicleFollowCamera followCamera;
        readonly Dictionary<Collider,int> surfaceKinds=new Dictionary<Collider,int>();
        bool player;
        int lastGear, lastCount = -1;
        RaceState previous = RaceState.Menu;
        float shiftCut, nextImpact;
        float previousThrottle,nextLift;
        Vector3 previousPosition;

        AudioSource Source(AudioClip clip, bool loop, bool spatial)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip; source.loop = loop; source.playOnAwake = false; source.volume = 0;
            source.spatialBlend = spatial ? 1 : 0; source.dopplerLevel = spatial ? .35f : 0;
            source.minDistance = 4; source.maxDistance = 100;
            source.rolloffMode = AudioRolloffMode.Logarithmic; source.priority = spatial ? 160 : 32;
            if (loop && clip) source.Play();
            return source;
        }
        void Awake()
        {
            car = GetComponent<OverdriveVehicle>(); player = GetComponent<PlayerVehicleInput>() != null;
            engine = Source(engineLoop, true, !player);
            if (player)
            {
                tyres = Source(tyreLoop, true, false); wind = Source(windLoop, true, false);
                effects = Source(null, false, false); effects.volume = volume * .55f;
                intake=Source(intakeLoop,true,false);transmission=Source(transmissionLoop,true,false);
                kerbs=Source(kerbLoop,true,false);gravel=Source(gravelLoop,true,false);
            }
            owned=new[]{engine,tyres,wind,effects,intake,transmission,kerbs,gravel};
            previousPosition=transform.position;
        }
        void Start() { race = FindFirstObjectByType<RaceManager>(); lastGear = car.Gear;if(player)followCamera=FindFirstObjectByType<VehicleFollowCamera>(); }
        void PauseAudio(bool paused)
        {
            if (AudioPaused == paused) return;
            AudioPaused = paused;
            if(owned!=null)foreach(var source in owned)SetPause(source,paused);
        }
        static void SetPause(AudioSource source, bool paused)
        { if (!source) return; if (paused) source.Pause(); else source.UnPause(); }
        void Update()
        {
            PauseAudio(Time.timeScale <= 0);
            if (AudioPaused || !car || !engine) return;
            if (!engine.isPlaying && engineLoop) engine.Play();
            if (player && tyres && !tyres.isPlaying && tyreLoop) tyres.Play();
            if (player && wind && !wind.isPlaying && windLoop) wind.Play();
            float dt = Time.deltaTime, blend = 1 - Mathf.Exp(-12 * dt);
            bool relocated=(transform.position-previousPosition).sqrMagnitude>225;previousPosition=transform.position;
            if(relocated){previousThrottle=0;shiftCut=0;if(kerbs)kerbs.volume=0;if(gravel)gravel.volume=0;}
            if (car.Gear != lastGear)
            {
                if(player && car.ControlsEnabled && !relocated && car.SpeedKmh>20 && shiftClip)
                {Play(shiftClip,car.Gear>lastGear?.38f:.28f);ShiftSounds++;}
                lastGear = car.Gear; shiftCut = .085f;
            }
            shiftCut = Mathf.Max(0, shiftCut - dt);
            float rpm = car.PowertrainEnabled ? car.EngineRPM : Mathf.Lerp(4000,14000,car.SpeedKmh/160);
            engine.pitch = Mathf.Lerp(engine.pitch, Mathf.Clamp(rpm / 5000, .75f, 3), blend);
            float load = car.ControlsEnabled ? Mathf.Lerp(.34f, 1, Mathf.Abs(car.Throttle)) : .22f;
            float level = volume * (player ? .44f : .16f) * load * (shiftCut > 0 ? .35f : 1);
            if (race && race.State == RaceState.Finished) level = 0;
            engine.volume = Mathf.Lerp(engine.volume, level, blend);
            if (!player) return;
            effects.volume=volume*.55f;
            float cockpit=followCamera && followCamera.ViewMode==1?.6f:1;
            UpdateLoop(intake,volume*.13f*Mathf.Clamp01(car.Throttle)*(car.ControlsEnabled?1:0)*(shiftCut>0?.3f:1),engine.pitch,blend);
            UpdateLoop(transmission,volume*.035f*Mathf.InverseLerp(15,120,car.SpeedKmh)*(car.ControlsEnabled?1:0),Mathf.Clamp(rpm/6500,.65f,2.3f),blend);
            if(car.ControlsEnabled && !relocated && car.SpeedKmh>80 && previousThrottle>.65f && car.Throttle<.12f && Time.time>=nextLift && liftClip)
            {Play(liftClip,.35f);LiftSounds++;nextLift=Time.time+.8f;}
            previousThrottle=car.Throttle;
            float slip = 0, loose = 0;int kerbWheels=0,looseWheels=0;
            if (car.ControlsEnabled && car.SpeedKmh > 15 && car.wheels != null)
                foreach (var wheel in car.wheels)
                    if (wheel && wheel.GetGroundHit(out var hit))
                    {
                        slip = Mathf.Max(slip, Mathf.Abs(hit.sidewaysSlip), Mathf.Abs(hit.forwardSlip) * .7f);
                        if(!surfaceKinds.TryGetValue(hit.collider,out int kind))
                        {
                            var surface=hit.collider.GetComponentInParent<TrackSurface>();
                            string label=hit.collider.name;
                            kind=surface && surface.grip<.9f?2:label.IndexOf("kerb",System.StringComparison.OrdinalIgnoreCase)>=0 || label.IndexOf("curb",System.StringComparison.OrdinalIgnoreCase)>=0?1:0;
                            surfaceKinds[hit.collider]=kind;
                        }
                        if(kind==1)kerbWheels++;else if(kind==2)looseWheels++;
                    }
            float rolling=Mathf.InverseLerp(15,100,car.SpeedKmh);
            UpdateLoop(kerbs,volume*.22f*kerbWheels*.25f*rolling,Mathf.Lerp(.6f,2.4f,Mathf.Clamp01(car.SpeedKmh/280)),blend);
            UpdateLoop(gravel,volume*.24f*looseWheels*.25f*rolling,Mathf.Lerp(.7f,1.4f,Mathf.Clamp01(car.SpeedKmh/200)),blend);
            loose = car.SurfaceGrip < .9f ? 1 : 0;
            float skid = Mathf.InverseLerp(.16f, .8f, slip) * Mathf.InverseLerp(15,70,car.SpeedKmh);
            tyres.volume = Mathf.Lerp(tyres.volume, volume * .25f * skid*(1-looseWheels*.2f), blend);
            tyres.pitch = Mathf.Lerp(1.15f, .65f, loose);
            wind.volume = Mathf.Lerp(wind.volume, car.ControlsEnabled ? volume * .19f * cockpit * Mathf.Pow(Mathf.Clamp01(car.SpeedKmh/320),2) : 0, blend);
            wind.pitch = Mathf.Lerp(.7f,1.25f,Mathf.Clamp01(car.SpeedKmh/320));
            if (!race) return;
            if (race.State == RaceState.Countdown)
            {
                int count = Mathf.CeilToInt(race.CountdownRemaining);
                if (count != lastCount && count > 0) { Play(countdownClip,.55f); CountdownSignals++; lastCount=count; }
            }
            else lastCount = -1;
            if (race.State == RaceState.Racing && previous == RaceState.Countdown) { Play(startClip,.7f); StartSignals++; }
            previous = race.State;
        }
        static void UpdateLoop(AudioSource source,float target,float pitch,float blend)
        {
            if(!source)return;
            source.volume=Mathf.Lerp(source.volume,target,blend);source.pitch=pitch;
            if(source.clip && !source.isPlaying)source.Play();
        }
        void Play(AudioClip clip,float gain) { if (effects && clip) effects.PlayOneShot(clip,gain); }
        void OnCollisionEnter(Collision collision)
        {
            if (!player || !car || !car.ControlsEnabled || Time.time < nextImpact || collision.contactCount == 0) return;
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
            if (speed < 2) return;
            nextImpact = Time.time + .15f; Play(impactClip,Mathf.Clamp01(speed/18));
        }
        void OnDisable()
        {
            if(owned!=null)foreach(var source in owned)if(source){source.Stop();source.volume=0;}
            AudioPaused=false; lastCount=-1; previous=RaceState.Menu;
        }
        void OnDestroy()
        { if(owned!=null)foreach(var source in owned)if(source)Destroy(source); }
    }
}
