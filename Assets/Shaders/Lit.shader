Shader "X Pipeline/Lit"
{
    Properties{
       _Color("Color",Color)=(1,1,1,1)
    }
    SubShader
    {
        Pass
        {
           HLSLPROGRAM
           #pragma target 3.5

           #pragma multi_compile_instancing
           //除了对象-世界（object-to-world）矩阵之外，默认情况下，世界-对象（world-to-object）矩阵也放置在实例化缓冲区中。
           //他们是M矩阵的逆，当使用非均匀缩放时，是法向向量所必需的。但是我们只使用统一的缩放比例，因此不需要那些额外的矩阵。
           //通过在着色器中添加#pragma instancing_options假定uniformscaling指令来通知Unity。
           #pragma instancing_options assumeuniformscaling
           #pragma multi_compile _ _SHADOWS_HARD
           #pragma multi_compile _ _SHADOWS_SOFT

           #pragma vertex LitPassVertex
           #pragma fragment LitPassFragment

           #include "../ShaderLibrary/Lit.hlsl"

           ENDHLSL
        }

        Pass
        {
           Tags{ "LightMode" = "ShadowCaster"}

           HLSLPROGRAM
           #pragma target 3.5

           #pragma multi_compile_instancing
           //除了对象-世界（object-to-world）矩阵之外，默认情况下，世界-对象（world-to-object）矩阵也放置在实例化缓冲区中。
           //他们是M矩阵的逆，当使用非均匀缩放时，是法向向量所必需的。但是我们只使用统一的缩放比例，因此不需要那些额外的矩阵。
           //通过在着色器中添加#pragma instancing_options假定uniformscaling指令来通知Unity。
           #pragma instancing_options assumeuniformscaling

           #pragma vertex ShadowCasterPassVertex
           #pragma fragment ShadowCasterPassFragment

           #include "../ShaderLibrary/ShadowCaster.hlsl"

           ENDHLSL
        }
    }
}
