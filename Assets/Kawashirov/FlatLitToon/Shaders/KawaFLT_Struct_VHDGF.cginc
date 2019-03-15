#ifndef KAWAFLT_STRUCT_VHDGF_INCLUDED
#define KAWAFLT_STRUCT_VHDGF_INCLUDED

#include ".\KawaFLT_Struct_Shared.cginc"

#include "AutoLight.cginc"
#include "UnityInstancing.cginc"

uniform float _Tsltn_Uni;
uniform float _Tsltn_Nrm;
uniform float _Tsltn_Inside;

#if !defined(TESS_D_QUAD)
	#define TESS_D_TRI 1
#endif

struct v2g {
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO

	float2 uv0 : TEXCOORD0;
	float4 vertex : KAWASMNT_VERTEX;
	half3 normal : KAWASMNT_NORMAL;
	
	#if defined(KAWAFLT_PASS_FORWARD)
		// Forward-specific
		float2 uv1 : TEXCOORD1;
		// float4 tangent : KAWASMNT_TANGENT;
		//float4 posWorld : KAWASMNT_POS_WORLD;
		half3 normalDir : KAWASMNT_NORMAL_DIR;
		half3 tangentDir : KAWASMNT_TANGENT_DIR;
		half3 bitangentDir : KAWASMNT_BITANGENT_DIR;
		//UNITY_SHADOW_COORDS(2)
		#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
			nointerpolation bool vertexlight_on : KAWASMNT_VERTEXLIGHT_ON;
		#endif
	#endif

	#if defined(DSTFD_ON) && defined(DSTFD_RANDOM_VERTEX)
		// float3 dstfdRandom : DSTFD_RANDOM; 
	#endif

	#if defined(NEED_CULL)
		nointerpolation bool cull : KAWASMNT_CULL;
	#endif
};


struct g2f {
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO

	// Shared values
	float4 pos : SV_POSITION;
	float2 uv0 : TEXCOORD1;
	
	float4 posWorld : KAWASMNT_POS_WORLD;

	#if defined(NEED_SCREENPOS)
		float4 screenPos : KAWASMNT_SCREENPOS;
		// float2 screenCoords : KAWASMNT_SCREENCOORDS;
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
		float4 vertex : KAWASMNT_VERTEX;
		#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
			half3 vertexlight : KAWASMNT_VERTEXLIGHT;
			#if defined(SHADE_KAWAFLT_DIFFUSE) && defined(UNITY_SHOULD_SAMPLE_SH)
				half3 ambient : KAWASMNT_AMBIENT;
			#endif
		#endif
		#if defined(TINTED_OUTLINE) || defined(COLORED_OUTLINE)
			bool is_outline : KAWASMNT_OUTLINE;
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
		 #if defined(DSTFD_RANDOM_VERTEX)
			// float3 dstfdRandom : DSTFD_RANDOM; 
		#endif
	#endif
	
	#if defined(DSNTGRT_FACE)
		half dsntgrtFactor : DSNTGRT_FACTOR;
	#endif
	
	#if defined(PCW_ON)
		half4 pcwColor : PCW_COLOR;
	#endif
};

#define VERTEX_IN appdata_full
#define VERTEX_OUT v2g
#define HULL_IN v2g
#define HULL_OUT v2g
#define DOMAIN_IN v2g
#define DOMAIN_OUT v2g
#define GEOMETRY_IN v2g
#define GEOMETRY_OUT g2f
#define FRAGMENT_IN g2f

#endif // KAWAFLT_STRUCT_VHDGF_INCLUDED
