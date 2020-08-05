﻿Shader "X Pipeline/Unlit"
{
    Properties{
       
    }
    SubShader
    {
        Pass
        {
           HLSLPROGRAM
           #pragma target 3.5
           #pragma vertex UnlitPassVertex
           #pragma fragment UnlitPassFragment

           #include "../ShaderLibrary/Unlit.hlsl"

           ENDHLSL
        }
    }
}
