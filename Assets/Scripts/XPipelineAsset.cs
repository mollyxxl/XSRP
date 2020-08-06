using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(menuName ="Rendering/X Pipeline")]
public class XPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    bool dynamicBatching = true;
    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new XPipeline(dynamicBatching);
    }
}
