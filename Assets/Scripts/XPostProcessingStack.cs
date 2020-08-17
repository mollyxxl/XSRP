using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName ="Rendering/My Post-Processing Stack")]
public class XPostProcessingStack : ScriptableObject
{
    static Mesh fullScreenTriangle;
    static Material material;
    static int mainTexId = Shader.PropertyToID("_MainTex");
    static int tempTexId = Shader.PropertyToID("_XPostProcessingStackTempTex");
    enum Pass { Copy,Blur};

    [SerializeField,Range(0,10)]
    int blurStrength;
    static void InitializeStatic() {
        if (fullScreenTriangle)
        {
            return;
        }
        fullScreenTriangle = new Mesh() { 
            name="X Post-Processing Stack Full-Screen Triangle",
             vertices=new Vector3[] { 
                new Vector3(-1f,-1f,0),
                new Vector3(-1f,3f,0),
                new Vector3(3f,-1f,0)
             },
            triangles=new int[] { 0,1,2},
        };
        fullScreenTriangle.UploadMeshData(true);

        material = new Material(Shader.Find("Hidden/XPipeline/PostEffectStack")) { 
            name="X Post-Processing Stack material",
            hideFlags= HideFlags.HideAndDontSave
        };
    }
    public void Render(CommandBuffer cb,int cameraColorId,int cameraDepthId,
        int width,int height
        )
    {
        InitializeStatic();
        if (blurStrength > 0)
        {
            Blur(cb, cameraColorId, width, height);
        }
        else
        {
            Blit(cb, cameraColorId, BuiltinRenderTextureType.CameraTarget);
        }
        
    }

    void Blur(CommandBuffer cb, int cameraColorId, int width, int height)
    {
        cb.BeginSample("Blur");
        if (blurStrength == 1)
        {
            Blit(
                cb, cameraColorId, BuiltinRenderTextureType.CameraTarget, Pass.Blur
            );
            cb.EndSample("Blur");
            return;
        }

        cb.GetTemporaryRT(tempTexId, width, height, 0, FilterMode.Bilinear);
        int passesLeft;
        for (passesLeft = blurStrength; passesLeft > 2; passesLeft -= 2)
        {
            Blit(cb, cameraColorId, tempTexId, Pass.Blur);
            Blit(cb, tempTexId, cameraColorId, Pass.Blur);
        }
        if (passesLeft > 1)
        {
            Blit(cb, cameraColorId, tempTexId, Pass.Blur);
            Blit(cb, tempTexId, BuiltinRenderTextureType.CameraTarget, Pass.Blur);
        }
        else
        {
            Blit(cb, cameraColorId, BuiltinRenderTextureType.CameraTarget,Pass.Blur);
        }
        
        cb.ReleaseTemporaryRT(tempTexId);
        cb.EndSample("Blur");
    }

    void Blit(
        CommandBuffer cb, 
        RenderTargetIdentifier sourceId,RenderTargetIdentifier destinationId,
        Pass pass=Pass.Copy
        )
    {
        cb.SetGlobalTexture(mainTexId, sourceId);
        //Debug.Log("Rendering Post-Processing Stack");
        //cb.Blit(cameraColorId, BuiltinRenderTextureType.CameraTarget);
        //cb.Blit(cameraDepthId, BuiltinRenderTextureType.CameraTarget);
        cb.SetRenderTarget(
            destinationId,
            RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store
            );
        cb.DrawMesh(fullScreenTriangle, Matrix4x4.identity, material, 0, (int)pass);
    }
}
