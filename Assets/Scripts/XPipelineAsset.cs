using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(menuName ="Rendering/X Pipeline")]
public class XPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    bool dynamicBatching = true;
    [SerializeField]
    bool instancing=false;

    public enum ShadowMapSize { 
        _256=256,
        _512=512,
        _1024=1024,
        _2048=2048,
        _4096=4096
    }
    [SerializeField]
    ShadowMapSize shadowMapSize= ShadowMapSize._1024;
    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new XPipeline(dynamicBatching,instancing, (int)shadowMapSize);
    }
}
