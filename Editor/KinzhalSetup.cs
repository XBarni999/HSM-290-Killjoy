using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Blueprinter;

[InitializeOnLoad]
public static class KinzhalSetup
{
    const string R="Assets/Blueprinter/Mods/Kh47M2/";
    const string D="Assets/Blueprinter/_donotship/";
    static KinzhalSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string request=R+"Tools~/setup.request";
        if(!File.Exists(request))return;
        string action=File.ReadAllText(request).Trim(); File.Delete(request);
        if(action=="build"){
            EditorApplication.delayCall+=()=>{try{Build();File.WriteAllText(R+"Tools~/setup.result","KINZHAL_BUILD_OK");}catch(Exception e){File.WriteAllText(R+"Tools~/setup.result",e.ToString());Debug.LogException(e);}};return;
        }
        try { if(action=="inspect")Inspect();else if(action=="preview"){Validate();Preview();}else Create(); File.WriteAllText(R+"Tools~/setup.result","KINZHAL_"+action.ToUpperInvariant()+"_OK"); }
        catch(Exception e){File.WriteAllText(R+"Tools~/setup.result",e.ToString());Debug.LogException(e);}
    }
    static T Load<T>(string p) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(p)??throw new Exception("Missing "+p);
    static SerializedProperty P(SerializedObject s,string n)=>s.FindProperty(n)??throw new Exception(s.targetObject.name+": "+n);
    static void Inspect()
    {
        var b=new StringBuilder();
        var model=Load<GameObject>(R+"Models/Kh47M2_Kinzhal.fbx");
        foreach(var t in model.GetComponentsInChildren<Transform>(true)) b.AppendLine("MODEL "+t.name+" pos="+t.localPosition+" rot="+t.localEulerAngles+" scale="+t.localScale);
        foreach(var r in model.GetComponentsInChildren<Renderer>(true)) b.AppendLine("RENDER "+r.name+" bounds="+r.bounds+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m?m.name:"null")));
        foreach(var mf in model.GetComponentsInChildren<MeshFilter>(true)){
            var mesh=mf.sharedMesh;double volume=0;var vs=mesh.vertices;var ts=mesh.triangles;
            for(int i=0;i<ts.Length;i+=3)volume+=Vector3.Dot(vs[ts[i]],Vector3.Cross(vs[ts[i+1]],vs[ts[i+2]]))/6;
            int opposed=0;var ns=mesh.normals;for(int i=0;i<ts.Length;i+=3){var normal=Vector3.Cross(vs[ts[i+1]]-vs[ts[i]],vs[ts[i+2]]-vs[ts[i]]);if(Vector3.Dot(normal,ns[ts[i]]+ns[ts[i+1]]+ns[ts[i+2]])<0)opposed++;}b.AppendLine("MESH "+mf.name+" signedVolume="+volume+" UV="+mesh.uv.Length+" normals="+mesh.normals.Length+" opposedNormals="+opposed+"/"+ts.Length/3);
        }
        foreach(var c in AssetDatabase.LoadAllAssetsAtPath(R+"Models/Kh47M2_Kinzhal.fbx").OfType<AnimationClip>())
        { b.AppendLine("CLIP "+c.name+" length="+c.length); foreach(var binding in AnimationUtility.GetCurveBindings(c)) b.AppendLine("CURVE "+binding.path+" "+binding.propertyName); }
        foreach(var name in new[]{"ballisticMissile1_PLACEHOLDER","BallisticMissile1_single_PLACEHOLDER","BallisticMissile1_internalx2_PLACEHOLDER","Multirole1_PLACEHOLDER","Darkreach_PLACEHOLDER"})
        {
            var go=Load<GameObject>(D+"GameObject/"+name+".prefab");
            b.AppendLine("PREFAB "+name);
            foreach(var c in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(!c){b.AppendLine("MISSING SCRIPT");continue;}
                b.AppendLine("COMP "+c.name+" "+c.GetType().Name);
                if(c.GetType().Name=="WeaponManager"){
                    var hp=P(new SerializedObject(c),"hardpointSets");
                    for(int i=0;i<hp.arraySize;i++){
                        var set=hp.GetArrayElementAtIndex(i); b.AppendLine("HP "+i+" "+set.FindPropertyRelative("name").stringValue);
                        var options=set.FindPropertyRelative("weaponOptions");for(int j=0;j<options.arraySize;j++){var m=options.GetArrayElementAtIndex(j).objectReferenceValue as WeaponMount;if(m)b.AppendLine("OPTION "+m.jsonKey);}
                    }
                }
            }
        }
        File.WriteAllText(R+"Validation~/inspection.txt",b.ToString());
    }
    static void Edit(UnityEngine.Object o,Action<SerializedObject> a){var s=new SerializedObject(o);a(s);s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(o);}
    static T Copy<T>(string source,string name) where T:UnityEngine.Object
    {string path=R+name+".asset";if(!File.Exists(path)&&!AssetDatabase.CopyAsset(source,path))throw new Exception(source);var o=Load<T>(path);o.name=name;return o;}
    static GameObject Clone(string p){var g=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(p));PrefabUtility.UnpackPrefabInstance(g,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);return g;}
    static Bounds BoundsOf(GameObject g){var rs=g.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
    static GameObject Visual(Transform parent)
    {
        var pivot=new GameObject("KinzhalVisual");pivot.transform.SetParent(parent,false);
        var g=Clone(R+"Models/Kh47M2_Kinzhal.fbx");g.transform.SetParent(pivot.transform,false);
        foreach(var camera in g.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(camera.gameObject);
        foreach(var light in g.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(light.gameObject);
        foreach(var c in g.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(c);
        // FBX importer maps the Blender +Z nose to Unity +Y. Correct only the visual pivot.
        pivot.transform.localRotation=Quaternion.Euler(90,0,0);
        var seeker=g.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="SeekerPoint");
        if(Vector3.Dot(seeker.position-pivot.transform.position,parent.forward)<0)pivot.transform.localRotation=Quaternion.Euler(-90,0,0);
        var bounds=BoundsOf(pivot);pivot.transform.position-=bounds.center-parent.position;
        // Preserve body UVs and assign an independent matte material only to the aft nozzle.
        var boosterRenderer=g.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="Kh47M2_Booster");
        var nozzleMaterial=AssetDatabase.LoadAssetAtPath<Material>(R+"Materials/M_KinzhalBooster.mat");
        if(!nozzleMaterial){nozzleMaterial=new Material(boosterRenderer.sharedMaterial);AssetDatabase.CreateAsset(nozzleMaterial,R+"Materials/M_KinzhalBooster.mat");}
        EditorUtility.CopySerialized(boosterRenderer.sharedMaterial,nozzleMaterial);nozzleMaterial.name="M_KinzhalBooster";
        nozzleMaterial.SetFloat("_Cull",0);nozzleMaterial.SetTexture("_MetallicGlossMap",null);nozzleMaterial.DisableKeyword("_METALLICSPECGLOSSMAP");nozzleMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");nozzleMaterial.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");nozzleMaterial.SetFloat("_Metallic",.05f);nozzleMaterial.SetFloat("_Smoothness",.25f);nozzleMaterial.SetFloat("_SpecularHighlights",0);nozzleMaterial.SetFloat("_EnvironmentReflections",0);nozzleMaterial.SetColor("_BaseColor",new Color(.55f,.55f,.55f));EditorUtility.SetDirty(nozzleMaterial);
        var filter=boosterRenderer.GetComponent<MeshFilter>();var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="KinzhalBoosterNozzle";
        var vertices=mesh.vertices;var triangles=mesh.triangles;var bodyTriangles=new System.Collections.Generic.List<int>();var nozzleTriangles=new System.Collections.Generic.List<int>();
        var points=vertices.Select(v=>parent.InverseTransformPoint(boosterRenderer.transform.TransformPoint(v))).ToArray();float aft=points.Min(v=>v.z);
        for(int i=0;i<triangles.Length;i+=3){bool nozzle=Enumerable.Range(0,3).All(j=>{var v=points[triangles[i+j]];return v.z<aft+.65f&&v.x*v.x+v.y*v.y<.36f;});var list=nozzle?nozzleTriangles:bodyTriangles;list.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});}
        if(nozzleTriangles.Count==0)throw new Exception("No nozzle faces classified");
        mesh.subMeshCount=2;mesh.SetTriangles(bodyTriangles,0);mesh.SetTriangles(nozzleTriangles,1);
        string meshPath=R+"KinzhalBoosterNozzle.asset";var oldMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(oldMesh){EditorUtility.CopySerialized(mesh,oldMesh);UnityEngine.Object.DestroyImmediate(mesh);mesh=oldMesh;}else AssetDatabase.CreateAsset(mesh,meshPath);
        filter.sharedMesh=mesh;boosterRenderer.sharedMaterials=new[]{boosterRenderer.sharedMaterial,nozzleMaterial};

        foreach(var t in g.GetComponentsInChildren<Transform>(true))t.gameObject.layer=parent.gameObject.layer;
        var anim=g.AddComponent<Animation>();var clip=Load<AnimationClip>(R+"KinzhalFinDeploy.anim");anim.AddClip(clip,clip.name);anim.clip=clip;anim.playAutomatically=false;clip.SampleAnimation(g,0);
        return pivot;
    }
    static void HideMeshes(GameObject g){foreach(var r in g.GetComponentsInChildren<MeshRenderer>(true))r.enabled=false;foreach(var lod in g.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(lod);}
    static void CreateClip()
    {
        var sources=AssetDatabase.LoadAllAssetsAtPath(R+"Models/Kh47M2_Kinzhal.fbx").OfType<AnimationClip>().ToArray();
        var c=new AnimationClip{name="KinzhalFinDeploy",legacy=true,frameRate=24,wrapMode=WrapMode.ClampForever};
        foreach(var fin in new[]{"Bottom","Left","Right","Top"}){
            string name="Warhead_Fin_"+fin;
            var source=sources.FirstOrDefault(x=>x.name==name+"|"+name+"Action")??sources.First(x=>AnimationUtility.GetCurveBindings(x).Any(b=>b.path.EndsWith(name)));
            foreach(var binding in AnimationUtility.GetCurveBindings(source).Where(b=>b.type==typeof(Transform)&&b.path.EndsWith(name))){
                var curve=AnimationUtility.GetEditorCurve(source,binding);
                var keys=curve.keys.Select(k=>{k.time*=.8f/source.length;k.inTangent*=source.length/.8f;k.outTangent*=source.length/.8f;return k;}).ToArray();
                AnimationUtility.SetEditorCurve(c,binding,new AnimationCurve(keys));
            }
        }
        c.EnsureQuaternionContinuity();
        var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(R+"KinzhalFinDeploy.anim");if(old){EditorUtility.CopySerialized(c,old);UnityEngine.Object.DestroyImmediate(c);}else AssetDatabase.CreateAsset(c,R+"KinzhalFinDeploy.anim");
    }
    static void CreateVariant(bool nuclear)
    {
        string suffix=nuclear?"_Nuclear":"_HE";
        var def=Copy<MissileDefinition>(D+"MonoBehaviour/ballisticMissile1"+(nuclear?"_tacNuke":"")+"_PLACEHOLDER.asset","Def_Kinzhal"+suffix);
        var info=Copy<WeaponInfo>(D+"MonoBehaviour/ballisticMissile1"+(nuclear?"_tacNuke":"")+"_info_PLACEHOLDER.asset","WI_Kinzhal"+suffix);
        string description="Heavy air-launched two-stage aeroballistic missile. Range depends on launch altitude and speed; no minimum release altitude. Boost, guided coast and steep terminal dive. Runtime plugin required.";
        Edit(def,s=>{P(s,"jsonKey").stringValue="Kh47M2"+suffix;P(s,"unitName").stringValue="HSM-290 \"Killjoy\""+(nuclear?" (Nuclear)":" (HE)");P(s,"bogeyName").stringValue="Killjoy";P(s,"description").stringValue=description;P(s,"code").stringValue="HSM-290";P(s,"mass").floatValue=4300;P(s,"length").floatValue=7.2f;P(s,"width").floatValue=2.2f;P(s,"height").floatValue=2.2f;});
        Edit(info,s=>{P(s,"weaponName").stringValue=nuclear?"HSM-290 \"Killjoy\" (Nuclear)":"HSM-290 \"Killjoy\" (HE)";var icon=P(s,"weaponIcon");if(icon.objectReferenceValue==null||icon.objectReferenceValue.name=="KinzhalWeaponIcon")icon.objectReferenceValue=Load<Sprite>(R+"Kh47.png");P(s,"shortName").stringValue=nuclear?"KILLJOY N":"KILLJOY HE";P(s,"description").stringValue=description;P(s,"massPerRound").floatValue=4300;P(s,"maxSpeed").floatValue=1550;P(s,"pierceDamage").floatValue=3500;P(s,"costPerRound").floatValue=nuclear?35:14;P(s,"targetRequirements.minRange").floatValue=48000;P(s,"targetRequirements.maxRange").floatValue=360000;P(s,"targetRequirements.maxSpeed").floatValue=45;P(s,"targetRequirements.minAlignment").floatValue=30;P(s,"targetRequirements.minAltitude").floatValue=0;});
        var g=Clone(D+"GameObject/ballisticMissile1"+(nuclear?"_tacNuke":"")+"_PLACEHOLDER.prefab");g.name="Kinzhal"+suffix;HideMeshes(g);var visual=Visual(g.transform);
        var missile=g.GetComponent<Missile>();
        var warheadRenderer=visual.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="Kh47M2_Warhead");
        var src=warheadRenderer.GetComponent<MeshFilter>().sharedMesh;
        var noseMesh=UnityEngine.Object.Instantiate(src);noseMesh.name="KinzhalNoseGlow";
        var vertices=src.vertices;var tris=src.triangles;var kept=new System.Collections.Generic.List<int>();
        for(int n=0;n<tris.Length;n+=3)if(Enumerable.Range(0,3).All(j=>g.transform.InverseTransformPoint(warheadRenderer.transform.TransformPoint(vertices[tris[n+j]])).z>2.65f))kept.AddRange(new[]{tris[n],tris[n+1],tris[n+2]});
        noseMesh.triangles=kept.ToArray();noseMesh.RecalculateBounds();
        string nosePath=R+"KinzhalNoseGlow.asset";var oldMesh=AssetDatabase.LoadAssetAtPath<Mesh>(nosePath);if(oldMesh){EditorUtility.CopySerialized(noseMesh,oldMesh);UnityEngine.Object.DestroyImmediate(noseMesh);noseMesh=oldMesh;}else AssetDatabase.CreateAsset(noseMesh,nosePath);
        var glowMaterial=AssetDatabase.LoadAssetAtPath<Material>(R+"Materials/M_KinzhalNoseGlow.mat");if(!glowMaterial){glowMaterial=new Material(Load<Material>(R+"Materials/M_Kh47M2_Kinzhal.mat"));AssetDatabase.CreateAsset(glowMaterial,R+"Materials/M_KinzhalNoseGlow.mat");}glowMaterial.EnableKeyword("_EMISSION");glowMaterial.SetColor("_EmissionColor",Color.black);
        var nose=new GameObject("KinzhalNoseGlow");nose.transform.SetParent(warheadRenderer.transform,false);nose.transform.localScale=Vector3.one*1.002f;nose.layer=g.layer;nose.AddComponent<MeshFilter>().sharedMesh=noseMesh;nose.AddComponent<MeshRenderer>().sharedMaterial=glowMaterial;
        var boom=g.AddComponent<AudioSource>();boom.clip=Load<AudioClip>(D+"AudioClip/sonicBoom1_PLACEHOLDER.ogg");boom.playOnAwake=false;boom.spatialBlend=1;boom.minDistance=800;boom.maxDistance=8000;boom.rolloffMode=AudioRolloffMode.Linear;boom.volume=1;
        Edit(missile,s=>{
            P(s,"definition").objectReferenceValue=def;P(s,"info").objectReferenceValue=info;P(s,"mass").floatValue=4300;P(s,"finArea").floatValue=.65f;P(s,"torque").floatValue=2.2f;P(s,"gLimit").floatValue=8;P(s,"maxTurnRate").floatValue=9;
            P(s,"pierceDamage").floatValue=3500;P(s,"blastYield").floatValue=nuclear?1000000:650;P(s,"impactFuseDelay").floatValue=.008f;
            var motors=P(s,"motors");motors.arraySize=1;
            for(int i=0;i<1;i++){var m=motors.GetArrayElementAtIndex(i);m.FindPropertyRelative("activated").boolValue=false;m.FindPropertyRelative("delayTimer").floatValue=1.2f;m.FindPropertyRelative("thrust").floatValue=160000;m.FindPropertyRelative("burnTime").floatValue=28;m.FindPropertyRelative("fuelMass").floatValue=2000;m.FindPropertyRelative("topSpeed").floatValue=1550;m.FindPropertyRelative("thrustVectoring").floatValue=0;}
        });
        Edit(g.GetComponent<BallisticMissileGuidance>(),s=>{P(s,"armDelay").floatValue=nuclear?1000:40;P(s,"airburstHeight").floatValue=nuclear?200:0;P(s,"tangibleDelay").floatValue=5;P(s,"circularError").floatValue=5;P(s,"maxTargetSpeed").floatValue=45;});
        var b=BoundsOf(visual);var col=g.GetComponent<CapsuleCollider>();col.center=g.transform.InverseTransformPoint(b.center);col.radius=Mathf.Max(.1f,Mathf.Max(b.size.x,b.size.y)*.5f);col.height=b.size.z;col.direction=2;g.GetComponent<Rigidbody>().mass=4300;
        var exhaust=visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="ExhaustPoint_Booster");
        foreach(var t in g.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="FireParticles"||t.name=="smokeParticles"||t.name=="smokeTrail")){t.position=exhaust.position;t.rotation=Quaternion.LookRotation(-g.transform.forward);}
        PrefabUtility.SaveAsPrefabAsset(g,R+"Kinzhal"+suffix+".prefab");UnityEngine.Object.DestroyImmediate(g);
        var prefab=Load<GameObject>(R+"Kinzhal"+suffix+".prefab");Edit(def,s=>P(s,"unitPrefab").objectReferenceValue=prefab);Edit(info,s=>P(s,"weaponPrefab").objectReferenceValue=prefab);
        foreach(bool twin in new[]{false,true}){
            string name="Kinzhal"+suffix+(twin?"_Darkreach":"_Single");
            var mount=Copy<WeaponMount>(D+"MonoBehaviour/BallisticMissile1"+(nuclear?"_tacNuke":"")+(twin?"_internalx2":"_single")+"_PLACEHOLDER.asset","WM_"+name);
            // Each normal internal bay hardpoint receives one missile.
            var rail=Clone(D+"GameObject/BallisticMissile1_single_PLACEHOLDER.prefab");rail.name=name;HideMeshes(rail);
            if(!twin){
                var hardware=new GameObject("KinzhalVanillaPylon");hardware.transform.SetParent(rail.transform,false);
                hardware.AddComponent<MeshFilter>().sharedMesh=Load<Mesh>(D+"Mesh/pylon_large_single_PLACEHOLDER.asset");hardware.AddComponent<MeshRenderer>().sharedMaterial=Load<Material>(R+"Materials/M_KinzhalHeavyPylon.mat");
                hardware.transform.localScale=new Vector3(8,1,1);var hb=BoundsOf(hardware);
                hardware.transform.position+=rail.transform.TransformPoint(new Vector3(0,hb.extents.y,0))-hb.center;
                foreach(var t in hardware.GetComponentsInChildren<Transform>(true))t.gameObject.layer=rail.layer;
            }

            foreach(var station in rail.GetComponentsInChildren<MountedMissile>(true)){
                var v=Visual(station.transform);var cc=station.GetComponent<CapsuleCollider>();if(cc){cc.height=7.2f;cc.radius=.45f;cc.center=Vector3.zero;}
                Edit(station,s=>{P(s,"info").objectReferenceValue=info;P(s,"ammo").intValue=1;P(s,"railDirection").enumValueIndex=1;P(s,"railLength").floatValue=2;P(s,"railSpeed").floatValue=7;P(s,"railDelay").floatValue=0;});
            }
            PrefabUtility.SaveAsPrefabAsset(rail,R+name+".prefab");UnityEngine.Object.DestroyImmediate(rail);
            Edit(mount,s=>{P(s,"jsonKey").stringValue=name;P(s,"mountName").stringValue="Killjoy "+(nuclear?"Nuclear":"HE")+(twin?" (internal)":"");P(s,"prefab").objectReferenceValue=Load<GameObject>(R+name+".prefab");P(s,"info").objectReferenceValue=info;P(s,"ammo").intValue=1;P(s,"mass").floatValue=4300;P(s,"drag").floatValue=twin?0:1.8f;P(s,"disabled").boolValue=false;P(s,"missileBay").boolValue=twin;});
            if(!twin){
                string path=R+"Op_Kinzhal"+suffix+"_Carriers.asset";var op=AssetDatabase.LoadAssetAtPath<OpAddWeaponToHardpoint>(path);if(!op){op=ScriptableObject.CreateInstance<OpAddWeaponToHardpoint>();AssetDatabase.CreateAsset(op,path);}op.weaponJsonKey=name;op.aircraft.Clear();
                op.aircraft.Add(new OpAddWeaponToHardpoint.AircraftTarget{aircraftJsonKey="FastBomber1",hardpointIndices=new System.Collections.Generic.List<int>{3}});EditorUtility.SetDirty(op);
            }
            if(twin){
                string path=R+"Op_"+name+".asset";var op=AssetDatabase.LoadAssetAtPath<OpAddWeaponToHardpoint>(path);if(!op){op=ScriptableObject.CreateInstance<OpAddWeaponToHardpoint>();AssetDatabase.CreateAsset(op,path);}op.weaponJsonKey=name;op.aircraft.Clear();
                // Combined inner bay has two hardpoints, one missile at each; other bays remain excluded by the native set.
                op.aircraft.Add(new OpAddWeaponToHardpoint.AircraftTarget{aircraftJsonKey="Darkreach",hardpointIndices=new System.Collections.Generic.List<int>{1,2}});EditorUtility.SetDirty(op);
            }
        }
    }
    [MenuItem("Blueprinter/HSM-290 Killjoy/Create or update assets")]
    public static void Create()
    {
        CreateClip();CreateVariant(false);CreateVariant(true);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();Validate();Preview();
    }
    [MenuItem("Blueprinter/HSM-290 Killjoy/Build bundle")]
    public static void Build()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);AssetDatabase.SaveAssets();
        ConfigureNativeFins();
        Validate();const string output="Releases/Kh47M2";Directory.CreateDirectory(output);
        var started=DateTime.UtcNow;
        ModBuilder.Build("Kh47M2","HSM-290 Killjoy","1.1.0",output);
        string path=output+"/HSM-290 Killjoy_1.1.0.nobp";if(!File.Exists(path)||File.GetLastWriteTimeUtc(path)<started.AddSeconds(-1))throw new Exception("Fresh bundle not produced");
        File.Copy(path,R+"Tools~/Runtime/Kinzhal/Bundle/Kh47M2.nobp",true);
    }
    // Bake the supplied clip endpoints into the game's networked FoldingFin data.
    // This only updates existing missile prefabs; it does not regenerate models or tuning.
    static void ConfigureNativeFins()
    {
        foreach(var suffix in new[]{"HE","Nuclear"}){
            string path=R+"Kinzhal_"+suffix+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try {
                var animation=root.GetComponentInChildren<Animation>(true);
                var fins=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Warhead_Fin_",StringComparison.Ordinal)).ToArray();
                animation.clip.SampleAnimation(animation.gameObject,0);
                var folded=fins.Select(t=>t.localEulerAngles).ToArray();
                animation.clip.SampleAnimation(animation.gameObject,animation.clip.length);
                var deployed=fins.Select(t=>t.localEulerAngles).ToArray();
                animation.clip.SampleAnimation(animation.gameObject,0);
                Edit(root.GetComponent<Missile>(),s=>{
                    var array=P(s,"foldingFins");array.arraySize=fins.Length;
                    for(int i=0;i<fins.Length;i++){
                        var fin=array.GetArrayElementAtIndex(i);
                        fin.FindPropertyRelative("fin").objectReferenceValue=fins[i];
                        fin.FindPropertyRelative("foldAngle").vector3Value=folded[i];
                        // Shortest Euler path, avoiding a full rotation across 0/360.
                        fin.FindPropertyRelative("deployAngle").vector3Value=folded[i]+new Vector3(Mathf.DeltaAngle(folded[i].x,deployed[i].x),Mathf.DeltaAngle(folded[i].y,deployed[i].y),Mathf.DeltaAngle(folded[i].z,deployed[i].z));
                        fin.FindPropertyRelative("deploySpeed").floatValue=1.25f;
                    }
                });
                PrefabUtility.SaveAsPrefabAsset(root,path);
            } finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Blueprinter/HSM-290 Killjoy/Validate assets")]
    public static void Validate()
    {
        foreach(var path in Directory.GetFiles(R,"*.prefab")){
            var g=Load<GameObject>(path.Replace('\\','/'));foreach(var t in g.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script "+path);
            foreach(var component in g.GetComponentsInChildren<MonoBehaviour>(true)){var it=new SerializedObject(component).GetIterator();while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue==null&&it.objectReferenceInstanceIDValue!=0)throw new Exception("Broken reference "+path+" "+it.propertyPath);}
            if(g.GetComponentsInChildren<Camera>(true).Any(c=>c.name=="MainCam")||g.GetComponentsInChildren<Light>(true).Any(l=>l.name.StartsWith("Studio")))throw new Exception("Studio object shipped");
        }
        var model=Load<GameObject>(R+"Kinzhal_HE.prefab");var visual=model.transform.Find("KinzhalVisual");var seeker=visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="SeekerPoint");if(seeker.position.z<2)throw new Exception("Nose not +Z");
        var kr=Load<GameObject>(D+"GameObject/Multirole1_PLACEHOLDER.prefab");var dark=Load<GameObject>(D+"GameObject/Darkreach_PLACEHOLDER.prefab");
        var darkManager=dark.GetComponentInChildren<WeaponManager>(true);
        var alkyon=Load<GameObject>(D+"GameObject/FastBomber1_PLACEHOLDER.prefab").GetComponentInChildren<WeaponManager>(true);
        foreach(var suffix in new[]{"HE","Nuclear"}){
            var mount=Load<WeaponMount>(R+"WM_Kinzhal_"+suffix+"_Single.asset");
            if(mount.ammo!=1||mount.prefab.GetComponentsInChildren<MountedMissile>(true).Length!=1)throw new Exception("Mount must carry one missile");
            var internalMount=Load<WeaponMount>(R+"WM_Kinzhal_"+suffix+"_Darkreach.asset");
            if(!internalMount.missileBay||internalMount.drag!=0||internalMount.info!=mount.info||internalMount.ammo!=1)throw new Exception("Incorrect internal mount configuration");
            var internalOp=Load<OpAddWeaponToHardpoint>(R+"Op_Kinzhal_"+suffix+"_Darkreach.asset");
            if(internalOp.weaponJsonKey!=internalMount.jsonKey||!internalOp.aircraft.Single(a=>a.aircraftJsonKey=="Darkreach").hardpointIndices.SequenceEqual(new[]{1,2}))throw new Exception("Incorrect Darkreach operation");
            var missile=Load<GameObject>(R+"Kinzhal_"+suffix+".prefab").GetComponent<Missile>();
            var guidance=new SerializedObject(missile.GetComponent<BallisticMissileGuidance>());
            bool nuclear=suffix=="Nuclear";
            if(P(guidance,"proximityFuse").boolValue||P(guidance,"airburstHeight").floatValue!=(nuclear?200:0)||P(guidance,"armDelay").floatValue!=(nuclear?1000:40)||P(guidance,"tangibleDelay").floatValue!=5)throw new Exception("Incorrect native fuse configuration");
            var missileData=new SerializedObject(missile);
            if(!P(missileData,"impactFuse").boolValue||missile.GetWeaponInfo()!=mount.info||P(missileData,"foldingFins").arraySize!=4)throw new Exception("Incorrect missile info, impact fuse or folding fins");
            if(mount.info.nuclear!=nuclear)throw new Exception("HE/nuclear WeaponInfo mismatch");
            if(darkManager.hardpointSets[1].hardpoints.Count+darkManager.hardpointSets[2].hardpoints.Count!=4||alkyon.hardpointSets[3].hardpoints.Count!=2)throw new Exception("Carrier ammunition counts differ");
            var op=Load<OpAddWeaponToHardpoint>(R+"Op_Kinzhal_"+suffix+"_Carriers.asset");
            if(!op.aircraft.Single(a=>a.aircraftJsonKey=="FastBomber1").hardpointIndices.SequenceEqual(new[]{3}))throw new Exception("Incorrect carrier slots");
        }
        foreach(var field in new[]{"pierceDamage","aimPoint"})if(typeof(Missile).GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)==null)throw new Exception("Runtime field missing: "+field);
        foreach(var method in new[]{"Initialize","Seek"})if(typeof(BallisticMissileGuidance).GetMethod(method)==null)throw new Exception("Runtime method missing: "+method);
        foreach(var suffix in new[]{"HE","Nuclear"}){var info=Load<WeaponInfo>(R+"WI_Kinzhal_"+suffix+".asset");if(info.weaponIcon==null||info.weaponIcon.name=="KinzhalWeaponIcon")throw new Exception("User weapon icon must be preserved");}
        var report="Unity scripts and references passed. Nose +Z. Native contact/airburst fuses and four FoldingFins. Separate HE/nuclear and external/internal mounts. Runtime adds low-loft trajectory, booster separation and KR-67 central pylon.\n";
        foreach(var wm in new[]{kr.GetComponentInChildren<WeaponManager>(true),dark.GetComponentInChildren<WeaponManager>(true),alkyon})foreach(var hp in wm.hardpointSets)report+=wm.name+" / "+hp.name+" points="+hp.hardpoints.Count+"\n";
        File.WriteAllText(R+"Validation~/assets.txt",report+"Runtime launch, multiplayer and trajectory not verified in game.\n");
    }
    static void Preview()
    {
        var g=UnityEngine.Object.Instantiate(Load<GameObject>(R+"Kinzhal_HE.prefab"));g.transform.position=new Vector3(0,10000,0);
        var camera=new GameObject("KinzhalPreviewCamera").AddComponent<Camera>();camera.transform.position=g.transform.position+new Vector3(9,6,7);camera.transform.LookAt(g.transform.position);camera.orthographic=true;camera.orthographicSize=4.8f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.10f,.14f);camera.farClipPlane=60;
        var light=new GameObject("PreviewLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;light.transform.rotation=Quaternion.Euler(40,-30,0);
        var rt=new RenderTexture(1280,800,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1280,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,800),0,0);tex.Apply();File.WriteAllBytes(R+"Validation~/preview.png",tex.EncodeToPNG());
        camera.transform.position=g.transform.position+new Vector3(.3f,.2f,-9);camera.transform.LookAt(g.transform.position+Vector3.back*3.1f);camera.orthographicSize=1.4f;camera.Render();tex.ReadPixels(new Rect(0,0,1280,800),0,0);tex.Apply();File.WriteAllBytes(R+"Validation~/nozzle.png",tex.EncodeToPNG());
        g.SetActive(false);var mount=UnityEngine.Object.Instantiate(Load<GameObject>(R+"Kinzhal_HE_Single.prefab"));mount.transform.position=g.transform.position;
        camera.transform.position=mount.transform.position+new Vector3(9,4,6);camera.transform.LookAt(mount.transform.position);camera.orthographicSize=4.8f;camera.Render();tex.ReadPixels(new Rect(0,0,1280,800),0,0);tex.Apply();File.WriteAllBytes(R+"Validation~/mount.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(mount);RenderTexture.active=null;
        UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);UnityEngine.Object.DestroyImmediate(g);
    }
}
