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
    static int visiableLightAttenuationsId = Shader.PropertyToID("_VisibleLightAttenuations");
    static int visiableLightSpotDirectionsId = Shader.PropertyToID("_VisibleLightSpotDirections");
    static int lightIndicesOffsetAndCountID = Shader.PropertyToID("unity_LightIndicesOffsetAndCount");
    static int shadowMapId = Shader.PropertyToID("_ShadowMap");
    //static int worldToShadowMatrixId = Shader.PropertyToID("_WorldToShadowMatrix");
    static int worldToShadowMatricesId = Shader.PropertyToID("_WorldToShadowMatrices");
    static int shadowBiasId = Shader.PropertyToID("_ShadowBias");
    //static int shadowStrengthId = Shader.PropertyToID("_ShadowStrength");
    static int shadowDataId = Shader.PropertyToID("_ShadowData");
    static int shadowMapSizeId = Shader.PropertyToID("_ShadowMapSize");
    static int globalShadowDataId = Shader.PropertyToID("_GlobalShadowData");
    static int cascadedShadowMapId = Shader.PropertyToID("_CascadedShadowMap");
    static int worldToShadowCascadeMatricesId = Shader.PropertyToID("_WorldToShadowCascadeMatrices");
    static int cascadedShadowMapSizeId = Shader.PropertyToID("_CascadedShadowMapSize");
    static int cascadedShadowStrengthId = Shader.PropertyToID("_CascadedShadowStrength");
    static int cascadedCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");

    const string shadowsSoftKeyWord = "_SHADOWS_SOFT";
    const string shadowsHardKeyWord = "_SHADOWS_HARD";
    const string cascadedShadowsHardKeyword = "_CASCADED_SHADOWS_HARD";
    const string cascadedShadowsSoftKeyword = "_CASCADED_SHADOWS_SOFT";


    Vector4[] visiableLightColors = new Vector4[maxVisiableLights];
    Vector4[] visiableLightDirectionsOrPositions = new Vector4[maxVisiableLights];
    Vector4[] visiableLightAttenuations = new Vector4[maxVisiableLights];
    Vector4[] visiableLightSpotDirections = new Vector4[maxVisiableLights];
    Vector4[] shadowData = new Vector4[maxVisiableLights];  //存储阴影数据
    Matrix4x4[] worldToShadowMatrices = new Matrix4x4[maxVisiableLights];
    Matrix4x4[] worldToShadowCascadeMatrices = new Matrix4x4[5];
    Vector4[] cascadedCullingSpheres = new Vector4[4];

    //SpotLight Shadows
    RenderTexture shadowMap,cascadedShadowMap;
    int shadowMapSize;
    int shadowTileCount;

    //Direction Shadows
    float shadowDistance;
    int shadowCascades;
    Vector3 shadowCascadeSplit;

    bool mainLightExists;
    public  XPipeline(bool dynamicBatching,bool instancing,
        int shadowMapSize,float shadowDistance,
        int shadowCascades,Vector3 shadowCascadeSplit)
    {
        GraphicsSettings.lightsUseLinearIntensity = true;
        if (SystemInfo.usesReversedZBuffer)
        {
            worldToShadowCascadeMatrices[4].m33 = 1f;
        }
        if (dynamicBatching)
        {
            drawFlags = DrawRendererFlags.EnableDynamicBatching;
        }

        if (instancing)
        {
            drawFlags |= DrawRendererFlags.EnableInstancing;
        }
        this.shadowMapSize = shadowMapSize;
        this.shadowDistance = shadowDistance;
        this.shadowCascades = shadowCascades;
        this.shadowCascadeSplit = shadowCascadeSplit;
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
        cullingParameters.shadowDistance = Mathf.Min(shadowDistance, camera.farClipPlane);

#if UNITY_EDITOR
        //SceneView 处理UI
        if(camera.cameraType== CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
        CullResults.Cull(ref cullingParameters, context,ref cull);

        //光照信息
        if (cull.visibleLights.Count > 0)
        {
            ConfigureLights();
            if (mainLightExists)
            {
                RenderCascadedShadows(context);
            }
            else {
                cameraBuffer.DisableShaderKeyword(cascadedShadowsHardKeyword);
                cameraBuffer.DisableShaderKeyword(cascadedShadowsSoftKeyword);
            }
            if (shadowTileCount > 0)
                RenderShadows(context);
            else {
                cameraBuffer.DisableShaderKeyword(shadowsHardKeyWord);
                cameraBuffer.DisableShaderKeyword(shadowsSoftKeyWord);
            }
        }
        else
        {
            cameraBuffer.SetGlobalVector(lightIndicesOffsetAndCountID, Vector4.zero);
            cameraBuffer.DisableShaderKeyword(cascadedShadowsHardKeyword);
            cameraBuffer.DisableShaderKeyword(cascadedShadowsSoftKeyword);
            cameraBuffer.DisableShaderKeyword(shadowsHardKeyWord);
            cameraBuffer.DisableShaderKeyword(shadowsSoftKeyWord);
        }

        //注意Scene窗口如果没有启用光源需要注意
        //  if (camera.cameraType == CameraType.Game)   //SceneView 光源个数有点问题
        {
            //阴影贴图是在常规场景之前渲染的，所以在渲染之前调用渲染阴影，但是在剔除之后
                
        }
        
        context.SetupCameraProperties(camera);  //设置视图投影矩阵

        CameraClearFlags clearFlags = camera.clearFlags;
        cameraBuffer.ClearRenderTarget(
            (clearFlags&CameraClearFlags.Depth) !=0, 
            (clearFlags&CameraClearFlags.Color) !=0,
            camera.backgroundColor);

        cameraBuffer.BeginSample("Render Camera");
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

        cameraBuffer.EndSample("Render Camera");
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        context.Submit();

        //提交上下文之后，释放渲染纹理
        if (shadowMap) {
            RenderTexture.ReleaseTemporary(shadowMap);
            shadowMap = null;
        }

        if (cascadedShadowMap)
        {
            RenderTexture.ReleaseTemporary(cascadedShadowMap);
            cascadedShadowMap = null;
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
        mainLightExists = false;
        shadowTileCount = 0;
        for (int i = 0; i < cull.visibleLights.Count; i++)
        {
            if (i == maxVisiableLights)   //最多光源数量，超过的忽略不再处理
                break;

            var light = cull.visibleLights[i];
            visiableLightColors[i] = light.finalColor;
            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1f;  //不影响其他光源类型
            Vector4 shadow = Vector4.zero;

            if (light.lightType == LightType.Directional)
            {
                Vector4 v = light.localToWorld.GetColumn(2);
                v.x = -v.x;
                v.y = -v.y;
                v.z = -v.z;
                visiableLightDirectionsOrPositions[i] = v;
                shadow = ConfigureShadows(i, light.light);
                shadow.z = 1f;
                if (i == 0 && shadow.x > 0f && shadowCascades > 0)
                {
                    mainLightExists = true;
                    shadowTileCount -= 1;
                }
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

                    shadow = ConfigureShadows(i, light.light);
                }
            }
            visiableLightAttenuations[i] = attenuation;
            shadowData[i] = shadow;
        }

        //超过最大光源个数限制时，设置为-1的灯(不存在的灯)
        if ( mainLightExists || cull.visibleLights.Count > maxVisiableLights)
        {
            int[] lightIndices = cull.GetLightIndexMap();
            if (mainLightExists)
            {
                lightIndices[0] = -1;
            }
            for (int i = maxVisiableLights; i < cull.visibleLights.Count; i++)
            {
                lightIndices[i] = -1;
            }
            cull.SetLightIndexMap(lightIndices);
        }
    }

    private Vector4 ConfigureShadows(int lightIndex, Light shadowLight)
    {
        Vector4 shadow = Vector4.zero;
        Bounds shadowBounds;
        if (shadowLight.shadows != LightShadows.None &&
            cull.GetShadowCasterBounds(lightIndex, out shadowBounds))
        {
            shadowTileCount += 1;
            shadow.x = shadowLight.shadowStrength;
            shadow.y = shadowLight.shadows == LightShadows.Soft ? 1.0f : 0f;
        }

        return shadow;
    }
    void RenderCascadedShadows(ScriptableRenderContext context)
    {
        float tileSize = shadowMapSize / 2;
        cascadedShadowMap = SetShadowRenderTarget();
        shadowBuffer.BeginSample("Render Shadows");
        shadowBuffer.SetGlobalVector(
            globalShadowDataId, new Vector4(0f, shadowDistance * shadowDistance)
        );
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
        Light shadowLight = cull.visibleLights[0].light;
        shadowBuffer.SetGlobalFloat(
            shadowBiasId, shadowLight.shadowBias
        );
        var shadowSettings = new DrawShadowsSettings(cull, 0);
        var tileMatrix = Matrix4x4.identity;
        tileMatrix.m00 = tileMatrix.m11 = 0.5f;

        for (int i = 0; i < shadowCascades; i++)
        {
            Matrix4x4 viewMatrix, projectionMatrix;
            ShadowSplitData splitData;
            cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                0, i, shadowCascades, shadowCascadeSplit, (int)tileSize,
                shadowLight.shadowNearPlane,
                out viewMatrix, out projectionMatrix, out splitData
            );

            Vector2 tileOffset = ConfigureShadowTile(i, 2, tileSize);
            shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            context.ExecuteCommandBuffer(shadowBuffer);
            shadowBuffer.Clear();

            cascadedCullingSpheres[i] =
                shadowSettings.splitData.cullingSphere = splitData.cullingSphere;
            cascadedCullingSpheres[i].w *= splitData.cullingSphere.w;
            context.DrawShadows(ref shadowSettings);
            CalculateWorldToShadowMatrix(
                ref viewMatrix, ref projectionMatrix,
                out worldToShadowCascadeMatrices[i]
            );
            tileMatrix.m03 = tileOffset.x * 0.5f;
            tileMatrix.m13 = tileOffset.y * 0.5f;
            worldToShadowCascadeMatrices[i] =
                tileMatrix * worldToShadowCascadeMatrices[i];
        }

        shadowBuffer.DisableScissorRect();
        shadowBuffer.SetGlobalTexture(cascadedShadowMapId, cascadedShadowMap);
        shadowBuffer.SetGlobalVectorArray(
            cascadedCullingSpheresId, cascadedCullingSpheres
        );
        shadowBuffer.SetGlobalMatrixArray(
            worldToShadowCascadeMatricesId, worldToShadowCascadeMatrices
        );
        float invShadowMapSize = 1f / shadowMapSize;
        shadowBuffer.SetGlobalVector(
            cascadedShadowMapSizeId, new Vector4(
                invShadowMapSize, invShadowMapSize, shadowMapSize, shadowMapSize
            )
        );
        shadowBuffer.SetGlobalFloat(
            cascadedShadowStrengthId, shadowLight.shadowStrength
        );
        bool hard = shadowLight.shadows == LightShadows.Hard;
        CoreUtils.SetKeyword(shadowBuffer, cascadedShadowsHardKeyword, hard);
        CoreUtils.SetKeyword(shadowBuffer, cascadedShadowsSoftKeyword, !hard);
        shadowBuffer.EndSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
    }
    void RenderShadows(ScriptableRenderContext context)
    {
        int spilt;
        if (shadowTileCount <= 1)
        {
            spilt = 1;
        }
        else if (shadowTileCount <= 4)
        {
            spilt = 2;
        }
        else if (shadowTileCount <= 9)
        {
            spilt = 3;
        }
        else
        {
            spilt = 4;
        }

        float tileSize = shadowMapSize / spilt;
        float tileScale = 1f / spilt;
        shadowMap = SetShadowRenderTarget();

        shadowBuffer.BeginSample("Render Shadows");
        shadowBuffer.SetGlobalVector(globalShadowDataId, new Vector4(tileScale, shadowDistance * shadowDistance));
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();

        int tileIndex = 0;
        bool hardShadows = false;
        bool softShadows = false;
        for (int i = mainLightExists ? 1 : 0; i < cull.visibleLights.Count; i++)
        {
            if (i == maxVisiableLights)
            {
                break;
            }
            if (shadowData[i].x <= 0f)
            {
                continue;
            }

            Matrix4x4 viewMatrix, projectionMatrix;
            ShadowSplitData splitData;
            bool validShadows;
            if (shadowData[i].z > 0f)
            {
                validShadows = cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                    i, 0, 1, Vector3.right, (int)tileSize, cull.visibleLights[i].light.shadowNearPlane,
                    out viewMatrix, out projectionMatrix, out splitData);
            }
            else
            {
                validShadows = cull.ComputeSpotShadowMatricesAndCullingPrimitives(
                                i, out viewMatrix, out projectionMatrix, out splitData
                             );
            }

            if (!validShadows)
            {
                shadowData[i].x = 0f;
                continue;
            }
            Vector2 tileOffset = ConfigureShadowTile(tileIndex, spilt, tileSize);
            shadowData[i].z = tileOffset.x * tileScale;
            shadowData[i].w = tileOffset.y * tileScale;

            shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            shadowBuffer.SetGlobalFloat(shadowBiasId, cull.visibleLights[i].light.shadowBias);
            context.ExecuteCommandBuffer(shadowBuffer);
            shadowBuffer.Clear();

            var shadowSetting = new DrawShadowsSettings(cull, i);
            shadowSetting.splitData.cullingSphere = splitData.cullingSphere;
            context.DrawShadows(ref shadowSetting);

            CalculateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix, out worldToShadowMatrices[i]);

            tileIndex += 1;

            if (shadowData[i].y <= 0f)
            {
                hardShadows = true;
            }
            else
            {
                softShadows = true;
            }
        }

        shadowBuffer.DisableScissorRect();   //恢复正常渲染区域，防止正常渲染收到影响

        shadowBuffer.SetGlobalTexture(shadowMapId, shadowMap);
        //shadowBuffer.SetGlobalFloat(shadowStrengthId, cull.visibleLights[0].light.shadowStrength);
        shadowBuffer.SetGlobalMatrixArray(worldToShadowMatricesId, worldToShadowMatrices);
        shadowBuffer.SetGlobalVectorArray(shadowDataId, shadowData);

        float invShadowMapSize = 1f / shadowMapSize;
        shadowBuffer.SetGlobalVector(shadowMapSizeId,
            new Vector4(invShadowMapSize, invShadowMapSize, shadowMapSize, shadowMapSize)
            );

        CoreUtils.SetKeyword(shadowBuffer, shadowsHardKeyWord, hardShadows);
        CoreUtils.SetKeyword(shadowBuffer, shadowsSoftKeyWord, softShadows);

        shadowBuffer.EndSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
    }

    private Vector2 ConfigureShadowTile(int tileIndex, int spilt, float tileSize)
    {
        Vector2 tileOffset;
        tileOffset.x = tileIndex % spilt;
        tileOffset.y = tileIndex / spilt;
        var tileViewport = new Rect(tileOffset.x * tileSize, tileOffset.y * tileSize, tileSize, tileSize);
        shadowBuffer.SetViewport(tileViewport);
        shadowBuffer.EnableScissorRect(
            new Rect(tileViewport.x + 4f, tileViewport.y + 4f,
            tileSize - 8f, tileSize - 8f
            ));  //创建一个小点的区域
        

        return tileOffset;
    }

    private void CalculateWorldToShadowMatrix(ref Matrix4x4 viewMatrix,ref Matrix4x4 projectionMatrix,
        out Matrix4x4 worldToShadowMatrix)
    {
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

        //Matrix4x4 worldToShadowMatrix = scaleOffset * (projectionMatrix * viewMatrix);
        //shadowBuffer.SetGlobalMatrix(worldToShadowMatrixId, worldToShadowMatrix);
        worldToShadowMatrix = scaleOffset * (projectionMatrix * viewMatrix);
    }

    private RenderTexture SetShadowRenderTarget()
    {
        var  texture = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        CoreUtils.SetRenderTarget(shadowBuffer, texture,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            ClearFlag.Depth);
        return texture;
    }
}
