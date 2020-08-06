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
    const int maxVisiableLights = 4;
    static int visiableLightColorsId = Shader.PropertyToID("_VisibleLightColors");
    static int visiableLightDirectionsOrPositionsId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    static int visiableLightAttenuationsId = Shader.PropertyToID("_VisiableLightAttenuations");

    Vector4[] visiableLightColors = new Vector4[maxVisiableLights];
    Vector4[] visiableLightDirectionsOrPositions = new Vector4[maxVisiableLights];
    Vector4[] visiableLightAttenuations = new Vector4[maxVisiableLights];

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
        ConfigureLights();
        cameraBuffer.SetGlobalVectorArray(visiableLightColorsId, visiableLightColors);
        cameraBuffer.SetGlobalVectorArray(visiableLightDirectionsOrPositionsId, visiableLightDirectionsOrPositions);
        cameraBuffer.SetGlobalVectorArray(visiableLightAttenuationsId, visiableLightAttenuations);

        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear(); 

        var drawSetting = new DrawRendererSettings(
            camera,new ShaderPassName("SRPDefaultUnlit")
            ) ;
        drawSetting.flags = drawFlags;   //DrawRendererFlags.EnableDynamicBatching;  //动态合批
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
        int i = 0;
        for (; i < cull.visibleLights.Count; i++)
        {
            if (i == maxVisiableLights)   //最多光源数量，超过的忽略不再处理
                break;

            var light = cull.visibleLights[i];
            visiableLightColors[i] = light.finalColor;

            Vector4 attenuation = Vector4.zero;
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
            }
            visiableLightAttenuations[i] = attenuation;
        }
        //当光源数量改变时，清理不使用的光源信息
        for (; i < maxVisiableLights; i++)
        {
            visiableLightColors[i] = Color.clear;
            visiableLightDirectionsOrPositions[i] = Vector4.zero;
        }
    }
}
