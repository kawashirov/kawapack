#ifndef KAWAFLT_PREFRAG_VF_INCLUDED
#define KAWAFLT_PREFRAG_VF_INCLUDED

#include ".\KawaFLT_Struct_VF.cginc"
#include "KawaRND.cginc"
#include "UnityInstancing.cginc"
#include ".\KawaFLT_Features_Lightweight.cginc"
#include ".\KawaFLT_PreFrag_Shared.cginc"

#include ".\kawa_feature_distance_fade.cginc"
#include ".\kawa_feature_fps.cginc"
#include ".\kawa_feature_matcap.cginc"


VERTEX_OUT vert(appdata_full v_in) {
	UNITY_SETUP_INSTANCE_ID(v_in);
	VERTEX_OUT v_out;
	//UNITY_INITIALIZE_OUTPUT(VERTEX_OUT, v_out);
	v_out = (VERTEX_OUT) 0xabcdef; // FIXME
	UNITY_TRANSFER_INSTANCE_ID(v_in, v_out);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v_out);

	half3 normal_obj = normalize(v_in.normal);

	v_out.uv0 = v_in.texcoord;
	#if defined(NEED_UV1)
		v_out.uv1 = v_in.texcoord1;
	#endif
	
	fps_vertex(v_in, v_out);

	v_out.pos = UnityObjectToClipPos(v_in.vertex); 
	v_out.pos_world = mul(unity_ObjectToWorld, v_in.vertex);
	v_out.normal_world = normalize(UnityObjectToWorldNormal(normal_obj));

	screencoords_fragment_in(v_out);
	
	#if defined(KAWAFLT_PASS_FORWARD)

		// Тангентное-пространство в координатах 
		half tangent_w = v_in.tangent.w; // Определяет леворукость/праворукость/зеркальность?
		half3 tangent_obj = normalize(v_in.tangent.xyz);
		half3 bitangent_obj = normalize(cross(normal_obj, tangent_obj) * tangent_w);

		// Тангентное-пространство в координатах мира
		v_out.tangent_world = normalize(UnityObjectToWorldDir(tangent_obj));
		v_out.bitangent_world = normalize(cross(v_out.normal_world, v_out.tangent_world) * tangent_w);
		
		// (v_out.world_normal) -> (v_out.matcap_uv)
		matcap_calc_uv(v_out);

		bool vertexlight = false;
		#if defined(VERTEXLIGHT_ON)
			vertexlight = true;
		#endif
		float3 wsvd = KawaWorldSpaceViewDir(v_out.pos_world.xyz);
		kawaflt_fragment_in(v_out, /* compile-time */ vertexlight, wsvd);

		// (vertex_obj, v_out.pos) -> (v_out._ShadowCoord)
		#if defined(SHADOWS_SHADOWMASK)
			v_out._ShadowCoord = (float4) 0xabcdef;
		#endif

		prefrag_transfer_shadow(v_in.vertex, v_out);
		UNITY_TRANSFER_FOG(v_out, v_out.pos);
	#endif
	
	prefrag_shadowcaster_pos(v_in.vertex.xyz, normal_obj, v_out.pos);
	
	dstfade_frament_in(v_out);

	return v_out;
}

#endif // KAWAFLT_PREFRAG_VF_INCLUDED