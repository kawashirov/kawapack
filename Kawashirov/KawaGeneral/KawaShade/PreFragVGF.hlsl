#ifndef KAWAFLT_VERT_GEOMETRY_INCLUDED
#define KAWAFLT_VERT_GEOMETRY_INCLUDED

VERTEX_OUT vert(VERTEX_IN v_in, in uint v_id : SV_VertexID) {
	UNITY_SETUP_INSTANCE_ID(v_in);
	VERTEX_OUT v_out;
	// UNITY_INITIALIZE_OUTPUT(VERTEX_OUT, v_out);
	UNITY_TRANSFER_INSTANCE_ID(v_in, v_out);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v_out);
	
	uint rnd_tri = v_id; // rnd_init_noise_uint(v_id);
	rnd_init(rnd_tri);
		
	apply_dps(v_in);
	
	v_out.uv0 = v_in.texcoord;
	apply_bitloss2(v_out.uv0, uint2(39736, 51879), uint2(33058, 38227), true);
	#if defined(NEED_UV1)
		v_out.uv1 = v_in.texcoord1;
		apply_bitloss2(v_out.uv1, uint2(55355, 35850), uint2(36711, 50447), true);
	#endif
	
	v_out.vertex = v_in.vertex;
	v_out.normal_obj = normalize(v_in.normal);
	
	#if defined(KAWAFLT_PASS_FORWARD)
		// С большой вероятностью на geom стейдже система изменится и нужно буде
		// пересчитывать o->w, по этому сохраняем тангентное-пространство в координатах меши
		// TODO оптимизировать
		half tangent_w = v_in.tangent.w; // Определяет леворукость/праворукость/зеркальность?
		v_out.tangent_obj = normalize(v_in.tangent.xyz);
		v_out.bitangent_obj = normalize(cross(v_out.normal_obj, v_out.tangent_obj) * tangent_w);

		#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
			v_out.vertexlight_on = false;
			#if defined(VERTEXLIGHT_ON)
				v_out.vertexlight_on = true;
			#endif
		#endif
	#endif
	
	fps_vertex(v_in, v_out);

	return v_out;
}

#if defined(OUTLINE_OFF)
	[maxvertexcount(3)]
	[instance(1)]
#else
	[maxvertexcount(3)]
	[instance(2)]
#endif
void geom(triangle GEOMETRY_IN v_in[3], in uint p_id : SV_PrimitiveID, uint g_id : SV_GSInstanceID, inout TriangleStream<GEOMETRY_OUT> tristream) {
	// This is not correct, but it's should work because every vert of triange should be from one instance.
	UNITY_SETUP_INSTANCE_ID(v_in[0]);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v_in[0]);
	GEOMETRY_OUT v_out[3];
	UNITY_UNROLL for (int i1 = 0; i1 < 3; i1++) {
		UNITY_INITIALIZE_OUTPUT(GEOMETRY_OUT, v_out[i1]); // FIXME
		UNITY_TRANSFER_INSTANCE_ID(v_in[i1], v_out[i1]);
		UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(v_in[i1], v_out[i1]);
	}

	bool is_outline = g_id == 1;

	#if defined(NEED_VERT_CULL)
		// Удаление треугольника, если все вертексы к удалению
		if (v_in[0].cull && v_in[1].cull && v_in[2].cull) return;
	#endif

	//uint p_id = 1;
	uint rnd_tri = rnd_init_noise_uint(p_id + g_id);
	rnd_init(rnd_tri);

	bool drop_face = false;
	// (v_in.vertex) -> (v_in.vertex, v_out.iwd_tint, drop_face)
	iwd_geometry(v_in, v_out, drop_face); 
	if (drop_face) return;

	// Модификация vertex в обджект-спейсе завершена, можно начинать работу в ворлд-спейсе
	UNITY_UNROLL for (int i3 = 0; i3 < 3; i3++) {
		v_out[i3].pos_world = mul(unity_ObjectToWorld, v_in[i3].vertex);
		apply_bitloss4(v_out[i3].pos_world, uint4(48666, 33763, 50100, 57693), uint4(58400, 39726, 63251, 46769), true);
	}

	// После смещающих модов можно сделать проверку на вылет за экран для оптимиации
	if (UnityWorldViewFrustumCull(v_out[0].pos_world, v_out[1].pos_world, v_out[2].pos_world, 0.0)) return;

	UNITY_UNROLL for (int i2 = 0; i2 < 3; i2++) {
		v_out[i2].normal_world = UnityObjectToWorldNormal(v_in[i2].normal_obj);
		apply_bitloss3(v_out[i2].normal_world, uint3(59947, 64539, 61205), uint3(47834, 55784, 51766), true);
		v_out[i2].normal_world = normalize(v_out[i2].normal_world);

		// Деформация для аутлайна
		// (v_out.pos_world, v_out.normal_world) -> (v_out.pos_world, v_out.normal_world, v_out.is_outline)
		outline_geometry_apply_offset(v_out[i2], is_outline);

		// Модификация в ворлд-спейсе завершена, можно обсчитывать клип-спейс и прочее

		v_out[i2].uv0 = v_in[i2].uv0;
		apply_bitloss2(v_out[i2].uv0, uint2(39736, 51879), uint2(33058, 38227), true);
		#if defined(NEED_UV1)
			v_out[i2].uv1 = v_in[i2].uv1;
			apply_bitloss2(v_out[i2].uv1, uint2(55355, 35850), uint2(36711, 50447), true);
		#endif
		
		v_out[i2].pos = UnityWorldToClipPos(v_out[i2].pos_world);
		// apply_bitloss4(v_out[i2].pos, uint4(58839, 57528, 40864, 38292), uint4(60970, 56821, 47165, 54475), true);
		apply_bitloss3(v_out[i2].pos.xyz, uint3(58839, 57528, 40864), uint3(60970, 56821, 47165), true);

		#if defined(KAWAFLT_PASS_FORWARD)
			v_out[i2].vertex = v_in[i2].vertex;
			
			v_out[i2].tangent_world = UnityObjectToWorldDir(v_in[i2].tangent_obj);
			apply_bitloss3(v_out[i2].tangent_world, uint3(44466, 40896, 52619), uint3(62055, 45194, 52148), true);
			v_out[i2].tangent_world = normalize(v_out[i2].tangent_world);
			
			v_out[i2].bitangent_world = UnityObjectToWorldDir(v_in[i2].bitangent_obj);
			apply_bitloss3(v_out[i2].bitangent_world, uint3(61661, 51423, 43258), uint3(43410, 42253, 40283), true);
			v_out[i2].bitangent_world = normalize(v_out[i2].bitangent_world);
			
			float3 wsvd = UnityWorldSpaceViewDir(v_out[i2].pos_world.xyz);
			apply_bitloss3(wsvd, uint3(44380, 44971, 50874), uint3(65110, 35381, 38937), true);
			half3 wsvd_norm = normalize(wsvd);
			
			// (v_out.world_normal) -> (v_out.matcap_uv)
			matcap_calc_uv(v_out[i2], wsvd_norm);

			bool vertexlight_on = false;
			#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
				vertexlight_on = v_in[i2].vertexlight_on;
			#endif
			kawaflt_fragment_in(v_out[i2], vertexlight_on, wsvd);

			// (vertex_obj, v_out.pos) -> (v_out._ShadowCoord)
			prefrag_transfer_shadow(v_in[i2].vertex, v_out[i2]);
			UNITY_TRANSFER_FOG(v_out[i2], v_out[i2].pos);
		#endif
		
		// (v_out.pos) -> (v_out.pos)
		psx_prefrag(v_out[i2]);
		// (v_out.pos) -> (v_out.pos_screen)
		screencoords_fragment_in(v_out[i2]);
		// (v_out.pos_world) -> (v_out.dstfd_distance)
		dstfade_frament_in(v_out[i2]);

		prefrag_shadowcaster_pos(v_in[i2].vertex, v_in[i2].normal_obj, v_out[i2].pos);
	}

	pcw_geometry_out(v_out);

	if (is_outline) {
		// Обратный порядок
		tristream.Append(v_out[2]);
		tristream.Append(v_out[1]);
		tristream.Append(v_out[0]);
	} else {
		// Прямой порядок
		tristream.Append(v_out[0]);
		tristream.Append(v_out[1]);
		tristream.Append(v_out[2]);
	}

	tristream.RestartStrip();
}


#endif // KAWAFLT_VERT_GEOMETRY_INCLUDED