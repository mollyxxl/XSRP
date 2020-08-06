using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(menuName ="Rendering/X Pipeline")]
public class XPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    bool dynamicBatching = true;
    [SerializeField]
    bool instancing;
    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new XPipeline(dynamicBatching,instancing);
    }
}
