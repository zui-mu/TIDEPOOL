using UnityEngine;

// The letter changes actual lamp colours; geometry and water keep local lighting contrast.
[DefaultExecutionOrder(940)]
public sealed class PoolHallLampRig : MonoBehaviour
{
    public PoolWaterCycle cycle;
    public Light[] poolLamps, hallLamps, rippleLights;
    public Light[] normalFillLights=new Light[0];
    [Min(1)] public float dimSeconds=12;
    [Min(1)] public float tintSeconds=24;
    public Renderer[] poolLenses, hallLenses;
    public Texture2D[] rippleFrames;
    public Color poolFloodColor=new Color(.055f,.68f,.82f);
    public Color hallFloodColor=new Color(.16f,.40f,.62f);
    [Range(0, 1)] public float floodedHallIntensity = .32f;
    [Range(0, 1)] public float floodedFillIntensity = 0f;
    public Color floodedAmbient = new Color(.035f, .085f, .105f);
    public float Transition {get;private set;}
    public float Dimming {get;private set;}
    Color[][] initialColors;
    float[][] initialIntensities;
    Color[] initialHallEmissions;
    Texture[] initialCookies;
    MaterialPropertyBlock block;
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    int lastFrame=-1;
    Color initialAmbient;
    void Awake()
    {
        Light[][] groups={poolLamps,hallLamps,rippleLights,normalFillLights};
        initialColors=new Color[groups.Length][];initialIntensities=new float[groups.Length][];
        for(int g=0;g<groups.Length;g++)
        {
            initialColors[g]=new Color[groups[g].Length];initialIntensities[g]=new float[groups[g].Length];
            for(int i=0;i<groups[g].Length;i++)if(groups[g][i]){initialColors[g][i]=groups[g][i].color;initialIntensities[g][i]=groups[g][i].intensity;}
        }
        initialCookies=new Texture[rippleLights.Length];for(int i=0;i<rippleLights.Length;i++)if(rippleLights[i])initialCookies[i]=rippleLights[i].cookie;
        initialHallEmissions = new Color[hallLenses?.Length ?? 0];
        for (int i = 0; i < initialHallEmissions.Length; i++)
        {
            var material = hallLenses[i] ? hallLenses[i].sharedMaterial : null;
            initialHallEmissions[i] = material && material.HasProperty(EmissionColor)
                ? material.GetColor(EmissionColor)
                : new Color(.85f, .94f, 1f) * 2.5f;
        }
        block=new MaterialPropertyBlock();
        initialAmbient=RenderSettings.ambientLight;
    }
    void LateUpdate()
    {
        if(!cycle || initialColors==null)return;
        bool flooding=cycle.CurrentPhase==PoolWaterCycle.Phase.Filling || cycle.CurrentPhase==PoolWaterCycle.Phase.Flooded;
        // The clock starts only when the letter starts Filling, never while draining the pool.
        Dimming=flooding?Mathf.SmoothStep(0,1,Mathf.Clamp01(cycle.Elapsed/Mathf.Max(1,dimSeconds))):0;
        Transition=flooding?Mathf.SmoothStep(0,1,Mathf.Clamp01(cycle.Elapsed/Mathf.Max(1,tintSeconds))):0;
        RenderSettings.ambientLight=Color.Lerp(initialAmbient,floodedAmbient,Dimming);
        var ambient=new UnityEngine.Rendering.SphericalHarmonicsL2();ambient.AddAmbientLight(RenderSettings.ambientLight);RenderSettings.ambientProbe=ambient;
        float wet=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-2.8f,-.5f,cycle.WaterHeight));
        ApplyGroup(poolLamps,0,poolFloodColor,Mathf.Lerp(1,1.35f,Transition));
        ApplyGroup(hallLamps,1,hallFloodColor,Mathf.Lerp(1,floodedHallIntensity,Dimming));
        ApplyGroup(rippleLights,2,poolFloodColor,wet*Mathf.Lerp(1,1.3f,Transition));
        for(int i=0;i<normalFillLights.Length;i++)if(normalFillLights[i])normalFillLights[i].intensity=initialIntensities[3][i]*Mathf.Lerp(1,floodedFillIntensity,Dimming);
        Lenses(poolLenses,Color.Lerp(new Color(.82f,.96f,1),poolFloodColor,Transition)*3.2f);
        float hallEmissionScale = Mathf.Lerp(1f, 1.1f / 2.5f, Transition);
        Color hallFloodEmission = hallFloodColor * 2.5f;
        for (int i = 0; i < initialHallEmissions.Length; i++)
            SetLensEmission(hallLenses[i], Color.Lerp(initialHallEmissions[i], hallFloodEmission, Transition) * hallEmissionScale);
        if(rippleFrames!=null && rippleFrames.Length>0)
        {
            int frame=Mathf.FloorToInt(Time.time*8)%rippleFrames.Length;
            if(frame!=lastFrame)
            {
                lastFrame=frame;
                for(int i=0;i<rippleLights.Length;i++)if(rippleLights[i])rippleLights[i].cookie=rippleFrames[(frame+i*3)%rippleFrames.Length];
            }
        }
    }
    void ApplyGroup(Light[] lights,int g,Color end,float intensity)
    {
        for(int i=0;i<lights.Length;i++)if(lights[i]){lights[i].color=Color.Lerp(initialColors[g][i],end,Transition);lights[i].intensity=initialIntensities[g][i]*intensity;}
    }
    void Lenses(Renderer[] lenses,Color emission)
    {
        if(lenses==null)return;
        foreach (var lens in lenses) SetLensEmission(lens, emission);
    }
    void SetLensEmission(Renderer lens, Color emission)
    {
        if (!lens) return;
        lens.GetPropertyBlock(block);
        block.SetColor(EmissionColor, emission);
        lens.SetPropertyBlock(block);
    }
    void OnDisable()
    {
        if(initialColors==null)return;
        Light[][] groups={poolLamps,hallLamps,rippleLights,normalFillLights};
        for(int g=0;g<groups.Length;g++)for(int i=0;i<groups[g].Length;i++)if(groups[g][i]){groups[g][i].color=initialColors[g][i];groups[g][i].intensity=initialIntensities[g][i];}
        for(int i=0;i<rippleLights.Length;i++)if(rippleLights[i])rippleLights[i].cookie=initialCookies[i];
        foreach(var r in poolLenses)if(r)r.SetPropertyBlock(null);
        foreach(var r in hallLenses)if(r)r.SetPropertyBlock(null);
        Transition=0;Dimming=0;
        RenderSettings.ambientLight=initialAmbient;var ambient=new UnityEngine.Rendering.SphericalHarmonicsL2();ambient.AddAmbientLight(initialAmbient);RenderSettings.ambientProbe=ambient;
    }
}
