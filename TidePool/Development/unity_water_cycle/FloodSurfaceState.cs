using UnityEngine;

// Fade the pool-edge wetness pattern into a uniformly soaked deck as water reaches its top.
[DefaultExecutionOrder(900)]
public sealed class FloodSurfaceState : MonoBehaviour
{
    public PoolWaterCycle cycle;
    public Renderer[] deckRenderers;
    public float beginFullWetHeight=-.12f;
    public float fullWetHeight=.02f;
    public float FloodBlend {get;private set;}
    MaterialPropertyBlock block;
    static readonly int Wet=Shader.PropertyToID("_FloodWetness");
    void LateUpdate()
    {
        if(!cycle || deckRenderers==null)return;
        if(block==null)block=new MaterialPropertyBlock();
        FloodBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(beginFullWetHeight,fullWetHeight,cycle.WaterHeight));
        foreach(var renderer in deckRenderers)
        {
            if(!renderer)continue;
            renderer.GetPropertyBlock(block);block.SetFloat(Wet,FloodBlend);renderer.SetPropertyBlock(block);
        }
    }
    void OnDisable()
    {
        if(deckRenderers==null)return;
        if(block==null)block=new MaterialPropertyBlock();
        foreach(var renderer in deckRenderers)
        {
            if(!renderer)continue;
            renderer.GetPropertyBlock(block);block.SetFloat(Wet,0);renderer.SetPropertyBlock(block);
        }
        FloodBlend=0;
    }
}
