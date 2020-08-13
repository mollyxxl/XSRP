using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstancedMaterialProperties: MonoBehaviour
{
    [SerializeField]
    Color color = Color.white;
    [SerializeField,Range(0,1f)]
    float smoothness = 0.5f;

    [SerializeField,Range(0,1f)]
    float metallic;

    static MaterialPropertyBlock propertyBlock;
    static int colorID = Shader.PropertyToID("_Color");
    static int metallicId = Shader.PropertyToID("_Metallic");
    static int smoothnessId = Shader.PropertyToID("_Smoothness");

    private void Awake()
    {
        OnValidate();
    }
    private void OnValidate()
    {   if(propertyBlock==null)
           propertyBlock = new MaterialPropertyBlock();

        propertyBlock.SetColor(colorID, color);
        propertyBlock.SetFloat(metallicId, metallic);
        propertyBlock.SetFloat(smoothnessId, smoothness);
        GetComponent<MeshRenderer>().SetPropertyBlock(propertyBlock);
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
