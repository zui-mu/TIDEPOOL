using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Changes a runtime-only copy of the volume. The cold daylight source profile is preserved.
[DefaultExecutionOrder(920)]
public sealed class FloodAtmosphere : MonoBehaviour
{
    public PoolWaterCycle cycle;
    public Volume globalVolume;
    public Light sun;
    public Color floodColor=new Color(.38f,.64f,.86f,1);
    [Range(0,3)] public float darkeningStops=1.5f;
    [Range(0,.4f)] public float finalVignette=.18f;
    public float Blend {get;private set;}
    public float Exposure => adjustment!=null ? adjustment.postExposure.value : 0;
    ColorAdjustments adjustment;
    Vignette vignette;
    float initialExposure,initialVignette,initialSunIntensity;
    Color initialFilter,initialSunColor;
    void Awake()
    {
        if(!globalVolume)return;
        VolumeProfile profile=globalVolume.profile;
        if(!profile.TryGet(out adjustment))adjustment=profile.Add<ColorAdjustments>(true);
        if(!profile.TryGet(out vignette))vignette=profile.Add<Vignette>(true);
        initialExposure=adjustment.postExposure.value;initialFilter=adjustment.colorFilter.value;
        initialVignette=vignette.intensity.value;
        if(sun){initialSunColor=sun.color;initialSunIntensity=sun.intensity;}
    }
    void LateUpdate()
    {
        if(!cycle || adjustment==null)return;
        float t=0;
        if(cycle.CurrentPhase==PoolWaterCycle.Phase.Filling || cycle.CurrentPhase==PoolWaterCycle.Phase.Flooded)
        {
            float total=cycle.refillSeconds+cycle.platformFloodSeconds+cycle.hallFloodSeconds;
            t=Mathf.SmoothStep(0,1,Mathf.Clamp01(cycle.Elapsed/(total*.85f)));
        }
        Apply(t);
    }
    void Apply(float value)
    {
        Blend=value;
        if(adjustment==null)return;
        adjustment.postExposure.Override(initialExposure-darkeningStops*value);
        adjustment.colorFilter.Override(Color.Lerp(initialFilter,floodColor,value));
        vignette.intensity.Override(Mathf.Lerp(initialVignette,finalVignette,value));
        vignette.smoothness.Override(.6f);
        if(sun)
        {
            sun.color=Color.Lerp(initialSunColor,new Color(.45f,.72f,1),value);
            sun.intensity=initialSunIntensity*Mathf.Lerp(1,.55f,value);
        }
    }
    void OnDisable(){Apply(0);}
}
