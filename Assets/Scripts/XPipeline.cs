using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;
public class XPipeline : RenderPipeline
{
    CullResults cull;
    CommandBuffer cameraBuffer = new CommandBuffer()
    {
        name = "Render Camera"
    };
    CommandBuffer shadowBuffer = new CommandBuffer() { 
        name = "Render Shadows"
    };

    Material errorMaterial;
    DrawRendererFlags drawFlags;

    //Light
    const int maxVisiableLights = 16;
    static int visiableLightColorsId = Shader.PropertyToID("_VisibleLightColors");
    static int visiableLightDirectionsOrPositionsId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    static int visiableLightAttenuationsId = Shader.PropertyToID("_VisiableLightAttenuations");
    static int visiableLightSpotDirectionsId = Shader.PropertyToID("_VisiableLightSpotDirections");
    static int lightIndicesOffsetAndCountID = Shader.PropertyToID("unity_LightIndicesOffsetAndCount");
    static int shadowMapId = Shader.PropertyToID("_ShadowMap");
    static int worldToShadowMatrixId = Shader.PropertyToID("_WorldToShadowMatrix");
    static int shadowBiasId = Shader.PropertyToID("_ShadowBias");
    static int shadowStrengthId = Shader.PropertyToID("_ShadowStrength");

    Vector4[] visiableLightColors = new Vector4[maxVisiableLights];
    Vector4[] visiableLightDirectionsOrPositions = new Vector4[maxVisiableLights];
    Vector4[] visiableLightAttenuations = new Vector4[maxVisiableLights];
    Vector4[] visiableLightSpotDirections = new Vector4[maxVisiableLights];

    //SpotLight Shadows
    RenderTexture shadowMap;
    int shadowMapSize;
    public  XPipeline(bool dynamicBatching,bool instancing,int shadowMapSize)
    {
        GraphicsSettings.lightsUseLinearIntensity = true;
         
        if (dynamicBatching)
        {
            drawFlags = DrawRendererFlags.EnableDynamicBatching;
        }

        if (instancing)
        {
            drawFlags |= DrawRendererFlags.EnableInstancing;
        }
        this.shadowMapSize = shadowMapSize;
    }
    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        base.Render(renderContext, cameras);

        foreach (var camera in cameras)
        {
            Render(renderContext,camera);
        }
    }
    void Render(ScriptableRenderContext context, Camera camera)
    {
        ScriptableCullingParameters cullingParameters;
        if (!CullResults.GetCullingParameters(camera, out cullingParameters))
        {
            return;
        }
#if UNITY_EDITOR
        //SceneView 处理UI
        if(camera.cameraType== CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
        CullResults.Cull(ref cullingParameters, context,ref cull);

        //注意Scene窗口如果没有启用光源需要注意
      //  if (camera.cameraType == CameraType.Game)   //SceneView 光源个数有点问题
        {
            //阴影贴图是在常规场景之前渲染的，所以在渲染之前调用渲染阴影，但是在剔除之后
            if (cull.visibleLights.Count > 0)
                RenderShadows(context);
        }
        
        context.SetupCameraProperties(camera);  //设置视图投影矩阵

        CameraClearFlags clearFlags = camera.clearFlags;
        cameraBuffer.ClearRenderTarget(
            (clearFlags&CameraClearFlags.Depth) !=0, 
            (clearFlags&CameraClearFlags.Color) !=0,
            camera.backgroundColor);
        //光照信息
        if (cull.visibleLights.Count > 0)
        {
            ConfigureLights();
        }
        else
        {
            cameraBuffer.SetGlobalVector(lightIndicesOffsetAndCountID, Vector4.zero);
        }
        cameraBuffer.SetGlobalVectorArray(visiableLightColorsId, visiableLightColors);
        cameraBuffer.SetGlobalVectorArray(visiableLightDirectionsOrPositionsId, visiableLightDirectionsOrPositions);
        cameraBuffer.SetGlobalVectorArray(visiableLightAttenuationsId, visiableLightAttenuations);
        cameraBuffer.SetGlobalVectorArray(visiableLightSpotDirectionsId, visiableLightSpotDirections);

        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        var drawSetting = new DrawRendererSettings(
            camera, new ShaderPassName("SRPDefaultUnlit")
            )
        {
            flags = drawFlags,
           // rendererConfiguration = RendererConfiguration.PerObjectLightIndices8
        } ;

        //没有光源时，防止Light Indices崩溃
        if (cull.visibleLights.Count > 0)
        {
            drawSetting.rendererConfiguration = RendererConfiguration.PerObjectLightIndices8;
        }

        //drawSetting.flags = drawFlags;   //DrawRendererFlags.EnableDynamicBatching;  //动态合批
        drawSetting.sorting.flags = SortFlags.CommonOpaque;
        var filterSettings = new FilterRenderersSettings(true)
        {
            renderQueueRange = RenderQueueRange.opaque
        };
        context.DrawRenderers(cull.visibleRenderers, ref drawSetting, filterSettings);
        context.DrawSkybox(camera);

        drawSetting.sorting.flags = SortFlags.CommonTransparent;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cull.visibleRenderers, ref drawSetting, filterSettings);

        DrawDefaultPipeline(context, camera);
        
        context.Submit();

        //提交上下文之后，释放渲染纹理
        if (shadowMap) {
            RenderTexture.ReleaseTemporary(shadowMap);
            shadowMap = null;
        }

    }
    [Conditional("DEVELOPMENT_BUILD"),Conditional("UNITY_EDITOR")]
     void DrawDefaultPipeline(ScriptableRenderContext context, Camera camera)
    {
        if (errorMaterial == null)
        {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            errorMaterial = new Material(errorShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("ForwardBase"));
        drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));
        drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));
        drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));
        drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGB"));
        drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));
        drawSettings.SetOverrideMaterial(errorMaterial, 0);

        var filterSettings = new FilterRenderersSettings(true);
        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
    }
    /// <summary>
    /// 初始化光照相关信息
    /// </summary>
    void ConfigureLights()
    {
        for (int i = 0; i < cull.visibleLights.Count; i++)
        {
            if (i == maxVisiableLights)   //最多光源数量，超过的忽略不再处理
                break;

            var light = cull.visibleLights[i];
            visiableLightColors[i] = light.finalColor;

            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1f;  //不影响其他光源类型
            if (light.lightType == LightType.Directional)
            {
                Vector4 v = light.localToWorld.GetColumn(2);
                v.x = -v.x;
                v.y = -v.y;
                v.z = -v.z;
                visiableLightDirectionsOrPositions[i] = v;
            }
            else
            {
                //光源的位置
                visiableLightDirectionsOrPositions[i] = light.localToWorld.GetColumn(3);
                attenuation.x = 1f / Mathf.Max(light.range * light.range, 0.00001f);

                if (light.lightType == LightType.Spot)
                {
                    Vector4 v = light.localToWorld.GetColumn(2);
                    v.x = -v.x;
                    v.y = -v.y;
                    v.z = -v.z;
                    visiableLightSpotDirections[i] = v;

                    float outerRad = Mathf.Deg2Rad * 0.5f * light.spotAngle;
                    float outerCos = Mathf.Cos(outerRad);
                    float outerTan = Mathf.Tan(outerRad);
                    float innerCos = Mathf.Cos(Mathf.Atan((46f / 64f) * outerTan));
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.0001f);
                    attenuation.z = 1f / angleRange;
                    attenuation.w = -outerCos * attenuation.z;
                }
            }
            visiableLightAttenuations[i] = attenuation;
        }

        //超过最大光源个数限制时，设置为-1的灯(不存在的灯)
        if (cull.visibleLights.Count > maxVisiableLights)
        {
            int[] lightIndices = cull.GetLightIndexMap();
            for (int i = maxVisiableLights; i < cull.visibleLights.Count; i++)
            {
                lightIndices[i] = -1;
            }
            cull.SetLightIndexMap(lightIndices);
        }
    }
    void RenderShadows(ScriptableRenderContext context)
    {
        shadowMap = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        shadowMap.filterMode = FilterMode.Bilinear;
        shadowMap.wrapMode = TextureWrapMode.Clamp;

        CoreUtils.SetRenderTarget(shadowBuffer, shadowMap,
            RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store,
            ClearFlag.Depth);

        shadowBuffer.BeginSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();

        Matrix4x4 viewMatrix, projectionMatrix;
        ShadowSplitData splitData;
        cull.ComputeSpotShadowMatricesAndCullingPrimitives(
           0, out viewMatrix, out projectionMatrix, out splitData
            );
        shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        shadowBuffer.SetGlobalFloat(shadowBiasId, cull.visibleLights[0].light.shadowBias);
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();

        var shadowSetting = new DrawShadowsSettings(cull, cull.visibleLights.Count-1);
         context.DrawShadows(ref shadowSetting);

        if (SystemInfo.usesReversedZBuffer)
        {
            projectionMatrix.m20 = -projectionMatrix.m20;
            projectionMatrix.m21 = -projectionMatrix.m21;
            projectionMatrix.m22 = -projectionMatrix.m22;
            projectionMatrix.m23 = -projectionMatrix.m23;
        }
        var scaleOffset = Matrix4x4.identity;
        scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
        scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;

        Matrix4x4 worldToShadowMatrix =scaleOffset*(projectionMatrix * viewMatrix);
        shadowBuffer.SetGlobalMatrix(worldToShadowMatrixId, worldToShadowMatrix);
        shadowBuffer.SetGlobalTexture(shadowMapId, shadowMap);
        shadowBuffer.SetGlobalFloat(shadowStrengthId, cull.visibleLights[0].light.shadowStrength);

        shadowBuffer.EndSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
    }

}
