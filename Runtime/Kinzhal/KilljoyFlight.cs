using System.Linq;
using UnityEngine;
namespace Kinzhal
{
    public sealed class KilljoyFlight : MonoBehaviour
    {
        private Missile missile;
        private Unit target;
        private GlobalPosition launch,coordinate;
        private Vector3 targetVelocity;
        private Transform booster;
        private bool separated,terminalReported;
        private float nextTrack;
        private float nextTelemetry;
        private float separatedAt;
        private Animation finAnimation;
        private Transform[] controlFins;
        private Quaternion[] finBases;
        private Vector3[] finAxes;
        private bool finsReady;
        private bool finAreaReady;
        private float finAreaAtSeparation;
        private Vector3 finDemand;
        private Vector3 steeringDirection;
        private GlobalPosition lastCollisionPosition;
        internal void SteeringAim()=>missile.SetAimpoint(missile.GlobalPosition()+steeringDirection*5000,Vector3.zero);
        internal void ContactAim()=>missile.SetAimpoint(coordinate,targetVelocity);
        private Vector3 collisionTravel;
        private readonly System.Collections.Generic.HashSet<int> rejectedContacts=new System.Collections.Generic.HashSet<int>();
        internal bool FirstRejectedContact(Collider collider)=>rejectedContacts.Add(collider.GetInstanceID());
        internal void BeginContactCheck()
        {
            Vector3 travel=missile.GlobalPosition()-lastCollisionPosition;
            lastCollisionPosition=missile.GlobalPosition();
            collisionTravel=travel.magnitude<=missile.rb.velocity.magnitude*Time.fixedDeltaTime*3+10?travel:Vector3.zero;
        }
        internal Vector3 CollisionStart(Vector3 current)=>current-collisionTravel;
        private static readonly System.Reflection.FieldInfo Reached=HarmonyLib.AccessTools.Field(typeof(Missile),"reachedOnTarget");
        private static readonly System.Reflection.FieldInfo Inputs=HarmonyLib.AccessTools.Field(typeof(Missile),"inputs");
        private static readonly System.Reflection.FieldInfo FinArea=HarmonyLib.AccessTools.Field(typeof(Missile),"currentFinArea");
        internal GlobalPosition TargetPosition=>coordinate;
        internal Vector3 TargetVelocity=>targetVelocity;
        internal void Bind(Missile value,Unit targetUnit,GlobalPosition point)
        {
            if(missile!=null)return;
            missile=value;target=targetUnit;coordinate=point;launch=missile.GlobalPosition();lastCollisionPosition=launch;steeringDirection=transform.forward;
            booster=GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Kh47M2_Booster");
            finAnimation=GetComponentInChildren<Animation>(true);
            if(finAnimation!=null){finAnimation.Stop();finAnimation.enabled=false;finAnimation.clip.SampleAnimation(finAnimation.gameObject,0);}
            if(missile.LocalSim){UpdateTrack();Vector3 delta=coordinate-launch;delta.y=0;Plugin.Trace($"Killjoy launch range={delta.magnitude:0}m referenceCeiling={LoftProfile.Ceiling(launch.y,coordinate.y,delta.magnitude):0}m; native fuse retained");}
        }
        private void FixedUpdate()
        {
            if(missile==null||missile.disabled)return;
            if(!separated&&missile.timeSinceSpawn>1.3f&&missile.GetRemainingBurnTime()<=.02f)Separate();
            // Keep authoritative lift area in sync with the visible deployment,
            // including servers where a client visual RPC does not execute locally.
            if(separated&&missile.LocalSim&&!finAreaReady){
                float amount=Mathf.Clamp01((Time.time-separatedAt)/.8f);
                FinArea.SetValue(missile,Mathf.Lerp(finAreaAtSeparation,missile.GetFinArea(),amount));
                finAreaReady=amount>=1;
            }
        }
        internal void Guide()
        {
            if(missile==null||!missile.LocalSim||missile.disabled)return;
            coordinate+=targetVelocity*Time.fixedDeltaTime;
            if(Time.time>=nextTrack){nextTrack=Time.time+.25f;UpdateTrack();}
            Vector3 toTarget=coordinate-missile.GlobalPosition();
            float speed=Mathf.Max(100,missile.rb.velocity.magnitude);
            Vector3 velocity=missile.rb.velocity;
            Vector3 horizontal=new Vector3(toTarget.x,0,toTarget.z);
            Vector3 heading=horizontal.sqrMagnitude>1?horizontal.normalized:Vector3.ProjectOnPlane(velocity,Vector3.up).normalized;
            Vector3 desired;
            if(!separated&&missile.GetRemainingBurnTime()>.1f){
                Vector3 initial=coordinate-launch;initial.y=0;
                float apex=(float)LoftProfile.Ceiling(launch.y,coordinate.y,initial.magnitude);
                float pitch=(float)GuidanceMath.BoostPitch(missile.GlobalPosition().y,velocity.y,missile.GetRemainingBurnTime(),missile.GetRemainingDeltaV(),apex);
                desired=heading*Mathf.Cos(pitch)+Vector3.up*Mathf.Sin(pitch);
            } else {
                float closing=Vector3.Dot(velocity-targetVelocity,toTarget.normalized);
                float time=Mathf.Clamp(toTarget.magnitude/Mathf.Max(100,closing),.4f,400);
                float forwardSpeed=Vector3.Dot(velocity,heading);
                double ax,ay;
                GuidanceMath.CoastAcceleration(horizontal.magnitude,toTarget.y,forwardSpeed,velocity.y,Vector3.Dot(targetVelocity,heading),targetVelocity.y,time,
                    Mathf.InverseLerp(50000,10000,horizontal.magnitude)*Mathf.InverseLerp(20000,35000,horizontal.magnitude),out ax,out ay);
                double terminalX,terminalY;
                GuidanceMath.TerminalAcceleration(horizontal.magnitude,toTarget.y,forwardSpeed,velocity.y,Vector3.Dot(targetVelocity,heading),targetVelocity.y,out terminalX,out terminalY);
                float terminalBlend=1-Mathf.InverseLerp(20000,35000,toTarget.magnitude);
                ax=ax+(terminalX-ax)*terminalBlend;ay=ay+(terminalY-ay)*terminalBlend;
                Vector3 lateral=velocity-heading*forwardSpeed-Vector3.up*velocity.y;
                Vector3 targetLateral=targetVelocity-heading*Vector3.Dot(targetVelocity,heading)-Vector3.up*targetVelocity.y;
                float lateralGain=Mathf.Lerp(3/time,4*Mathf.Max(0,closing)/Mathf.Max(1,toTarget.magnitude),terminalBlend);
                Vector3 acceleration=heading*(float)ax+Vector3.up*(float)ay+lateralGain*(targetLateral-lateral);
                acceleration=Vector3.ProjectOnPlane(acceleration,velocity.normalized);
                acceleration=Vector3.ClampMagnitude(acceleration,8*9.81f);
                // Convert needed normal acceleration into the AoA that native lift can supply.
                float liftScale=.5f*missile.airDensity*velocity.sqrMagnitude*(float)FinArea.GetValue(missile)/Mathf.Max(1,missile.rb.mass);
                float requestedCoeff=acceleration.magnitude/Mathf.Max(.01f,liftScale);
                float low=0,high=22*Mathf.Deg2Rad;
                for(int i=0;i<16;i++){float mid=(low+high)*.5f;if(missile.GetLiftCoeff(mid)<requestedCoeff)low=mid;else high=mid;}
                float alpha=(low+high)*.5f;
                desired=velocity.normalized*Mathf.Cos(alpha)+(acceleration.sqrMagnitude>.001f?acceleration.normalized:Vector3.zero)*Mathf.Sin(alpha);
                if(!terminalReported){terminalReported=true;Plugin.Trace($"Killjoy predictive coast range={toTarget.magnitude:0}m altitude={missile.GlobalPosition().y:0}m velocityPitch={Mathf.Atan2(velocity.y,Mathf.Max(1,Vector3.ProjectOnPlane(velocity,Vector3.up).magnitude))*Mathf.Rad2Deg:0.0}deg");}
            }
            if(desired.sqrMagnitude<.01f)return;
            // Native Steering blends body and velocity after first alignment. Supply
            // that blended direction, so its servo does not double the requested AoA.
            if(velocity.sqrMagnitude>100&&(bool)Reached.GetValue(missile))desired=(desired.normalized+velocity.normalized).normalized;
            missile.SetTorque(2.2f,12);
            steeringDirection=desired.normalized;
            ContactAim();
            if(Time.time>=nextTelemetry){
                nextTelemetry=Time.time+5;
                float velocityPitch=Mathf.Atan2(velocity.y,Mathf.Max(1,Vector3.ProjectOnPlane(velocity,Vector3.up).magnitude))*Mathf.Rad2Deg;
                float alpha=Vector3.Angle(transform.forward,velocity);
                Plugin.Trace($"Killjoy guidance age={missile.timeSinceSpawn:0.0}s range={toTarget.magnitude:0}m altitude={missile.GlobalPosition().y:0}m speed={speed:0}m/s velocityPitch={velocityPitch:0.0}deg AoA={alpha:0.0}deg finArea={(float)FinArea.GetValue(missile):0.00} phase={(separated?"warhead":"boost")}");
            }
        }
        private void UpdateTrack()
        {
            if(target==null||target.disabled||missile.NetworkHQ==null)return;
            if(missile.NetworkHQ.TryGetKnownPosition(target,out var position)){
                coordinate=position;
                targetVelocity=target.rb!=null?Vector3.ClampMagnitude(target.rb.velocity,45):Vector3.zero;
            }
        }
        private void Separate()
        {
            separated=true;separatedAt=Time.time;finAreaAtSeparation=(float)FinArea.GetValue(missile);
            missile.DeployFins();
            if(booster!=null){
                var debris=Instantiate(booster.gameObject,booster.position,booster.rotation);
                debris.transform.localScale=booster.lossyScale;
                debris.name="KilljoySpentBooster";
                foreach(var collider in debris.GetComponentsInChildren<Collider>())collider.enabled=false;
                foreach(var animation in debris.GetComponentsInChildren<Animation>())animation.enabled=false;
                var body=debris.AddComponent<Rigidbody>();body.mass=400;body.velocity=missile.rb.GetPointVelocity(booster.position)-transform.forward*3;body.angularVelocity=Vector3.ClampMagnitude(missile.rb.angularVelocity,.4f);body.useGravity=true;
                body.interpolation=RigidbodyInterpolation.Interpolate;body.drag=.025f;body.angularDrag=1;body.detectCollisions=false;
                Destroy(debris,25);booster.gameObject.SetActive(false);
            }
            if(missile.LocalSim)missile.rb.mass=Mathf.Max(1800,missile.rb.mass-400);
            foreach(var effect in GetComponentsInChildren<ParticleSystem>(true))effect.Stop(true,ParticleSystemStopBehavior.StopEmitting);
            var capsule=GetComponent<CapsuleCollider>();
            var warhead=GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(t=>t.name=="Kh47M2_Warhead");
            if(capsule!=null&&warhead!=null){
                var mesh=warhead.sharedMesh.bounds;
                var bounds=new Bounds(transform.InverseTransformPoint(warhead.transform.TransformPoint(mesh.center)),Vector3.zero);
                for(int i=0;i<8;i++)bounds.Encapsulate(transform.InverseTransformPoint(warhead.transform.TransformPoint(mesh.center+Vector3.Scale(mesh.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))));
                capsule.center=bounds.center;capsule.height=bounds.size.z;capsule.radius=Mathf.Max(bounds.size.x,bounds.size.y)*.5f;
                if(missile.LocalSim)missile.rb.centerOfMass=capsule.center;
            }
            if(missile.LocalSim)Plugin.Trace($"Killjoy booster separated altitude={missile.GlobalPosition().y:0}m speed={missile.speed:0}m/s; unpowered warhead");
        }
        private void LateUpdate()
        {
            if(missile==null||missile.disabled||finAnimation==null)return;
            if(!separated){finAnimation.clip.SampleAnimation(finAnimation.gameObject,0);return;}
            if(!finsReady){
                float amount=Mathf.Clamp01((Time.time-separatedAt)/.8f);
                finAnimation.clip.SampleAnimation(finAnimation.gameObject,finAnimation.clip.length*amount);
                if(amount<1)return;
                controlFins=GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Warhead_Fin_")).ToArray();
                finBases=controlFins.Select(t=>t.localRotation).ToArray();
                finAxes=controlFins.Select(t=>t.InverseTransformDirection(t.name.EndsWith("Top")||t.name.EndsWith("Bottom")?transform.up:transform.right)).ToArray();
                finsReady=true;
                if(missile.LocalSim)Plugin.Trace($"Killjoy warhead fins deployed count={controlFins.Length}");
            }
            Vector3 demand=missile.LocalSim?(Vector3)Inputs.GetValue(missile):transform.InverseTransformDirection(missile.rb.angularVelocity)*2;
            finDemand=Vector3.Lerp(finDemand,demand,1-Mathf.Exp(-8*Time.deltaTime));
            for(int i=0;i<controlFins.Length;i++){
                bool yaw=controlFins[i].name.EndsWith("Top")||controlFins[i].name.EndsWith("Bottom");
                float deflection=Mathf.Clamp(yaw?finDemand.y:-finDemand.x,-1,1)*12;
                controlFins[i].localRotation=finBases[i]*Quaternion.AngleAxis(deflection,finAxes[i]);
            }
        }
    }
}
