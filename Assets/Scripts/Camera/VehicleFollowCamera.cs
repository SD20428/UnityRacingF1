using UnityEngine;
using UnityEngine.InputSystem;
namespace Overdrive
{
    [DefaultExecutionOrder(100)]
    public sealed class VehicleFollowCamera : MonoBehaviour
    {
        public Transform target;
        [SerializeField, Min(0.1f)] float response = 12f;
        [SerializeField] Vector3 offset=new Vector3(0,3.1f,-8);
        [SerializeField, Range(0,1)] float motionAmount=.65f;
        public Vector2 MotionAngles {get;private set;}
        Vector3 previousVelocity;
        Quaternion heading;
        Vector3 lastTarget;
        float height,heightVelocity;
        Transform previousTarget;
        UnityEngine.Camera lens;
        Rigidbody body;
        public int ViewMode {get;private set;}
        void OnEnable() { previousTarget=null; lens=GetComponent<UnityEngine.Camera>(); }
        void LateUpdate()
        {
            if (!target) return;
            var key=Keyboard.current;bool change=key!=null && key.cKey.wasPressedThisFrame && Time.timeScale>0;
            if(change){ViewMode=(ViewMode+1)%3;previousTarget=null;}
            var targetHeading=Quaternion.Euler(0,target.eulerAngles.y,0);
            bool snap=previousTarget!=target || Vector3.Distance(target.position,lastTarget)>15;
            if(snap)
            {heading=targetHeading;height=target.position.y;heightVelocity=0;body=target.GetComponent<Rigidbody>();previousTarget=target;
             previousVelocity=body?body.linearVelocity:Vector3.zero;MotionAngles=Vector2.zero;}
            float dt=Time.deltaTime;
            if(!snap && dt<=0) return;
            heading=Quaternion.Slerp(heading,targetHeading,1-Mathf.Exp(-response*dt));
            height=Mathf.SmoothDamp(height,target.position.y,ref heightVelocity,.08f,Mathf.Infinity,Mathf.Max(.0001f,dt));
            // Follow the interpolated render pose directly in XZ. Two cascaded position/aim
            // filters previously added speed-dependent lag and a delayed correction in bends.
            var anchor=target.position;anchor.y=height;
            var viewOffset=ViewMode==0?offset:ViewMode==1?new Vector3(0,1.55f,-1.2f):new Vector3(0,1.15f,1.9f);
            transform.position=anchor+heading*viewOffset;
            var aim=target.position+Vector3.up+heading*Vector3.forward*5-transform.position;
            if(key!=null && Time.timeScale>0)
            {
                float direction=key.endKey.isPressed?180:key.deleteKey.isPressed?-90:key.pageDownKey.isPressed?90:0;
                if(key.homeKey.isPressed)direction=0;
                if(direction!=0)aim=heading*Quaternion.Euler(0,direction,0)*Vector3.forward;
            }
            if(aim.sqrMagnitude>.01f) transform.rotation=Quaternion.LookRotation(aim,Vector3.up);
            if(body && dt>0)
            {
                // Filter and cap inertial cues; collisions/recovery cannot whip the camera.
                var acceleration=target.InverseTransformDirection((body.linearVelocity-previousVelocity)/Mathf.Max(dt,.001f))/9.81f;
                previousVelocity=body.linearVelocity;
                var desired=new Vector2(Mathf.Clamp(-acceleration.z*.7f,-1.6f,1.6f),Mathf.Clamp(acceleration.x*.45f,-1.2f,1.2f))*motionAmount;
                MotionAngles=Vector2.Lerp(MotionAngles,desired,1-Mathf.Exp(-5*dt));
                transform.rotation*=Quaternion.Euler(MotionAngles.x,0,MotionAngles.y);
            }
            if(lens && body) lens.fieldOfView=Mathf.Lerp(lens.fieldOfView,Mathf.Lerp(60,73,Mathf.Clamp01(body.linearVelocity.magnitude*3.6f/300)),1-Mathf.Exp(-5*dt));
            lastTarget=target.position;
        }
    }
}
