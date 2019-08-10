#ifndef KAWAFLT_STRUCT_VF_INCLUDED
#define KAWAFLT_STRUCT_VF_INCLUDED

#include ".\KawaFLT_Struct_Shared.cginc"

#include "UnityInstancing.cginc"
#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "Lighting.cginc"

#define PIPELINE_VF 1

struct v2f {
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO

	// Shared values
	float4 pos : SV_POSITION;
	float2 uv0 : TEXCOORD1;
	
	float4 posWorld : KAWASMNT_POS_WORLD;

	#if defined(RANDOM_MIX_COORD) || defined(RANDOM_SEED_TEX)
		float4 screenPos : KAWASMNT_SCREENPOS;
	#endif

	#if defined(NEED_CULL)
		nointerpolation bool cull : KAWASMNT_CULL;
	#endif
	
	// Forward-only
	#if defined(KAWAFLT_PASS_FORWARD)
		float2 uv1 : TEXCOORD2;
		half3 normalDir : KAWASMNT_NORMAL_DIR;
		half3 tangentDir : KAWASMNT_TANGENT_DIR;
		half3 bitangentDir : KAWASMNT_BITANGENT_DIR;
		#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
			half3 vertexlight : KAWASMNT_VERTEXLIGHT;
			#if defined(UNITY_SHOULD_SAMPLE_SH) && (defined(SHADE_KAWAFLT_LOG) || defined(SHADE_KAWAFLT_SINGLE))
				half3 ambient : KAWASMNT_AMBIENT;
			#endif
		#endif
		SHADOW_COORDS(3)
		UNITY_FOG_COORDS(4)
	#endif
	
	// ShadowCaster-only
	#if defined(KAWAFLT_PASS_SHADOWCASTER)
		V2F_SHADOW_CASTER_NOPOS // TEXCOORD0
	#endif

	#if defined(DSTFD_ON)
		float3 dstfdDistance : DSTFD_DISTANCE;
	#endif
};


#define VERTEX_IN appdata_full
#define VERTEX_OUT v2f
#define FRAGMENT_IN v2f

#endif // KAWAFLT_STRUCT_VF_INCLUDED
