#ifndef KAWA_PREFRAG_VF_INCLUDED
#define KAWA_PREFRAG_VF_INCLUDED

VERTEX_OUT vert(VERTEX_IN v_in, in uint v_id : SV_VertexID) {
	UNITY_SETUP_INSTANCE_ID(v_in);
	VERTEX_OUT v_out;
	//UNITY_INITIALIZE_OUTPUT(VERTEX_OUT, v_out);
	v_out = (VERTEX_OUT) 0xabcdef; // FIXME
	UNITY_TRANSFER_INSTANCE_ID(v_in, v_out);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v_out);
	
	uint rnd_tri = v_id; // rnd_init_noise_uint(v_id);
	rnd_init(rnd_tri);
	
	apply_dps(v_in);
	
	half3 normal_obj = normalize(v_in.normal);

	v_out.uv0 = v_in.texcoord;
	apply_bitloss2(v_out.uv0, uint2(39736, 51879), uint2(33058, 38227), true);
	#if defined(NEED_UV1)
		v_out.uv1 = v_in.texcoord1;
		apply_bitloss2(v_out.uv1, uint2(55355, 35850), uint2(36711, 50447), true);
	#endif
	
	fps_vertex(v_in, v_out);

	v_out.pos = UnityObjectToClipPos(v_in.vertex);
	// apply_bitloss4(v_out.pos, uint4(58839, 57528, 40864, 38292), uint4(60970, 56821, 47165, 54475), true);
	apply_bitloss3(v_out.pos.xyz, uint3(58839, 57528, 40864), uint3(60970, 56821, 47165), true);
	
	v_out.pos_world = mul(unity_ObjectToWorld, v_in.vertex);
	apply_bitloss4(v_out.pos_world, uint4(48666, 33763, 50100, 57693), uint4(58400, 39726, 63251, 46769), true);
	
	v_out.normal_world = UnityObjectToWorldNormal(normal_obj);
	apply_bitloss3(v_out.normal_world, uint3(59947, 64539, 61205), uint3(47834, 55784, 51766), true);
	v_out.normal_world = normalize(v_out.normal_world);

	// (v_out.pos) -> (v_out.pos)
	psx_prefrag(v_out);
	// (v_out.pos) -> (v_out.pos_screen)
	screencoords_fragment_in(v_out);
	
	#if defined(KAWAFLT_PASS_FORWARD)

		// Тангентное-пространство в координатах 
		half tangent_w = v_in.tangent.w; // Определяет леворукость/праворукость/зеркальность?
		half3 tangent_obj = normalize(v_in.tangent.xyz);
		half3 bitangent_obj = normalize(cross(normal_obj, tangent_obj) * tangent_w);

		// Тангентное-пространство в координатах мира
		v_out.tangent_world = UnityObjectToWorldDir(tangent_obj);
		apply_bitloss3(v_out.tangent_world, uint3(44466, 40896, 52619), uint3(62055, 45194, 52148), true);
		v_out.tangent_world = normalize(v_out.tangent_world);
		
		v_out.bitangent_world = cross(v_out.normal_world, v_out.tangent_world) * tangent_w;
		apply_bitloss3(v_out.bitangent_world, uint3(61661, 51423, 43258), uint3(43410, 42253, 40283), true);
		v_out.bitangent_world = normalize(v_out.bitangent_world);
		
		float3 wsvd = UnityWorldSpaceViewDir(v_out.pos_world.xyz);
		apply_bitloss3(wsvd, uint3(44380, 44971, 50874), uint3(65110, 35381, 38937), true);
		half3 wsvd_norm = normalize(wsvd);
		
		// (v_out.world_normal) -> (v_out.matcap_uv)
		matcap_calc_uv(v_out, wsvd_norm);

		bool vertexlight = false;
		#if defined(VERTEXLIGHT_ON)
			vertexlight = true;
		#endif
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

#endif // KAWA_PREFRAG_VF_INCLUDED