using UnityEngine;
using AbstractOcclusion.WebGpuWater;

// Authored roof positions; no external weather asset. Sparse drips develop into threads of water.
public sealed class CeilingSeepage : MonoBehaviour
{
    public PoolWaterCycle cycle;
    public WaterVolume water;
    public Vector3[] ceilingPoints;
    public Material dropletsMaterial;
    public Material streamMaterial;
    [Min(100)] public int maximumParticles=6000;
    public float fullDripsPerSecond=1000;
    public float streamWidth=.035f;
    public float Intensity {get;private set;}
    public int LiveParticles => particles ? particles.particleCount : 0;
    public int VisibleStreams {get;private set;}
    ParticleSystem particles;
    ParticleSystem.Particle[] buffer;
    LineRenderer[] streams;
    float dripBudget,rippleBudget;
    void Awake()
    {
        var go=new GameObject("Roof water droplets");go.transform.SetParent(transform,false);
        particles=go.AddComponent<ParticleSystem>();
        var main=particles.main;main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.maxParticles=maximumParticles;main.startLifetime=6;main.startSize=new ParticleSystem.MinMaxCurve(.018f,.038f);
        main.startSpeed=0;main.gravityModifier=1;main.startColor=new Color(.66f,.85f,.93f,.6f);
        main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        var emission=particles.emission;emission.enabled=false;
        var shape=particles.shape;shape.enabled=false;
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=dropletsMaterial;
        renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.velocityScale=.014f;renderer.lengthScale=3;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        particles.Play();buffer=new ParticleSystem.Particle[maximumParticles];
        streams=new LineRenderer[Mathf.Min(28,ceilingPoints==null?0:ceilingPoints.Length)];
        for(int i=0;i<streams.Length;i++)
        {
            var thread=new GameObject("Roof seep thread "+i);thread.transform.SetParent(transform,false);
            var line=thread.AddComponent<LineRenderer>();line.sharedMaterial=streamMaterial;
            line.useWorldSpace=true;line.positionCount=12;line.widthMultiplier=streamWidth;
            line.startColor=new Color(.65f,.85f,.94f,.38f);line.endColor=new Color(.65f,.85f,.94f,.23f);
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
            line.enabled=false;streams[i]=line;
        }
    }
    bool IsDryColumn(Vector3 p) => Mathf.Abs(p.x-8)<2.08f && Mathf.Abs(p.z-39.5f)<2.08f;
    bool IsPool(Vector3 p) => Mathf.Abs(p.x)<11 && p.z>1 && p.z<31;
    float LandingHeight(Vector3 p) => Mathf.Max(cycle.WaterHeight,IsPool(p)?-3:0);
    void Update()
    {
        if(!cycle || ceilingPoints==null || ceilingPoints.Length==0)return;
        bool flowing=cycle.CurrentPhase==PoolWaterCycle.Phase.Filling || cycle.CurrentPhase==PoolWaterCycle.Phase.Flooded;
        Intensity=flowing ? Mathf.Lerp(.06f,1,Mathf.SmoothStep(0,1,cycle.Elapsed/25f)) : 0;
        if(!flowing){ResetSeepage();return;}
        dripBudget+=Mathf.Lerp(70,fullDripsPerSecond,Intensity)*Time.deltaTime;
        int count=Mathf.Min(150,Mathf.FloorToInt(dripBudget));dripBudget-=count;
        for(int i=0;i<count;i++)
        {
            Vector3 p=ceilingPoints[Random.Range(0,ceilingPoints.Length)]+new Vector3(Random.Range(-1.5f,1.5f),0,Random.Range(-1.5f,1.5f));
            if(IsDryColumn(p)||p.y<cycle.WaterHeight+.1f)continue;
            var ep=new ParticleSystem.EmitParams {position=p,velocity=new Vector3(Random.Range(-.03f,.03f),-Random.Range(.35f,.8f),Random.Range(-.03f,.03f))};
            particles.Emit(ep,1);
        }
        int alive=particles.GetParticles(buffer);
        for(int i=0;i<alive;i++) if(IsDryColumn(buffer[i].position)||buffer[i].position.y<=LandingHeight(buffer[i].position)) buffer[i].remainingLifetime=0;
        particles.SetParticles(buffer,alive);
        VisibleStreams=0;
        for(int i=0;i<streams.Length;i++)
        {
            Vector3 top=ceilingPoints[(i*17+11)%ceilingPoints.Length];float bottom=LandingHeight(top);
            bool visible=Intensity>.18f+(i/(float)streams.Length)*.7f && !IsDryColumn(top) && top.y>bottom+.1f;
            var line=streams[i];line.enabled=visible;if(!visible)continue;VisibleStreams++;
            line.widthMultiplier=streamWidth*Mathf.Lerp(.25f,1.2f,Intensity);
            for(int j=0;j<line.positionCount;j++)
            {
                float t=j/(float)(line.positionCount-1);
                Vector3 p=Vector3.Lerp(top,new Vector3(top.x,bottom,top.z),t);
                p.x+=Mathf.Sin(t*21+Time.time*2+i)*.018f*t;p.z+=Mathf.Sin(t*15-Time.time*3+i)*.014f*t;
                line.SetPosition(j,p);
            }
        }
        rippleBudget+=Mathf.Lerp(1,16,Intensity)*Time.deltaTime;
        if(rippleBudget>=1)
        {
            rippleBudget=0;
            Vector3 p=ceilingPoints[Random.Range(0,ceilingPoints.Length)];
            if(!IsDryColumn(p) && (cycle.WaterHeight>0 || IsPool(p)))water.AddRipple(p.x,p.z,.09f,.008f);
        }
    }
    public void ResetSeepage()
    {
        Intensity=0;VisibleStreams=0;dripBudget=0;rippleBudget=0;
        if(particles && particles.particleCount>0)particles.Clear();
        if(streams!=null)foreach(var l in streams)if(l)l.enabled=false;
    }
}
