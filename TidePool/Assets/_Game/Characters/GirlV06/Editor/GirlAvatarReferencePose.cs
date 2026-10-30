using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Defines a calibrated Humanoid reference pose without modifying the FBX bind pose.
public static class GirlAvatarReferencePose
{
    public static SkeletonBone[] Calibrate(GameObject model, HumanBone[] mapping)
    {
        var instance=UnityEngine.Object.Instantiate(model);
        try
        {
            var type=typeof(Editor).Assembly.GetType("UnityEditor.AvatarSetupTool");
            if(type==null)throw new InvalidOperationException("Use Rig > Configure > Enforce T-Pose before using this Avatar.");
            var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
            var get=type.GetMethods(flags).First(m=>m.Name=="GetHumanBones"&&m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(Dictionary<string,string>));
            var bones=instance.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>true);
            var map=mapping.ToDictionary(m=>m.humanName,m=>m.boneName);
            var wrappers=get.Invoke(null,new object[]{map,bones});
            var error=type.GetMethod("GetPoseError",flags);
            var enforce=type.GetMethod("MakePoseValid",flags);
            float before=(float)error.Invoke(null,new object[]{wrappers});
            for(int i=0;i<3;i++)
            {
                enforce.Invoke(null,new object[]{wrappers});
                if((float)error.Invoke(null,new object[]{wrappers})<.01f)break;
            }
            float after=(float)error.Invoke(null,new object[]{wrappers});
            Debug.Log("Girl V06 Avatar reference calibration: "+before+" -> "+after);
            if(after>.01f)throw new InvalidOperationException("Avatar needs manual T-Pose inspection: "+after);
            return (SkeletonBone[])type.GetMethod("GetSkeletonBones",flags).Invoke(null,new object[]{instance.transform});
        }
        finally{UnityEngine.Object.DestroyImmediate(instance);}
    }
}
