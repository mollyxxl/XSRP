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
    XPostProcessingStack defaultStack;

    [SerializeField]
    Texture2D ditherTexture = null;
    [SerializeField,Range(0,120f)]
    float ditherAnimationSpeed = 30f;

    [SerializeField]
    bool supportLODCrossFading = true;

    public bool HasLODCrossFading {
        get {
            return supportLODCrossFading;
        }
    }

    [SerializeField]
    ShadowMapSize shadowMapSize= ShadowMapSize._1024;

    [SerializeField]
    float shadowDistance = 100f;
    [SerializeField,Range(0.01f,2f)]
    float shadowFadeRange = 1f;
    public enum ShadowCascade {
        Zero=0,
        Two=2,
        Four=4
    };

    public bool HasShadowCascade {
        get {
            return shadowCascades != ShadowCascade.Zero;
        }
    }

    [SerializeField]
    ShadowCascade shadowCascades = ShadowCascade.Four;
    
    [SerializeField,HideInInspector]
    float twoCascadesSplit = 0.25f;

    [SerializeField,HideInInspector]
    Vector3 fourCascadesSplit = new Vector3(0.067f, 0.2f, 0.467f);


    protected override IRenderPipeline InternalCreatePipeline()
    {
        Vector3 shadowCascadeSplit = shadowCascades == ShadowCascade.Four ? fourCascadesSplit : new Vector3(twoCascadesSplit, 0f);
        return new XPipeline(dynamicBatching,instancing, defaultStack,
                ditherTexture,ditherAnimationSpeed,
                (int)shadowMapSize,shadowDistance,shadowFadeRange,
                (int)shadowCascades,shadowCascadeSplit
            );
    }
}
