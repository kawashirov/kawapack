#ifndef KAWAFLT_STRUCT_VF_INCLUDED
#define KAWAFLT_STRUCT_VF_INCLUDED

#include ".\KawaFLT_Struct_Shared.cginc"

#include "UnityInstancing.cginc"
#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "Lighting.cginc"

struct v2f {
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO

	// Shared values
	float4 pos : SV_POSITION;
	half2 uv0 : TEXCOORD1;
	float4 pos_world : KAWASMNT_POS_WORLD;
	half3 normal_world : KAWASMNT_NORMAL_WORLD;

	#if defined(RANDOM_MIX_COORD) || defined(RANDOM_SEED_TEX)
		float4 pos_screen : KAWASMNT_SCREENPOS;
	#endif

	#if defined(NEED_CULL)
		nointerpolation bool cull : KAWASMNT_CULL;
	#endif
	
	// Forward-only
	#if defined(KAWAFLT_PASS_FORWARD)
		half2 uv1 : TEXCOORD2;
		half3 tangent_world : KAWASMNT_TANGENT_WORLD;
		half3 bitangent_world : KAWASMNT_BITANGENT_WORLD;
		#if defined(KAWAFLT_F_MATCAP_ON)
			half3 matcap_x : KAWASMNT_MATCAP_X;
			half3 matcap_y : KAWASMNT_MATCAP_Y;
		#endif
		#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
			half3 vertexlight : KAWASMNT_VERTEXLIGHT;
			#if defined(UNITY_SHOULD_SAMPLE_SH) && (defined(SHADE_KAWAFLT_LOG) || defined(SHADE_KAWAFLT_SINGLE))
				half3 ambient : KAWASMNT_AMBIENT;
			#endif
		#endif
		UNITY_SHADOW_COORDS(1337)
		UNITY_FOG_COORDS(4)
	#endif
	
	#if defined(DSTFD_ON)
		float3 dstfd_distance : DSTFD_DISTANCE;
	#endif
};


#define VERTEX_IN appdata_full
#define VERTEX_OUT v2f
#define FRAGMENT_IN v2f

#endif // KAWAFLT_STRUCT_VF_INCLUDED
