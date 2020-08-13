#ifndef XRP_LIGHTING_INCLUDED
#define XRP_LIGHTING_INCLUDED

//IBL相关
TEXTURECUBE(unity_SpecCube0);
TEXTURECUBE(unity_SpecCube1);
SAMPLER(samplerunity_SpecCube0);

struct LitSurface{
	float3 normal,position,viewDir;
	float3 diffuse,specular;
	float perceptualRoughness, roughness,fresnelStrength,reflectivity;
	bool perfectDiffuser;
};

LitSurface GetLitSurface(
		float3 normal,float3 position,float3 viewDir,
		float3 color,float metallic,float smoothness,bool perfectDiffuser =false)
{
	LitSurface s;
	s.normal=normal;
	s.position=position;
	s.viewDir=viewDir;
	s.diffuse=color;
	if(perfectDiffuser){
		s.reflectivity=0.0;
		smoothness=0.0;
		s.specular=0.0;
	}
	else
	{
		s.specular=lerp(0.04,color,metallic);
		s.reflectivity=lerp(0.04,1.0,metallic);
		s.diffuse *=1.0 - s.reflectivity;
	}
	s.perfectDiffuser = perfectDiffuser;
	s.perceptualRoughness = 1.0-smoothness;
	s.roughness=s.perceptualRoughness*s.perceptualRoughness;
	s.fresnelStrength=saturate(smoothness+s.reflectivity);
	return s;
}
//顶点光照
LitSurface GetLitSurfaceVertex(float3 normal,float3 position)
{
	return GetLitSurface(normal,position,0,1,0,0,true);
}

float3 ReflectEnvironment(LitSurface s,float3 environment)
{
	if(s.perfectDiffuser)
	{
		return 0;
	}
	environment*= s.specular;
	environment /= s.roughness * s.roughness + 1.0;
	float fresnel=Pow4(1.0-saturate(dot(s.normal,s.viewDir)));
	environment*=lerp(s.specular,s.fresnelStrength,fresnel);
	return environment;
}
void PremultiplyAlpha(inout LitSurface s,inout float alpha)
{
	s.diffuse *=alpha;
	alpha=lerp(alpha,1,s.reflectivity);
}

LitSurface GetLitSurfaceMeta(float3 color,float metallic,float smoothness)
{
	return GetLitSurface(0,0,0,color,metallic,smoothness);
}

#endif   //XRP_LIGHTING_INCLUDED