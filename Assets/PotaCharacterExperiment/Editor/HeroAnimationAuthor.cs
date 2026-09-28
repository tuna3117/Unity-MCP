// Unity Editor authors, previews and samples the actual AnimationClips consumed by the game.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;

public static class HeroAnimationAuthor
{
    const string Folder = "Assets/_Project/Experiments/PotaCharacterExperiment";
    class Pose {
        public float t, drop; public Dictionary<string,Vector3> e = new Dictionary<string,Vector3>();
        public Pose(float time,float y=0){t=time;drop=y;}
        public Pose Set(string b,float x,float y=0,float z=0){e[b]=new Vector3(x,y,z);return this;}
    }
    static Pose Idle(float t) {return new Pose(t).Set("RightUpperArm",-20,0,-12).Set("RightForeArm",-78).Set("LeftUpperArm",0,0,8);}
    static Pose Run(float t,int side) {
        var p=Idle(t).Set("Hips",0,side*3,side*2).Set("Spine",9).Set("Chest",0,-side*5).Set("Head",-3,side*2);
        p.Set("RightThigh",-side*32).Set("RightShin",side>0?16:58).Set("LeftThigh",side*32).Set("LeftShin",side>0?58:16);
        p.Set("RightFoot",side>0?16:-26).Set("LeftFoot",side>0?-26:16);
        return p.Set("LeftUpperArm",side*24,0,12).Set("LeftForeArm",-50).Set("RightUpperArm",-27,0,-14).Set("RightForeArm",-84);
    }
    static Pose Pass(float t,int side) {
        return Idle(t).Set("Spine",8).Set("RightThigh",side>0?-30:4).Set("RightShin",side>0?85:10).Set("RightFoot",side>0?-30:-14)
            .Set("LeftThigh",side>0?4:-30).Set("LeftShin",side>0?10:85).Set("LeftFoot",side>0?-14:-30)
            .Set("LeftUpperArm",0,0,12).Set("LeftForeArm",-55).Set("RightUpperArm",-27,0,-14).Set("RightForeArm",-84);
    }
    static Pose Squat(float t) {return Idle(t).Set("Spine",16).Set("RightThigh",-48).Set("LeftThigh",-48).Set("RightShin",82).Set("LeftShin",82);}
    // glTF / Three's XYZ composition, then X-reflection to Unity's handedness.
    static Quaternion XYZ(Vector3 e){return Quaternion.AngleAxis(e.x,Vector3.right)*Quaternion.AngleAxis(e.y,Vector3.up)*Quaternion.AngleAxis(e.z,Vector3.forward);}
    static Quaternion Rotation(Pose p,string n){
        Vector3 e;p.e.TryGetValue(n,out e);if(n.EndsWith("UpperArm"))e.z=-e.z;var q=XYZ(e);
        var sign=n.StartsWith("Right")?1f:-1f;
        var down=Quaternion.AngleAxis(-90*sign,Vector3.forward);
        if(n.EndsWith("UpperArm"))q=q*down;
        if(n.EndsWith("ForeArm")||n.EndsWith("Hand"))q=Quaternion.Inverse(down)*q*down;
        return new Quaternion(q.x,-q.y,-q.z,q.w);
    }
    static void Curve(AnimationClip clip,string path,string property,float[] times,float[] values){
        var keys=times.Select((t,i)=>new Keyframe(t,values[i])).ToArray();var curve=new AnimationCurve(keys);
        for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
    }
    static AnimationClip Create(GameObject hero,string name,Pose[] poses,bool loop){
        var clip=new AnimationClip{name=name,frameRate=60};var times=poses.Select(p=>p.t).ToArray();
        foreach(var bone in hero.GetComponentsInChildren<Transform>().Where(x=>x.name!="HeroMesh"&&x!=hero.transform)){
            var path=AnimationUtility.CalculateTransformPath(bone,hero.transform);var qs=poses.Select(p=>Rotation(p,bone.name)).ToArray();
            for(int i=1;i<qs.Length;i++)if(Quaternion.Dot(qs[i-1],qs[i])<0)qs[i]=new Quaternion(-qs[i].x,-qs[i].y,-qs[i].z,-qs[i].w);
            for(int j=0;j<4;j++){int k=j;Curve(clip,path,"m_LocalRotation."+"xyzw"[j],times,qs.Select(q=>q[k]).ToArray());}
            if(bone.name=="Hips")Curve(clip,path,"m_LocalPosition.y",times,poses.Select(p=>.69f+p.drop).ToArray());
        }
        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
        var asset=Folder+"/"+name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);if(old!=null){EditorUtility.CopySerialized(clip,old);UnityEngine.Object.DestroyImmediate(clip);clip=old;}else AssetDatabase.CreateAsset(clip,asset);
        return clip;
    }
    // Two-bone guiding-hand IK, baked into clips. The right palm stays the ball socket.
    static void SupportBall(GameObject hero, AnimationClip clip) {
        if(clip.name!="Bekleme" && clip.name!="Atis")return;
        var all=hero.GetComponentsInChildren<Transform>();
        var right=all.First(x=>x.name=="RightHand");var upper=all.First(x=>x.name=="LeftUpperArm");
        var fore=all.First(x=>x.name=="LeftForeArm");var hand=all.First(x=>x.name=="LeftHand");
        var targets=new[]{upper,fore,hand};var keys=targets.Select(x=>new List<Quaternion>()).ToArray();var times=new List<float>();
        int count=Mathf.RoundToInt(clip.length*60)+1;
        for(int f=0;f<count;f++) {
            float t=Mathf.Min(clip.length,f/60f);clip.SampleAnimation(hero,t);
            var original=targets.Select(x=>x.localRotation).ToArray();
            var ball=right.TransformPoint(new Vector3(-.075f,-.025f,.13f));
            var goal=ball+new Vector3(.14f,-.025f,-.025f);
            var a=upper.position;float l1=Vector3.Distance(a,fore.position),l2=Vector3.Distance(fore.position,hand.position);
            var dir=(goal-a).normalized;float d=Mathf.Clamp(Vector3.Distance(a,goal),.02f,l1+l2-.002f);
            var pole=hero.transform.TransformDirection(new Vector3(1,-.35f,.3f));pole=(pole-dir*Vector3.Dot(pole,dir)).normalized;
            float along=(l1*l1-l2*l2+d*d)/(2*d),height=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            var elbow=a+dir*along+pole*height;
            upper.rotation=Quaternion.FromToRotation(fore.position-a,elbow-a)*upper.rotation;
            fore.rotation=Quaternion.FromToRotation(hand.position-fore.position,a+dir*d-fore.position)*fore.rotation;
            hand.rotation=Quaternion.LookRotation((ball-hand.position).normalized,hero.transform.up);
            float blend=clip.name=="Atis"?1-Mathf.SmoothStep(0,1,(t-.2f)/.13f):1;
            for(int j=0;j<targets.Length;j++)keys[j].Add(Quaternion.Slerp(original[j],targets[j].localRotation,blend));
            times.Add(t);
        }
        for(int j=0;j<targets.Length;j++) {
            var qs=keys[j];for(int i=1;i<qs.Count;i++)if(Quaternion.Dot(qs[i-1],qs[i])<0)qs[i]=new Quaternion(-qs[i].x,-qs[i].y,-qs[i].z,-qs[i].w);
            var path=AnimationUtility.CalculateTransformPath(targets[j],hero.transform);
            for(int k=0;k<4;k++){int axis=k;Curve(clip,path,"m_LocalRotation."+"xyzw"[axis],times.ToArray(),qs.Select(q=>q[axis]).ToArray());}
        }
        clip.EnsureQuaternionContinuity();
    }
    static void GroundClip(GameObject hero, AnimationClip clip) {
        var skin=hero.GetComponentInChildren<SkinnedMeshRenderer>();skin.quality=SkinQuality.Bone4;
        var hips=hero.GetComponentsInChildren<Transform>().First(x=>x.name=="Hips");
        var path=AnimationUtility.CalculateTransformPath(hips,hero.transform);
        var vertices=skin.sharedMesh.vertices;var weights=skin.sharedMesh.boneWeights;var binds=skin.sharedMesh.bindposes;
        var times=new List<float>();var ys=new List<float>();
        int count=Mathf.RoundToInt(clip.length*60)+1;
        for(int f=0;f<count;f++) {
            float t=Mathf.Min(clip.length,f/60f);clip.SampleAnimation(hero,t);
            foreach(var bone in skin.bones)bone.localRotation=bone.localRotation.normalized;
            float floor=float.PositiveInfinity;
            var matrices=skin.bones.Select((b,i)=>hero.transform.worldToLocalMatrix*b.localToWorldMatrix*binds[i]).ToArray();
            for(int i=0;i<vertices.Length;i++) {
                var w=weights[i];var v=vertices[i];
                var p=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                floor=Mathf.Min(floor,p.y);
            }
            float weight=1, lift=0;
            if(clip.name=="Smac") weight=t<=.9f?1:t<1.1f?1-Mathf.SmoothStep(0,1,(t-.9f)/.2f):t<2.85f?0:Mathf.SmoothStep(0,1,(t-2.85f)/.2f);
            if(clip.name=="Atis")lift=t<.16f?0:t<.35f?.14f*Mathf.SmoothStep(0,1,(t-.16f)/.19f):t<.65f?.14f*(1-Mathf.SmoothStep(0,1,(t-.35f)/.3f)):0;
            times.Add(t);ys.Add(hips.localPosition.y+weight*(.008f+lift-floor));
        }
        Curve(clip,path,"m_LocalPosition.y",times.ToArray(),ys.ToArray());

    }
    public static void BuildAndExport(){
        Build();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-potaExport");
        if(at<0||at+1>=args.Length)throw new Exception("-potaExport destination required");
        AssetDatabase.ExportPackage(Folder,args[at+1],ExportPackageOptions.Recurse);
    }
    [MenuItem("Pota/Character/Build and bake animations")]
    public static void Build(){
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Hero.glb");if(prefab==null)throw new Exception("Hero.glb not imported");
        if(Application.isBatchMode) EditorSceneManager.OpenScene(Folder+"/CharacterPreview.unity");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var hero=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);hero.name="Hero";
        var clips=new List<AnimationClip>();
        var breath=Idle(1);breath.drop=.009f;breath.Set("Spine",-1.5f).Set("Head",1);
        clips.Add(Create(hero,"Bekleme",new[]{Idle(0),breath.Set("Chest",0,2).Set("LeftForeArm",-6),Idle(2)},true));
        var shot=Squat(.1f);shot.drop=-.10f;shot.Set("RightUpperArm",-62,0,-8).Set("RightForeArm",-100).Set("LeftUpperArm",-60,0,18).Set("LeftForeArm",-95);
        var release=Idle(.35f);release.drop=.20f;release.Set("Spine",-5).Set("RightUpperArm",-140,0,-28).Set("RightForeArm",-8).Set("RightHand",-25).Set("LeftUpperArm",-135,0,28).Set("LeftForeArm",-18);
        var follow=Idle(.47f);follow.drop=.10f;follow.Set("Spine",-3).Set("Head",-6).Set("RightUpperArm",-136,0,-26).Set("RightForeArm",-12).Set("RightHand",-36).Set("LeftUpperArm",-90,0,24).Set("LeftForeArm",-42);
        var settle=Squat(.65f);settle.drop=-.04f;settle.Set("RightThigh",-18).Set("LeftThigh",-18).Set("RightShin",30).Set("LeftShin",30).Set("RightUpperArm",-48,0,-16).Set("RightForeArm",-48);
        clips.Add(Create(hero,"Atis",new[]{Idle(0),shot,release,follow,settle,Idle(.85f)},false));
        clips.Add(Create(hero,"Kosu",new[]{Run(0,1),Pass(.125f,-1),Run(.25f,-1),Pass(.375f,1),Run(.5f,1)},true));
        var frames=new List<Pose>{Run(0,1),Pass(.125f,-1),Run(.25f,-1),Pass(.375f,1),Run(.5f,1),Pass(.625f,-1),Run(.75f,-1)};
        var crouch=Squat(.9f);crouch.drop=-.12f;frames.Add(crouch);
        frames.Add(Idle(1.2f).Set("Spine",-7).Set("RightUpperArm",-145,0,-25).Set("RightForeArm",-12).Set("LeftUpperArm",-40,0,24).Set("LeftForeArm",-50).Set("LeftThigh",-28).Set("LeftShin",45).Set("RightThigh",15).Set("RightShin",30));
        frames.Add(Idle(1.55f).Set("Spine",-14).Set("Head",-12).Set("RightUpperArm",40,0,-30).Set("RightForeArm",-32).Set("LeftUpperArm",-55,0,25).Set("LeftForeArm",-50).Set("LeftThigh",-45).Set("LeftShin",65).Set("RightThigh",25).Set("RightShin",40));
        Func<float,Pose> contact=t=>Idle(t).Set("Spine",-3).Set("RightUpperArm",-140,0,-28).Set("RightForeArm",-30).Set("LeftUpperArm",-35,0,24).Set("RightThigh",-20).Set("LeftThigh",-20).Set("RightShin",40).Set("LeftShin",40);
        frames.Add(contact(1.8f));frames.Add(contact(2.25f));
        frames.Add(contact(2.6f).Set("LeftUpperArm",-148,0,30).Set("LeftForeArm",-20).Set("RightUpperArm",-148,0,-30).Set("RightForeArm",-20).Set("LeftShin",55).Set("RightShin",65));
        frames.Add(Idle(2.83f).Set("RightUpperArm",-70,0,-25).Set("LeftUpperArm",-70,0,25));
        var land=Squat(3.05f);land.drop=-.11f;land.Set("RightUpperArm",20,0,-20).Set("RightForeArm",-15).Set("LeftUpperArm",20,0,20);frames.Add(land);
        Func<float,Pose> win=t=>Idle(t).Set("Spine",-5).Set("Head",-8).Set("RightUpperArm",-148,0,-32).Set("RightForeArm",-25).Set("LeftUpperArm",-20,0,28).Set("LeftForeArm",-95).Set("LeftThigh",0,0,8).Set("RightThigh",0,0,-8);
        frames.Add(Idle(3.2f).Set("Spine",5).Set("RightThigh",-10).Set("RightShin",16).Set("LeftThigh",-8).Set("LeftShin",12));frames.Add(win(3.4f));frames.Add(win(3.55f));clips.Add(Create(hero,"Smac",frames.ToArray(),false));
        clips.Add(Create(hero,"Zafer",new[]{win(0),win(.5f).Set("Chest",0,3).Set("Head",-4).Set("RightForeArm",-38),win(1)},true));
        var anim=hero.AddComponent<Animator>();
        // Unity resamples rotation curves when adding dense translation keys. Re-ground that final curve representation.
        foreach(var clip in clips){SupportBall(hero,clip);GroundClip(hero,clip);GroundClip(hero,clip);}
        var controllerPath=Folder+"/Hero.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm=controller.layers[0].stateMachine;foreach(var old in sm.states)sm.RemoveState(old.state);
        foreach(var clip in clips){var state=sm.AddState(clip.name);state.motion=clip;if(clip.name=="Bekleme")sm.defaultState=state;}
        anim.runtimeAnimatorController=controller;anim.applyRootMotion=false;
        var data=new JObject{{"generator","Unity "+Application.unityVersion+" AnimationClip.SampleAnimation"},{"fps",60}};var baked=new JArray();
        var bones=hero.GetComponentsInChildren<Transform>().Where(x=>x.name!="HeroMesh"&&x!=hero.transform).ToArray();
        foreach(var clip in clips){
            int count=Mathf.RoundToInt(clip.length*60)+1;var times=new JArray();var rotations=bones.ToDictionary(x=>x.name,x=>new JArray());var translations=new JArray();
            for(int f=0;f<count;f++){
                float time=Mathf.Min(clip.length,f/60f);times.Add(time);clip.SampleAnimation(hero,time);
                foreach(var b in bones){var q=b.localRotation.normalized;rotations[b.name].Add(q.x);rotations[b.name].Add(-q.y);rotations[b.name].Add(-q.z);rotations[b.name].Add(q.w);if(b.name=="Hips"){translations.Add(-b.localPosition.x);translations.Add(b.localPosition.y);translations.Add(b.localPosition.z);}}
            }
            var channels=new JArray();foreach(var b in bones)channels.Add(new JObject{{"bone",b.name},{"path","rotation"},{"values",rotations[b.name]}});channels.Add(new JObject{{"bone","Hips"},{"path","translation"},{"values",translations}});
            baked.Add(new JObject{{"name",clip.name},{"duration",clip.length},{"times",times},{"channels",channels}});
        }
        data["clips"]=baked;File.WriteAllText(Folder+"/animations.json",data.ToString(Newtonsoft.Json.Formatting.None));
        clips[0].SampleAnimation(hero,0);PrefabUtility.SaveAsPrefabAsset(hero,Folder+"/AnimatedHero.prefab");
        var cameraGO=new GameObject("CharacterCamera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGO,scene);var camera=cameraGO.AddComponent<Camera>();camera.transform.position=new Vector3(-2.3f,1.5f,4.5f);camera.transform.LookAt(new Vector3(0,.95f,0));camera.fieldOfView=32;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.63f,.8f,.88f);
        var lightGO=new GameObject("KeyLight");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGO,scene);var light=lightGO.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2.5f;lightGO.transform.rotation=Quaternion.Euler(35,-30,0);
        EditorSceneManager.SaveScene(scene,Folder+"/CharacterPreview.unity");EditorSceneManager.CloseScene(scene,true);AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("POTA_CHARACTER_BAKE_OK: "+clips.Count+" clips / "+bones.Length+" bones");
    }
}
