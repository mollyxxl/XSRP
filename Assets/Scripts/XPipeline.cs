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

    Material errorMaterial;
    DrawRendererFlags drawFlags;

    //Light
    const int maxVisiableLights = 16;
    static int visiableLightColorsId = Shader.PropertyToID("_VisibleLightColors");
    static int visiableLightDirectionsOrPositionsId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    static int visiableLightAttenuationsId = Shader.PropertyToID("_VisiableLightAttenuations");
    static int visiableLightSpotDirectionsId = Shader.PropertyToID("_VisiableLightSpotDirections");
    static int lightIndicesOffsetAndCountID = Shader.PropertyToID("unity_LightIndicesOffsetAndCount");

    Vector4[] visiableLightColors = new Vector4[maxVisiableLights];
    Vector4[] visiableLightDirectionsOrPositions = new Vector4[maxVisiableLights];
    Vector4[] visiableLightAttenuations = new Vector4[maxVisiableLights];
    Vector4[] visiableLightSpotDirections = new Vector4[maxVisiableLights];

    public  XPipeline(bool dynamicBatching,bool instancing)
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
}
