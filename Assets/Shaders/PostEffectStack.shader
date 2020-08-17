Shader "Hidden/XPipeline/PostEffectStack"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off

        HLSLINCLUDE
        #include "../ShaderLibrary/PostEffectStack.hlsl"
        ENDHLSL

        Pass
        {
           //0 copy
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment CopyPassFragment
            ENDHLSL
        }
        Pass
        {
           //1 blur
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BlurPassFragment
            ENDHLSL
        }
    }
}
