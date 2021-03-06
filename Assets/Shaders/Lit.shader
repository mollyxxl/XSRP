﻿Shader "X Pipeline/Lit"
{
    Properties{
       _Color("Color",Color)=(1,1,1,1)
       _MainTex("RGB:Albedo  A:Alpha",2D)="white"{}
       //[Toggle(_CLIPPING)] _Clipping("Alpha Clipping",float)=0
       [KeywordEnum(Off,On,Shadows)]_Clipping("Alpha Clipping",float)=0
       _Cutoff("Alpha Cutoff",Range(0,1))=0.5
       _Metallic("Metallic",Range(0,1))=0.5
       _Smoothness("Smoothness",Range(0,1))=0.5
       [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
       [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull",Float)=2
       [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend",Float)=1
       [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend",Float)=0
       [Enum(Off,0,On,1)] _ZWrite("Z Write",Float)=1
       [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
       [Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
    }
    SubShader
    {
        Pass
        {
           Blend [_SrcBlend] [_DstBlend]
           Cull[_Cull]
           ZWrite[_ZWrite]
           HLSLPROGRAM
           #pragma target 3.5

           #pragma multi_compile_instancing
           //除了对象-世界（object-to-world）矩阵之外，默认情况下，世界-对象（world-to-object）矩阵也放置在实例化缓冲区中。
           //他们是M矩阵的逆，当使用非均匀缩放时，是法向向量所必需的。但是我们只使用统一的缩放比例，因此不需要那些额外的矩阵。
           //通过在着色器中添加#pragma instancing_options假定uniformscaling指令来通知Unity。
           //#pragma instancing_options assumeuniformscaling
           #pragma multi_compile _ _CASCADED_SHADOWS_HARD _CASCADED_SHADOWS_SOFT
           #pragma multi_compile _ _SHADOWS_HARD
           #pragma multi_compile _ _SHADOWS_SOFT
           #pragma multi_compile _ LIGHTMAP_ON
           #pragma multi_compile _ DYNAMICLIGHTMAP_ON
           #pragma multi_compile _ _SHADOWMASK _DISTANCE_SHADOWMASK _SUBTRACTIVE_LIGHTING
           #pragma multi_compile _ LOD_FADE_CROSSFADE

           #pragma shader_feature _CLIPPING_ON
           #pragma shader_feature _RECEIVE_SHADOWS
           #pragma shader_feature _PREMULTIPLY_ALPHA

           #pragma vertex LitPassVertex
           #pragma fragment LitPassFragment

           #include "../ShaderLibrary/Lit.hlsl"

           ENDHLSL
        }

        Pass
        {
           Tags{ "LightMode" = "ShadowCaster"}
           Cull[_Cull]
           HLSLPROGRAM
           #pragma target 3.5

           #pragma multi_compile_instancing
           //除了对象-世界（object-to-world）矩阵之外，默认情况下，世界-对象（world-to-object）矩阵也放置在实例化缓冲区中。
           //他们是M矩阵的逆，当使用非均匀缩放时，是法向向量所必需的。但是我们只使用统一的缩放比例，因此不需要那些额外的矩阵。
           //通过在着色器中添加#pragma instancing_options假定uniformscaling指令来通知Unity。
          // #pragma instancing_options assumeuniformscaling

           #pragma shader_feature _CLIPPING_OFF
           #pragma multi_compile _ LOD_FADE_CROSSFADE

           #pragma vertex ShadowCasterPassVertex
           #pragma fragment ShadowCasterPassFragment

           #include "../ShaderLibrary/ShadowCaster.hlsl"

           ENDHLSL
        }

        Pass {
			Tags {
				"LightMode" = "Meta"
			}
			
			Cull Off
			
			HLSLPROGRAM
			
			#pragma vertex MetaPassVertex
			#pragma fragment MetaPassFragment
			
			#include "../ShaderLibrary/Meta.hlsl"
			
			ENDHLSL
		}
    }
    CustomEditor "LitShaderGUI"
}
