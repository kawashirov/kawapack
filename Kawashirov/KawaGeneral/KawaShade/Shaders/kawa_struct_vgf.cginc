#ifndef KAWA_STRUCT_VGF_INCLUDED
#define KAWA_STRUCT_VGF_INCLUDED

#include ".\kawa_struct_shared.cginc"

#include "AutoLight.cginc"
#include "UnityInstancing.cginc"

struct v2g {
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO

	half2 uv0 : TEXCOORD0;
	#if defined(NEED_UV1)
		half2 uv1 : TEXCOORD1;
	#endif
	float4 vertex : KAWASMNT_VERTEX;
	half3 normal_obj : KAWASMNT_NORMAL_OBJ;
	
	#if defined(KAWAFLT_PASS_FORWARD)
		// Forward-specific
		half3 tangent_obj : KAWASMNT_TANGENT_OBJ;
		half3 bitangent_obj : KAWASMNT_BITANGENT_OBJ;
		#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
			nointerpolation bool vertexlight_on : KAWASMNT_VERTEXLIGHT_ON;
		#endif
	#endif
};

struct g2f {
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO

	float4 pos : SV_POSITION;
	half2 uv0 : TEXCOORD0;
	#if defined(NEED_UV1)
		half2 uv1 : TEXCOORD1;
	#endif
	
	float4 pos_world : KAWASMNT_POS_WORLD;
	half3 normal_world : KAWASMNT_NORMAL_WORLD;

	#if defined(RANDOM_MIX_COORD) || defined(RANDOM_SEED_TEX)
		float4 pos_screen : KAWASMNT_SCREENPOS;
	#endif

	#if defined(KAWAFLT_PASS_FORWARD)
		half3 tangent_world : KAWASMNT_TANGENT_WORLD;
		half3 bitangent_world : KAWASMNT_BITANGENT_WORLD;
		float4 vertex : KAWASMNT_VERTEX;
		#if defined(MATCAP_ON)
			half2 matcap_uv : KAWASMNT_MATCAP_UV;
		#endif
		#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
			half3 vertexlight : KAWASMNT_VERTEXLIGHT;
		#endif
		#if defined(OUTLINE_ON)
			nointerpolation bool is_outline : KAWASMNT_OUTLINE;
		#endif
		UNITY_SHADOW_COORDS(3)
		UNITY_FOG_COORDS(4)
	#endif

	#if defined(DSTFD_ON)
		 float3 dstfd_distance : DSTFD_DISTANCE;
	#endif

	#if defined(IWD_ON)
		half iwd_tint : IWD_FACTOR;
	#endif

	#if defined(PCW_ON) && defined(KAWAFLT_PASS_FORWARDBASE)
		half4 pcw_color : PCW_COLOR;
	#endif
};

#define VERTEX_IN appdata_full_extended
#define VERTEX_OUT v2g
#define GEOMETRY_IN v2g
#define GEOMETRY_OUT g2f
#define FRAGMENT_IN g2f

#endif // KAWA_STRUCT_VGF_INCLUDED
