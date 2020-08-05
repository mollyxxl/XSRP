#ifndef XRP_UNLIT_INCLUDED
#define XRP_UNLIT_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/common.hlsl"

CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
CBUFFER_END

struct VertexInput{
	float4 pos:POSITION;
};

struct VertexOutput{
	float4 clipPos:SV_POSITION;
};

VertexOutput UnlitPassVertex(VertexInput input)
{
	VertexOutput output;
	float4 worldPos=mul(unity_ObjectToWorld,float4(input.pos.xyz,1.0));
	output.clipPos= mul(unity_MatrixVP,worldPos);
	return output;
}
float4 UnlitPassFragment(VertexOutput input):SV_TARGET
{
	return 1;
}
#endif   //XRP_UNLIT_INCLUDED