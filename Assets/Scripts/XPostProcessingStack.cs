using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName ="Rendering/My Post-Processing Stack")]
public class XPostProcessingStack : ScriptableObject
{
    public void Render(CommandBuffer cb,int cameraColorId,int cameraDepthId)
    {
        //Debug.Log("Rendering Post-Processing Stack");
        cb.Blit(cameraColorId, BuiltinRenderTextureType.CameraTarget);
        //cb.Blit(cameraDepthId, BuiltinRenderTextureType.CameraTarget);
    }
}
