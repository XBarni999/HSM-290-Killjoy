using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using BepInEx;

namespace Kinzhal
{
    [BepInPlugin("ua.ncmod.kh47m2", "HSM-290 \"Killjoy\"", "1.1.0")]
    [BepInDependency("com.nikkorap.blueprinter", "2.0.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        private static readonly System.Reflection.FieldInfo SeekerMissile=AccessTools.Field(typeof(MissileSeeker),"missile");
        private static readonly System.Reflection.FieldInfo KnownPosition=AccessTools.Field(typeof(BallisticMissileGuidance),"knownPos");
        private static readonly System.Reflection.FieldInfo KnownVelocity=AccessTools.Field(typeof(BallisticMissileGuidance),"knownVel");
        internal static bool Is(Missile missile)=>missile!=null&&missile.GetWeaponInfo()!=null&&missile.GetWeaponInfo().name.StartsWith("WI_Kinzhal_",StringComparison.Ordinal);
        internal static void Trace(string message){Instance?.Logger.LogInfo(message);}
        private Harmony harmony;
        private float refresh;
        private WeaponMount[] cachedMounts=Array.Empty<WeaponMount>();
        private bool definitionsReady;
        private void Awake()
        {
            Instance=this;
            harmony=new Harmony("ua.ncmod.kh47m2");
            harmony.Patch(AccessTools.Method(typeof(WeaponManager), "InitializeWeaponManager"),
                prefix:new HarmonyMethod(typeof(Plugin), nameof(Manager)));
            harmony.Patch(AccessTools.Method(typeof(BallisticMissileGuidance),"Initialize"),postfix:new HarmonyMethod(typeof(Plugin),nameof(FlightInitialized)));
            harmony.Patch(AccessTools.Method(typeof(BallisticMissileGuidance),"SetTrajectory"),prefix:new HarmonyMethod(typeof(Plugin),nameof(Trajectory)));
            harmony.Patch(AccessTools.Method(typeof(Missile),"StartMissile"),postfix:new HarmonyMethod(typeof(Plugin),nameof(ClientStage)));
            harmony.Patch(AccessTools.Method(typeof(Missile),"Detonate"),prefix:new HarmonyMethod(typeof(Plugin),nameof(TraceDetonation)));
            harmony.Patch(AccessTools.Method(typeof(Missile),"CalcRange"),postfix:new HarmonyMethod(typeof(Plugin),nameof(AltitudeRange)));
            harmony.Patch(AccessTools.Method(typeof(Missile),"GetMinRange"),postfix:new HarmonyMethod(typeof(Plugin),nameof(MinimumRange)));
            harmony.Patch(AccessTools.Method(typeof(Missile),"Steering"),prefix:new HarmonyMethod(typeof(Plugin),nameof(SteeringStart)),postfix:new HarmonyMethod(typeof(Plugin),nameof(SteeringEnd)));
            harmony.Patch(AccessTools.Method(typeof(Missile),"DetectCollisions"),prefix:new HarmonyMethod(typeof(Plugin),nameof(ContactStart)),transpiler:new HarmonyMethod(typeof(Plugin),nameof(ContactQueries)));
            Logger.LogInfo("Killjoy: two-stage 16-25 km loft, altitude-dependent range, native fuses and KR-67 central pylon enabled.");
        }
        private static void AltitudeRange(Missile __instance,float launchSpeed,float launchAltitude,ref float __result,ref float noEscapeDistance)
        {
            if(!Is(__instance))return;
            __result=LaunchEnvelope.MaximumRange(launchAltitude,launchSpeed);
            noEscapeDistance=__result*.65f;
        }
        private static void MinimumRange(Missile __instance,ref float __result)
        {if(Is(__instance))__result=LaunchEnvelope.MinimumRange;}
        private static void SteeringStart(Missile __instance){if(Is(__instance)&&__instance.LocalSim)__instance.GetComponent<KilljoyFlight>()?.SteeringAim();}
        private static void SteeringEnd(Missile __instance){if(Is(__instance)&&__instance.LocalSim)__instance.GetComponent<KilljoyFlight>()?.ContactAim();}
        private static void ContactStart(Missile __instance)
        {if(Is(__instance)&&__instance.LocalSim)__instance.GetComponent<KilljoyFlight>()?.BeginContactCheck();}
        private static IEnumerable<CodeInstruction> ContactQueries(IEnumerable<CodeInstruction> instructions)
        {
            var original=AccessTools.Method(typeof(Physics),nameof(Physics.Linecast),new[]{typeof(Vector3),typeof(Vector3),typeof(RaycastHit).MakeByRefType(),typeof(int)});
            var replacement=AccessTools.Method(typeof(Plugin),nameof(ContactLinecast));
            int replaced=0;
            foreach(var instruction in instructions){
                if(instruction.Calls(original)){
                    var owner=new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0);
                    owner.labels.AddRange(instruction.labels);instruction.labels.Clear();
                    yield return owner;
                    instruction.opcode=System.Reflection.Emit.OpCodes.Call;instruction.operand=replacement;replaced++;
                }
                yield return instruction;
            }
            if(replaced!=2)throw new InvalidOperationException("Unexpected Missile.DetectCollisions queries: "+replaced);
        }
        private static bool ContactLinecast(Vector3 start,Vector3 end,out RaycastHit hit,int mask,Missile owner)
        {
            if(!Is(owner))return Physics.Linecast(start,end,out hit,mask);
            var flight=owner.GetComponent<KilljoyFlight>();
            if(flight!=null)start=flight.CollisionStart(start);
            Vector3 travel=end-start;hit=default;
            if(travel.sqrMagnitude<.000001f)return false;
            var hits=Physics.RaycastAll(start,travel.normalized,travel.magnitude,mask,QueryTriggerInteraction.Collide);
            foreach(var candidate in hits.OrderBy(h=>h.distance)){
                bool own=candidate.collider.transform.IsChildOf(owner.transform)||candidate.collider.attachedRigidbody==owner.rb;
                if(own||candidate.collider.isTrigger){
                    if(flight==null||flight.FirstRejectedContact(candidate.collider))Trace($"Killjoy rejected contact collider={candidate.collider.name} own={own} trigger={candidate.collider.isTrigger} layer={candidate.collider.gameObject.layer}");
                    continue;
                }
                hit=candidate;
                Trace($"Killjoy physical contact collider={hit.collider.name} layer={hit.collider.gameObject.layer} point={hit.point} tangible={owner.IsTangible()} armed={owner.IsArmed()}");
                return true;
            }
            return false;
        }
        // Native Initialize, SlowChecks and Seek/Fusing still run. Only SetTrajectory is replaced.
        private static void FlightInitialized(BallisticMissileGuidance __instance,Unit target,GlobalPosition aimpoint)
        {
            var missile=SeekerMissile.GetValue(__instance) as Missile;if(!Is(missile))return;
            var flight=missile.GetComponent<KilljoyFlight>()??missile.gameObject.AddComponent<KilljoyFlight>();
            flight.Bind(missile,target,(GlobalPosition)KnownPosition.GetValue(__instance));
        }
        private static bool Trajectory(BallisticMissileGuidance __instance)
        {
            var missile=SeekerMissile.GetValue(__instance) as Missile;if(!Is(missile))return true;
            var flight=missile.GetComponent<KilljoyFlight>();if(flight==null)return true;
            flight.Guide();
            KnownPosition.SetValue(__instance,flight.TargetPosition);
            KnownVelocity.SetValue(__instance,flight.TargetVelocity);
            return false;
        }
        private static void ClientStage(Missile __instance)
        {
            if(!Is(__instance)||__instance.LocalSim)return;
            var flight=__instance.GetComponent<KilljoyFlight>()??__instance.gameObject.AddComponent<KilljoyFlight>();
            flight.Bind(__instance,null,__instance.GlobalPosition());
        }
        // Observation only: never suppress or replace a native detonation.
        private static void TraceDetonation(Missile __instance,bool hitArmor,bool hitTerrain)
        {
            if(!Is(__instance)||!__instance.LocalSim)return;
            var flight=__instance.GetComponent<KilljoyFlight>();
            if(flight!=null){Vector3 error=flight.TargetPosition-__instance.GlobalPosition();Trace($"Killjoy impact error horizontal={new Vector2(error.x,error.z).magnitude:0.0}m vertical={error.y:0.0}m targetSpeed={flight.TargetVelocity.magnitude:0.0}m/s");}
            Trace($"Killjoy detonation age={__instance.timeSinceSpawn:0.0}s altitude={__instance.GlobalPosition().y:0.0}m armor={hitArmor} terrain={hitTerrain} nuclear={__instance.GetWeaponInfo().nuclear} tangible={__instance.IsTangible()} armed={__instance.IsArmed()}\n{new System.Diagnostics.StackTrace(2)}");
        }
        private void Update()
        {
            if(Time.unscaledTime<refresh)return;refresh=Time.unscaledTime+1;
            if(cachedMounts.Length==0||cachedMounts.Any(m=>m==null)){
                refresh=Time.unscaledTime+5;
                cachedMounts=Resources.FindObjectsOfTypeAll<WeaponMount>().Where(m=>m.jsonKey!=null&&m.jsonKey.StartsWith("Kinzhal_",StringComparison.Ordinal)).ToArray();
                definitionsReady=false;
            }
            var mounts=cachedMounts;if(mounts.Length==0)return;
            if(definitionsReady)return;
            refresh=Time.unscaledTime+5;
            foreach(var definition in Resources.FindObjectsOfTypeAll<AircraftDefinition>())
                if(definition.jsonKey=="Multirole1"&&definition.unitPrefab!=null)
                {
                    var aircraft=definition.unitPrefab.GetComponent<Aircraft>();
                    var manager=definition.unitPrefab.GetComponentInChildren<WeaponManager>(true);
                    if(aircraft&&manager)AddCentralPylon(aircraft,manager,mounts);
                    if(manager&&definition.aircraftParameters!=null){
                        foreach(var loadout in definition.aircraftParameters.loadouts)
                            while(loadout.weapons.Count<manager.hardpointSets.Length)loadout.weapons.Add(null);
                        definitionsReady=manager.hardpointSets.Any(h=>h.name=="Killjoy Central Heavy Pylon");
                    }
                }
        }
        private static void Manager(WeaponManager __instance)
        {
            var aircraft=__instance.GetComponentInParent<Aircraft>();
            if(!aircraft||aircraft.definition.jsonKey!="Multirole1")return;
            AddCentralPylon(aircraft,__instance,Resources.FindObjectsOfTypeAll<WeaponMount>());
            if(aircraft.loadout!=null)while(aircraft.loadout.weapons.Count<__instance.hardpointSets.Length)aircraft.loadout.weapons.Add(null);
        }
        private static void AddCentralPylon(Aircraft aircraft,WeaponManager manager,IEnumerable<WeaponMount> mounts)
        {
            var options=mounts.Where(m=>m.jsonKey=="Kinzhal_HE_Single"||m.jsonKey=="Kinzhal_Nuclear_Single").ToList();
            if(options.Count==0)return;
            var existing=manager.hardpointSets.FirstOrDefault(h=>h.name=="Killjoy Central Heavy Pylon");
            if(existing!=null){existing.weaponOptions=options;return;}
            if(manager.hardpointSets.Length<5)return;
            var source=manager.hardpointSets[1].hardpoints.FirstOrDefault();
            if(source==null||source.part==null)return;
            var pivot=new GameObject("KinzhalCentralHardpoint").transform;pivot.SetParent(source.part.transform,false);
            // Center the long weapon below the fuselage; both internal bays are precluded while it is loaded.
            pivot.position=aircraft.transform.TransformPoint(new Vector3(0,-.85f,0));pivot.rotation=aircraft.transform.rotation;
            int index=manager.hardpointSets.Length;
            var hp=new Hardpoint{transform=pivot,part=source.part,bayDoors=new BayDoor[0],BuiltInWeapons=new Weapon[0],BuiltInTurrets=new Turret[0],HardpointIndex=index};
            var pylonOptions=AccessTools.Field(typeof(Hardpoint),"pylonOptions");pylonOptions.SetValue(hp,Array.CreateInstance(pylonOptions.FieldType.GetElementType(),0));
            var set=new HardpointSet{name="Killjoy Central Heavy Pylon",precludingHardpointSets=new List<byte>{1,2},weaponOptions=options,hardpoints=new List<Hardpoint>{hp}};
            var all=manager.hardpointSets.ToList();all.Add(set);manager.hardpointSets=all.ToArray();
            foreach(int bay in new[]{1,2})if(!manager.hardpointSets[bay].precludingHardpointSets.Contains((byte)index))manager.hardpointSets[bay].precludingHardpointSets.Add((byte)index);
        }
        private void OnDestroy(){harmony?.UnpatchSelf();if(Instance==this)Instance=null;}
    }
}
