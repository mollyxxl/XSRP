#ifndef XRP_LIT_INCLUDED
#define XRP_LIT_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/common.hlsl"

CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4 unity_LightIndicesOffsetAndCount;   //x：偏移量  y：影响对象的光源数量
	float4 unity_4LightIndices0,unity_4LightIndices1;
CBUFFER_END

//先定义UNITY_MATRIX_M 再引用
#define UNITY_MATRIX_M  unity_ObjectToWorld
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

UNITY_INSTANCING_BUFFER_START(PerInstance)
	UNITY_DEFINE_INSTANCED_PROP(float4,_Color)
UNITY_INSTANCING_BUFFER_END(PerInstance)

#define  MAX_VISIABLE_LIGHTS 4

CBUFFER_START(_LightBuffer)
	float4 _VisibleLightColors[MAX_VISIABLE_LIGHTS];
	float4 _VisibleLightDirectionsOrPositions[MAX_VISIABLE_LIGHTS];
	float4 _VisiableLightAttenuations[MAX_VISIABLE_LIGHTS];
	float4 _VisiableLightSpotDirections[MAX_VISIABLE_LIGHTS];
CBUFFER_END

float3 DiffuseLight(int index,float3 normal,float3 worldPos)
{
	float3 lightColor=_VisibleLightColors[index].rgb;
	float4 lightPositionOrDirection=_VisibleLightDirectionsOrPositions[index];
	float4 lightAttenuation=_VisiableLightAttenuations[index];
	float3 spotDirection=_VisiableLightSpotDirections[index].xyz;

	float3 lightVector = lightPositionOrDirection.xyz - worldPos * lightPositionOrDirection.w;
	float3 lightDirection=normalize(lightVector);
	float diffuse=saturate(dot(normal,lightDirection));
	//光的衰减
	float rangeFade=dot(lightVector,lightVector)*lightAttenuation.x;
	rangeFade=saturate(1.0-rangeFade*rangeFade);
	rangeFade*=rangeFade;

	//spot 
	float spotFade=dot(spotDirection,lightDirection);
	spotFade=saturate(spotFade*lightAttenuation.z+lightAttenuation.w);
	spotFade*=spotFade;

	float distanceSqr=max(dot(lightVector,lightVector),0.00001);
	diffuse *= spotFade*rangeFade/distanceSqr;

	return diffuse*lightColor;
}

struct VertexInput{
	float4 pos:POSITION;
	float3 normal:NORMAL;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput{
	float4 clipPos:SV_POSITION;
	float3 normal:TEXCOORD0;
	float3 worldPos:TEXCOORD1;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

VertexOutput LitPassVertex(VertexInput input)
{
	VertexOutput output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input,output);
	float4 worldPos=mul(UNITY_MATRIX_M,float4(input.pos.xyz,1.0));
	output.clipPos= mul(unity_MatrixVP,worldPos);
	output.normal=mul((float3x3)UNITY_MATRIX_M,input.normal);
	output.worldPos=worldPos.xyz;
	return output;
}
float4 LitPassFragment(VertexOutput input):SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	input.normal=normalize(input.normal);
	
	float3 albedo=UNITY_ACCESS_INSTANCED_PROP(PerInstance,_Color).rgb;
	float3 diffuseLight=0;

	//for(int i=0;i<MAX_VISIABLE_LIGHTS;i++)
	//根据Light indices进行循环
	for(int i=0;i<min(unity_LightIndicesOffsetAndCount.y,4);i++)
	{
		int lightIndex=unity_4LightIndices0[i];//限制4盏灯，所以只需要unity_4LightIndices0即可
		diffuseLight+=DiffuseLight(lightIndex,input.normal,input.worldPos);
	}
	for(int i=4;i<min(unity_LightIndicesOffsetAndCount.y,4);i++)
	{
		int lightIndex=unity_4LightIndices1[i-4];//限制8盏灯，需要unity_4LightIndices1
		diffuseLight+=DiffuseLight(lightIndex,input.normal,input.worldPos);
	}

	float3 color=diffuseLight * albedo;
	return float4(color,1);
}
#endif   //XRP_LIT_INCLUDED