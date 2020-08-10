﻿using UnityEngine;
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
    [SerializeField]
    float shadowDistance = 100f;
    public enum ShadowCascade {
        Zero=0,
        Two=2,
        Four=4
    };
    [SerializeField]
    ShadowCascade shadowCascades = ShadowCascade.Four;
    
    [SerializeField,HideInInspector]
    float twoCascadesSplit = 0.25f;

    [SerializeField,HideInInspector]
    Vector3 fourCascadesSplit = new Vector3(0.067f, 0.2f, 0.467f);
    protected override IRenderPipeline InternalCreatePipeline()
    {
        Vector3 shadowCascadeSplit = shadowCascades == ShadowCascade.Four ? fourCascadesSplit : new Vector3(twoCascadesSplit, 0f);
        return new XPipeline(dynamicBatching,instancing, (int)shadowMapSize,shadowDistance,(int)shadowCascades,shadowCascadeSplit);
    }
}
