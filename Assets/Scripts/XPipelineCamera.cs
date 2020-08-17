using UnityEngine;
[ImageEffectAllowedInSceneView,RequireComponent(typeof(Camera))]
public class XPipelineCamera : MonoBehaviour
{
    [SerializeField]
    XPostProcessingStack postProcessingStack = null;
    public XPostProcessingStack PostProcessingStack {
        get {
            return postProcessingStack;
        }
    }
}
