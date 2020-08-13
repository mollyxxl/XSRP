#ifndef XRP_LIT_INCLUDED
#define XRP_LIT_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
#include "Lighting.hlsl"

CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerCamera)
	float3 _WorldSpaceCameraPos;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld,unity_WorldToObject;
	float4 unity_LightIndicesOffsetAndCount;   //x��ƫ����  y��Ӱ�����Ĺ�Դ����
	float4 unity_4LightIndices0,unity_4LightIndices1;
	float4 unity_SpecCube0_BoxMin,unity_SpecCube0_BoxMax;
	float4 unity_SpecCube0_ProbePosition;
	float4 unity_SpecCube1_BoxMin,unity_SpecCube1_BoxMax;
	float4 unity_SpecCube1_ProbePosition;
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
	float4 _MainTex_ST;
	float _Cutoff;
	//float _Smoothness;
CBUFFER_END

//�ȶ���UNITY_MATRIX_M ������
#define UNITY_MATRIX_M  unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

UNITY_INSTANCING_BUFFER_START(PerInstance)
	UNITY_DEFINE_INSTANCED_PROP(float4,_Color)
	UNITY_DEFINE_INSTANCED_PROP(float,_Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float,_Smoothness)
UNITY_INSTANCING_BUFFER_END(PerInstance)

#define  MAX_VISIABLE_LIGHTS 16

CBUFFER_START(_LightBuffer)
	float4 _VisibleLightColors[MAX_VISIABLE_LIGHTS];
	float4 _VisibleLightDirectionsOrPositions[MAX_VISIABLE_LIGHTS];
	float4 _VisibleLightAttenuations[MAX_VISIABLE_LIGHTS];
	float4 _VisibleLightSpotDirections[MAX_VISIABLE_LIGHTS];
CBUFFER_END

CBUFFER_START(_ShadowBuffer)
	float4x4 _WorldToShadowMatrices[MAX_VISIABLE_LIGHTS];
	float4x4 _WorldToShadowCascadeMatrices[5];
	float4 _CascadeCullingSpheres[4];
	float4 _ShadowData[MAX_VISIABLE_LIGHTS];
	float4 _ShadowMapSize;
	float4 _CascadedShadowMapSize;
	float4 _GlobalShadowData;
	float _CascadedShadowStrength;
CBUFFER_END

TEXTURE2D_SHADOW(_ShadowMap);
SAMPLER_CMP(sampler_ShadowMap);

TEXTURE2D_SHADOW(_CascadedShadowMap);
SAMPLER_CMP(sampler_CascadedShadowMap);

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

float3 BoxProjection (
	float3 direction, float3 position,
	float4 cubemapPosition, float4 boxMin, float4 boxMax
) {
	UNITY_BRANCH
	if (cubemapPosition.w > 0) {
		float3 factors =
			((direction > 0 ? boxMax.xyz : boxMin.xyz) - position) / direction;
		float scalar = min(min(factors.x, factors.y), factors.z);
		direction = direction * scalar + (position - cubemapPosition.xyz);
	}
	return direction;
}
float3 SampleEnvironment(LitSurface s){
	float3 reflectVector=reflect(-s.viewDir,s.normal);
	float mip=PerceptualRoughnessToMipmapLevel(s.perceptualRoughness);
	
	float3 uvw=BoxProjection(reflectVector,s.position,
				unity_SpecCube0_ProbePosition,unity_SpecCube0_BoxMin,unity_SpecCube0_BoxMax
			);
	float4 sample=SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0,samplerunity_SpecCube0,uvw,mip);
	float3 color=sample.rgb;

	float blend = unity_SpecCube0_BoxMin.w;
	if (blend < 0.99999) {
		uvw = BoxProjection(
			reflectVector, s.position,
			unity_SpecCube1_ProbePosition,
			unity_SpecCube1_BoxMin, unity_SpecCube1_BoxMax
		);
		sample = SAMPLE_TEXTURECUBE_LOD(
			unity_SpecCube1, samplerunity_SpecCube0, uvw, mip
		);
		color = lerp(sample.rgb, color, blend);
	}
	return color;
}

float3 LightSurface(LitSurface s,float3 lightDir)
{
	float3 color=s.diffuse;
	if(!s.perfectDiffuser){
		float3 halfDir=SafeNormalize(lightDir+s.viewDir);
		float nh=saturate(dot(s.normal,halfDir));
		float lh=saturate(dot(lightDir,halfDir));
		float d=nh*nh*(s.roughness*s.roughness -1.0)+1.00001;
		float normalizationTerm =s.roughness*4.0 + 2.0;
		float specularTerm=s.roughness*s.roughness;
		specularTerm /= (d * d) * max(0.1, lh * lh) * normalizationTerm;
		color += specularTerm * s.specular;
	}
	return color*saturate(dot(s.normal,lightDir));
}

float DistanceToCameraSqr (float3 worldPos) {
	float3 cameraToFragment = worldPos - _WorldSpaceCameraPos;
	return dot(cameraToFragment, cameraToFragment);
}
float HardShadowAttenuation (float4 shadowPos, bool cascade = false) {
	if (cascade) {
		return SAMPLE_TEXTURE2D_SHADOW(
			_CascadedShadowMap, sampler_CascadedShadowMap, shadowPos.xyz
		);
	}
	else {
		return SAMPLE_TEXTURE2D_SHADOW(
			_ShadowMap, sampler_ShadowMap, shadowPos.xyz
		);
	}
}
float SoftShadowAttenuation (float4 shadowPos, bool cascade = false) {
	real tentWeights[9];
	real2 tentUVs[9];
	float4 size = cascade ? _CascadedShadowMapSize : _ShadowMapSize;
	SampleShadow_ComputeSamples_Tent_5x5(
		size, shadowPos.xy, tentWeights, tentUVs
	);
	float attenuation = 0;
	for (int i = 0; i < 9; i++) {
		attenuation += tentWeights[i] * HardShadowAttenuation(
			float4(tentUVs[i].xy, shadowPos.z, 0), cascade
		);
	}
	return attenuation;
}
float ShadowAttenuation (int index, float3 worldPos) {
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#elif !defined(_SHADOWS_HARD) && !defined(_SHADOWS_SOFT)
		return 1.0;
	#endif
	if (
		_ShadowData[index].x <= 0 ||
		DistanceToCameraSqr(worldPos) > _GlobalShadowData.y
	) {
		return 1.0;
	}
	float4 shadowPos = mul(_WorldToShadowMatrices[index], float4(worldPos, 1.0));
	shadowPos.xyz /= shadowPos.w;
	shadowPos.xy = saturate(shadowPos.xy);
	shadowPos.xy = shadowPos.xy * _GlobalShadowData.x + _ShadowData[index].zw;
	float attenuation;
	
	#if defined(_SHADOWS_HARD)
		#if defined(_SHADOWS_SOFT)
			if (_ShadowData[index].y == 0) {
				attenuation = HardShadowAttenuation(shadowPos);
			}
			else {
				attenuation = SoftShadowAttenuation(shadowPos);
			}
		#else
			attenuation = HardShadowAttenuation(shadowPos);
		#endif
	#else
		attenuation = SoftShadowAttenuation(shadowPos);
	#endif
	
	return lerp(1, attenuation, _ShadowData[index].x);
}
float InsideCascadeCullingSphere (int index, float3 worldPos) {
	float4 s = _CascadeCullingSpheres[index];
	return dot(worldPos - s.xyz, worldPos - s.xyz) < s.w;
}
float CascadedShadowAttenuation(float3 worldPos)
{	
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#elif  !defined(_CASCADED_SHADOWS_HARD) && !defined(_CASCADED_SHADOWS_SOFT)
		return 1.0;
	#endif

	if(DistanceToCameraSqr(worldPos) > _GlobalShadowData.y)
	{
		return 1.0;
	}

	float4 cascadeFlags=float4(
		InsideCascadeCullingSphere(0,worldPos),
		InsideCascadeCullingSphere(1,worldPos),
		InsideCascadeCullingSphere(2,worldPos),
		InsideCascadeCullingSphere(3,worldPos)
	);
	cascadeFlags.yzw=saturate(cascadeFlags.yzw - cascadeFlags.xyz);
	float cascadeIndex= 4 - dot(cascadeFlags,float4(4,3,2,1));
	float4 shadowPos=mul(_WorldToShadowCascadeMatrices[cascadeIndex],float4(worldPos,1.0));
	float attenuation;
	#if defined(_CASCADED_SHADOWS_HARD)
		attenuation = HardShadowAttenuation(shadowPos,true);
	#else
		attenuation = SoftShadowAttenuation(shadowPos,true);
	#endif

	return lerp(1,attenuation,_CascadedShadowStrength);
}
float3 MainLight(LitSurface s)
{
	float shadowAttenuation=CascadedShadowAttenuation(s.position);
	float3 lightColor=_VisibleLightColors[0].rgb;
	float3 lightDirection=_VisibleLightDirectionsOrPositions[0].xyz;
	//float diffuse=saturate(dot(normal,lightDirection));
	float3 color=LightSurface(s,lightDirection);
	color*=shadowAttenuation;
	return color*lightColor;
}

float3 GenericLight(int index,LitSurface s,float shadowAttenuation)
{
	float3 lightColor=_VisibleLightColors[index].rgb;
	float4 lightPositionOrDirection=_VisibleLightDirectionsOrPositions[index];
	float4 lightAttenuation=_VisibleLightAttenuations[index];
	float3 spotDirection=_VisibleLightSpotDirections[index].xyz;

	float3 lightVector = lightPositionOrDirection.xyz - s.position * lightPositionOrDirection.w;
	float3 lightDirection=normalize(lightVector);

	float3 color=LightSurface(s,lightDirection);
	//float diffuse=saturate(dot(normal,lightDirection));
	//���˥��
	float rangeFade=dot(lightVector,lightVector)*lightAttenuation.x;
	rangeFade=saturate(1.0-rangeFade*rangeFade);
	rangeFade*=rangeFade;

	//spot 
	float spotFade=dot(spotDirection,lightDirection);
	spotFade=saturate(spotFade*lightAttenuation.z+lightAttenuation.w);
	spotFade*=spotFade;

	float distanceSqr=max(dot(lightVector,lightVector),0.00001);
	color *= shadowAttenuation*spotFade*rangeFade/distanceSqr;

	return color*lightColor;
}

struct VertexInput{
	float4 pos:POSITION;
	float3 normal:NORMAL;
	float2 uv:TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput{
	float4 clipPos:SV_POSITION;
	float3 normal:TEXCOORD0;
	float3 worldPos:TEXCOORD1;
	float3 vertexLighting:TEXCOORD2;
	float2 uv:TEXCOORD3;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

VertexOutput LitPassVertex(VertexInput input)
{
	VertexOutput output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input,output);
	float4 worldPos=mul(UNITY_MATRIX_M,float4(input.pos.xyz,1.0));
	output.clipPos= mul(unity_MatrixVP,worldPos);
	#if defined(UNITY_ASSUME_UNIFORM_SCALING)
		output.normal=mul((float3x3)UNITY_MATRIX_M,input.normal);
	#else
		output.normal=normalize(mul(input.normal,(float3x3)UNITY_MATRIX_I_M));
	#endif
	output.worldPos=worldPos.xyz;

	LitSurface surface=GetLitSurfaceVertex(output.normal,output.worldPos);
	//���ڵ������Ч��Զ�����һ�����Ч�������Էŵ���������Ϊ�������
	output.vertexLighting=0;
	for(int i=4;i<min(unity_LightIndicesOffsetAndCount.y,8);i++)
	{
		int lightIndex=unity_4LightIndices1[i-4];//����8յ�ƣ���Ҫunity_4LightIndices1
		output.vertexLighting+=GenericLight(lightIndex,surface,1);
	}
	output.uv=TRANSFORM_TEX(input.uv,_MainTex);
	return output;
}
float4 LitPassFragment(VertexOutput input,FRONT_FACE_TYPE isFrontFace:FRONT_FACE_SEMANTIC):SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	input.normal=normalize(input.normal);
	input.normal = IS_FRONT_VFACE(isFrontFace, input.normal, -input.normal);
	
	float4 albedoAlpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
	albedoAlpha *= UNITY_ACCESS_INSTANCED_PROP(PerInstance,_Color);

	#if defined(_CLIPPING_ON)
	clip(albedoAlpha.a - _Cutoff);
	#endif

	float3 viewDir=normalize(_WorldSpaceCameraPos-input.worldPos.xyz);
	LitSurface surface = GetLitSurface(
		input.normal,input.worldPos,viewDir,
		albedoAlpha.rgb,
		UNITY_ACCESS_INSTANCED_PROP(PerInstance,_Metallic),
		UNITY_ACCESS_INSTANCED_PROP(PerInstance,_Smoothness)
	);

	//float3 diffuseLight=input.vertexLighting;   //�������
	float3 color=input.vertexLighting * surface.diffuse;

	#if defined(_CASCADED_SHADOWS_HARD) ||defined(_CASCADED_SHADOWS_SOFT)
			color+=MainLight(surface);
	#endif

	//for(int i=0;i<MAX_VISIABLE_LIGHTS;i++)
	//����Light indices����ѭ��
	for(int i=0;i<min(unity_LightIndicesOffsetAndCount.y,4);i++)
	{
		int lightIndex=unity_4LightIndices0[i];//����4յ�ƣ�����ֻ��Ҫunity_4LightIndices0����
		float shadowAttenuation=ShadowAttenuation(lightIndex,input.worldPos);
		color+=GenericLight(lightIndex,surface,shadowAttenuation);
	}

	color += ReflectEnvironment(surface,SampleEnvironment(surface));

	return float4(color,albedoAlpha.a);
}
#endif   //XRP_LIT_INCLUDED