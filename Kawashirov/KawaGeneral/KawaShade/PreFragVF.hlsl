#ifndef KAWA_PREFRAG_VF_INCLUDED
#define KAWA_PREFRAG_VF_INCLUDED

VERTEX_OUT vert(VERTEX_IN v_in) {
	UNITY_SETUP_INSTANCE_ID(v_in);
	VERTEX_OUT v_out;
	//UNITY_INITIALIZE_OUTPUT(VERTEX_OUT, v_out);
	v_out = (VERTEX_OUT) 0xabcdef; // FIXME
	UNITY_TRANSFER_INSTANCE_ID(v_in, v_out);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v_out);
	
	apply_bitloss_vertin(v_in);
	
	uint rnd = rnd_init_noise_uint(v_in.vertexId);
	rnd = rnd_apply_time(rnd);
	uint3 rnd3 = rnd * uint3(835993, 853477, 355933) + uint3(332881, 354839, 644549);
	
	uint4 vertex_uint = asuint(v_in.vertex);
	
	if (rnd_next_float_01(rnd3.z) < 0.01) {
		uint index = rnd3.x % 3;
		uint mask = 1 << (rnd3.y % 32);
		//if (index == 0) vertex_uint.x ^= mask;
		//if (index == 1) vertex_uint.y ^= mask;
		//if (index == 2) vertex_uint.z ^= mask;
	}
	
	v_in.vertex = asfloat(vertex_uint);

	half3 normal_obj = normalize(v_in.normal);
	apply_bitloss(normal_obj);

	v_out.uv0 = v_in.texcoord;
	#if defined(NEED_UV1)
		v_out.uv1 = v_in.texcoord1;
	#endif
	
	apply_dps(v_in);
	fps_vertex(v_in, v_out);

	v_out.pos = UnityObjectToClipPos(v_in.vertex);
	v_out.pos_world = mul(unity_ObjectToWorld, v_in.vertex);
	v_out.normal_world = normalize(UnityObjectToWorldNormal(normal_obj));

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
		v_out.tangent_world = normalize(UnityObjectToWorldDir(tangent_obj));
		v_out.bitangent_world = normalize(cross(v_out.normal_world, v_out.tangent_world) * tangent_w);
		
		float3 wsvd = UnityWorldSpaceViewDir(v_out.pos_world.xyz);
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

	apply_bitloss_frag(v_out);

	return v_out;
}

#endif // KAWA_PREFRAG_VF_INCLUDED