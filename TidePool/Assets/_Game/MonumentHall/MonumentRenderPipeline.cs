using UnityEngine;
using UnityEngine.Rendering;

// Runtime-only quality override for the monumental hall. Leaving Play restores the project choice.
[DefaultExecutionOrder(-1000)]
public sealed class MonumentRenderPipeline : MonoBehaviour
{
    public RenderPipelineAsset pipeline;
    RenderPipelineAsset previous;
    bool applied;
    void OnEnable()
    {
        if(!Application.isPlaying || !pipeline)return;
        previous=QualitySettings.renderPipeline;
        QualitySettings.renderPipeline=pipeline;applied=true;
    }
    void OnDisable()
    {
        if(!applied)return;
        if(QualitySettings.renderPipeline==pipeline)QualitySettings.renderPipeline=previous;
        applied=false;
    }
}
