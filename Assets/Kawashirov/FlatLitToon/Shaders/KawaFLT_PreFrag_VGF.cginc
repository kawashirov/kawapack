#ifndef KAWAFLT_VERT_GEOMETRY_INCLUDED
#define KAWAFLT_VERT_GEOMETRY_INCLUDED

#include ".\KawaFLT_Struct_VGF.cginc"
#include "Tessellation.cginc"
#include "UnityInstancing.cginc"
#include "KawaRND.cginc"
#include ".\KawaFLT_Features_Lightweight.cginc"
#include ".\KawaFLT_Features_Geometry.cginc"
#include ".\KawaFLT_PreFrag_Shared.cginc"

/* General */

VERTEX_OUT vert(appdata_full v_in) {
	UNITY_SETUP_INSTANCE_ID(v_in);
	VERTEX_OUT v_out;
	// UNITY_INITIALIZE_OUTPUT(VERTEX_OUT, v_out);
	UNITY_TRANSFER_INSTANCE_ID(v_in, v_out);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v_out);
	
	v_out.uv0 = v_in.texcoord;
	v_out.vertex = v_in.vertex;
	v_out.normal_obj = normalize(v_in.normal);
	
	#if defined(KAWAFLT_PASS_FORWARD)
		v_out.uv1 = v_in.texcoord1;

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
		UNITY_INITIALIZE_OUTPUT(GEOMETRY_OUT, v_out[i1]);
		UNITY_TRANSFER_INSTANCE_ID(v_in[i1], v_out[i1]);
		UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(v_in[i1], v_out[i1]);
	}

	bool is_outline = g_id == 1;

	#if defined(NEED_CULL)
		// Удаление треугольника, если все вертексы к удалению
		if (v_in[0].cull && v_in[1].cull && v_in[2].cull) return;
	#endif

	//uint p_id = 1;
	uint rnd_tri = rnd_init_noise_uint(p_id);
	rnd_tri = rnd_apply_uint(rnd_tri, p_id);

	bool drop_face = false;
	// (v_in.vertex, rnd) -> (v_in.vertex, v_out.iwd_tint, rnd, drop_face)
	iwd_geometry(v_in, v_out, rnd_tri, drop_face); 
	if (drop_face) return;

	// Модификация vertex в обджект-спейсе завершена, можно начинать работу в ворлд-спейсе
	v_out[0].pos_world = mul(unity_ObjectToWorld, v_in[0].vertex);
	v_out[1].pos_world = mul(unity_ObjectToWorld, v_in[1].vertex);
	v_out[2].pos_world = mul(unity_ObjectToWorld, v_in[2].vertex);

	// После смещающих модов можно сделать проверку на вылет за экран для оптимиации
	if (UnityWorldViewFrustumCull(v_out[0].pos_world, v_out[1].pos_world, v_out[2].pos_world, 0.0)) return;

	UNITY_UNROLL for (int i2 = 0; i2 < 3; i2++) {
		v_out[i2].normal_world = normalize(UnityObjectToWorldNormal(v_in[i2].normal_obj));

		// Деформация для аутлайна
		// (v_out.pos_world, v_out.normal_world) -> (v_out.pos_world, v_out.normal_world, v_out.is_outline)
		outline_geometry_apply_offset(v_out[i2], is_outline);

		// Модификация в ворлд-спейсе завершена, можно обсчитывать клип-спейс и прочее

		v_out[i2].uv0 = v_in[i2].uv0;
		v_out[i2].pos = UnityWorldToClipPos(v_out[i2].pos_world);

		#if defined(KAWAFLT_PASS_FORWARD)
			v_out[i2].uv1 = v_in[i2].uv1;

			v_out[i2].vertex = v_in[i2].vertex;
			v_out[i2].tangent_world = normalize(UnityObjectToWorldDir(v_in[i2].tangent_obj));
			v_out[i2].bitangent_world = normalize(UnityObjectToWorldDir(v_in[i2].bitangent_obj));

			#if defined(KAWAFLT_F_MATCAP_ON)
				half3x3 tangent_obj_basis = half3x3(v_in[i2].tangent_obj, v_in[i2].bitangent_obj, v_in[i2].normal_obj);
				v_out[i2].matcap_x = mul(tangent_obj_basis, half3(UNITY_MATRIX_IT_MV[0].xyz));
				v_out[i2].matcap_y = mul(tangent_obj_basis, half3(UNITY_MATRIX_IT_MV[1].xyz));
			#endif

			bool vertexlight_on = false;
			#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
				vertexlight_on = v_in[i2].vertexlight_on;
			#endif
			float3 wsvd = KawaWorldSpaceViewDir(v_out[i2].pos_world.xyz);
			kawaflt_fragment_in(v_out[i2], vertexlight_on, wsvd);

			// (vertex_obj, v_out.pos) -> (v_out._ShadowCoord)
			prefrag_transfer_shadow(v_in[i2].vertex, v_out[i2]);
			UNITY_TRANSFER_FOG(v_out[i2], v_out[i2].pos);
		#endif

		// (v_out.pos) -> (v_out.pos_screen)
		screencoords_fragment_in(v_out[i2]);
		// (v_out.pos_world) -> (v_out.dstfd_distance)
		dstfade_frament_in(v_out[i2]);

		prefrag_shadowcaster_pos(v_in[i2].vertex, v_in[i2].normal_obj, v_out[i2].pos);
	}

	pcw_geometry_out(v_out, rnd_tri);

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