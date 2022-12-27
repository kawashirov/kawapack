#ifndef KAWA_PREFRAG_VHDGF_INCLUDED
#define KAWA_PREFRAG_VHDGF_INCLUDED

#include ".\kawa_struct_vhdgf.cginc"

#include "Tessellation.cginc"
#include "UnityInstancing.cginc"
#include ".\kawa_rnd.cginc"

#include ".\kawa_prefrag_shared.cginc"

#include ".\kawa_feature_dps.cginc"
#include ".\kawa_feature_distance_fade.cginc"
#include ".\kawa_feature_fps.cginc"
#include ".\kawa_feature_matcap.cginc"
#include ".\kawa_feature_psx.cginc"

#include ".\kawa_feature_infinity_war.cginc"
#include ".\kawa_feature_poly_color_wave.cginc"
#include ".\kawa_feature_outline.cginc"

#include ".\kawa_shading_kawaflt_fragment_in.cginc"

/* General */

VERTEX_OUT vert(VERTEX_IN v_in) {
	UNITY_SETUP_INSTANCE_ID(v_in);
	VERTEX_OUT v_out;
	// UNITY_INITIALIZE_OUTPUT(VERTEX_OUT, v_out);
	UNITY_TRANSFER_INSTANCE_ID(v_in, v_out);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v_out);

	apply_dps(v_in);
	
	//uint rnd = rnd_from_float2(v_in.texcoord);
	
	v_out.uv0 = v_in.texcoord;
	#if defined(NEED_UV1)
		v_out.uv1 = v_in.texcoord1;
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

struct TessellationFactors {
	float edge[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

TessellationFactors hullconst(InputPatch<HULL_IN,3> v_in) {
	bool cull = false;
	#if defined(NEED_CULL)
		cull = v_in[0].cull && v_in[1].cull && v_in[2].cull;
	#endif

	TessellationFactors v_out;

	if (cull) {
		v_out.inside = v_out.edge[0] = v_out.edge[1] = v_out.edge[2] = 0.0;
	} else {
		unorm float3 dots;
		dots.x = saturate(0.5 - dot(v_in[1].normal_obj, v_in[2].normal_obj) * 0.5);
		dots.y = saturate(0.5 - dot(v_in[2].normal_obj, v_in[0].normal_obj) * 0.5);
		dots.z = saturate(0.5 - dot(v_in[0].normal_obj, v_in[1].normal_obj) * 0.5);
		dots = sqrt(dots); // TODO Ускорить?

		v_out.edge[0] = max(0.1, _Tsltn_Uni + _Tsltn_Nrm * dots.x);
		v_out.edge[1] = max(0.1, _Tsltn_Uni + _Tsltn_Nrm * dots.y);
		v_out.edge[2] = max(0.1, _Tsltn_Uni + _Tsltn_Nrm * dots.z);

		iwd_hullconst(v_out.edge, v_in[0], v_in[1], v_in[2]);

		// v_out.edge[0] += 2;
		// v_out.edge[1] += 2;
		// v_out.edge[2] += 2;

		v_out.inside = (v_out.edge[0] + v_out.edge[1] + v_out.edge[2]) / 3.0 * _Tsltn_Inside;
	}
	return v_out;
}

#if defined(TESS_D_QUAD)
	[domain("quad")]
#elif defined(TESS_D_TRI)
	[domain("tri")]
#else
	#error "TESS_D_???"
#endif

#if defined(TESS_P_INT)
	[partitioning("integer")]
#elif defined(TESS_P_EVEN)
	[partitioning("fractional_even")]
#elif defined(TESS_P_ODD)
	[partitioning("fractional_odd")]
#elif defined(TESS_P_POW2)
	[partitioning("pow2")]
#else
	#error "TESS_P_???"
#endif

[outputtopology("triangle_cw")]
[patchconstantfunc("hullconst")]
[outputcontrolpoints(3)]
HULL_OUT hull (InputPatch<HULL_IN, 3> v, uint id : SV_OutputControlPointID) {
	return v[id];
}

#define DOMAIN_INTERPOLATE_3D(patch, fieldName, bary) ((patch)[0].fieldName * (bary).x + (patch)[1].fieldName * (bary).y + (patch)[2].fieldName * (bary).z)


#if defined(TESS_D_QUAD)
	[domain("quad")]
#elif defined(TESS_D_TRI)
	[domain("tri")]
#else
	#error "TESS_D_???"
#endif
DOMAIN_OUT domain (TessellationFactors tessFactors, const OutputPatch<DOMAIN_IN,3> v_in, float3 bary : SV_DomainLocation) {
	DOMAIN_OUT v_out;

	v_out.uv0 = DOMAIN_INTERPOLATE_3D(v_in, uv0, bary);
	#if defined(NEED_UV1)
		v_out.uv1 = DOMAIN_INTERPOLATE_3D(v_in, uv1, bary);
	#endif
	v_out.normal_obj = normalize(DOMAIN_INTERPOLATE_3D(v_in, normal_obj, bary));

	#if defined(KAWAFLT_PASS_FORWARD)
		v_out.tangent_obj = normalize(DOMAIN_INTERPOLATE_3D(v_in, tangent_obj, bary));
		v_out.bitangent_obj = normalize(DOMAIN_INTERPOLATE_3D(v_in, bitangent_obj, bary));
		#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
			v_out.vertexlight_on = v_in[0].vertexlight_on;
		#endif
	#endif

	// Phong
	v_out.vertex = DOMAIN_INTERPOLATE_3D(v_in, vertex, bary);
	// float3 proj_0 = dot(v_in[0].vertex - v_out.vertex.xyz, v_in[0].normal) * v_in[0].normal;
	// float3 proj_1 = dot(v_in[1].vertex - v_out.vertex.xyz, v_in[1].normal) * v_in[1].normal;
	// float3 proj_2 = dot(v_in[2].vertex - v_out.vertex.xyz, v_in[2].normal) * v_in[2].normal;
	// float3 vecOffset = bary.x * proj_0 + bary.y * proj_1 + bary.z * proj_2;
	// v_out.vertex.xyz += 0.6666 * vecOffset;

	// Spherize
	// v_out.vertex = DOMAIN_INTERPOLATE_3D(v_in, vertex, bary);
	// v_out.vertex.xyz = normalize(v_out.vertex.xyz) * ( length(v_in[0].vertex.xyz) * bary.x + length(v_in[1].vertex.xyz) * bary.y + length(v_in[2].vertex.xyz) * bary.z );
	// // v_out.vertex.xyz = normalize(v_out.vertex.xyz)* ( length(v_in[0].vertex.xyz) + length(v_in[1].vertex.xyz) + length(v_in[2].vertex.xyz) ) / 3.0;


	#if defined(NEED_CULL)
		v_out.cull = v_in[0].cull && v_in[1].cull && v_in[2].cull;
	#endif

	return v_out;
}


#if defined(OUTLINE_OFF)
	[maxvertexcount(3)]
	[instance(1)]
#else
	[maxvertexcount(3)]
	[instance(2)]
#endif
void geom(triangle GEOMETRY_IN v_in[3], uint p_id : SV_PrimitiveID, uint g_id : SV_GSInstanceID, inout TriangleStream<GEOMETRY_OUT> tristream) {
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

	#if defined(NEED_CULL)
		// Удаление треугольника, если все вертексы к удалению
		if (v_in[0].cull && v_in[1].cull && v_in[2].cull) return;
	#endif

	//uint p_id = 1;
	uint rnd_tri = rnd_init_noise_uint(p_id);
	rnd_tri = rnd_apply_uint(rnd_tri, p_id);
	// FIXME temporary salt with vtx ids for randomness in tessellated sub-primitives
	// FIMME a lot of instructions used here, ouff
	rnd_tri = rnd_apply_uint2(rnd_tri, asuint(v_in[0].uv0));
	rnd_tri = rnd_apply_uint2(rnd_tri, asuint(v_in[1].uv0));
	rnd_tri = rnd_apply_uint2(rnd_tri, asuint(v_in[1].uv0));

	bool drop_face = false;
	// (v_in[i].vertex, rnd) -> (v_in[i].vertex, v_out[i].iwd_tint, rnd, drop_face)
	iwd_geometry(v_in, v_out, rnd_tri, drop_face); 
	if (drop_face) return;
	
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
		#if defined(NEED_UV1)
			v_out[i2].uv1 = v_in[i2].uv1;
		#endif
		v_out[i2].pos = UnityWorldToClipPos(v_out[i2].pos_world);

		#if defined(KAWAFLT_PASS_FORWARD)
			v_out[i2].vertex = v_in[i2].vertex;
			v_out[i2].tangent_world = normalize(UnityObjectToWorldDir(v_in[i2].tangent_obj));
			v_out[i2].bitangent_world = normalize(UnityObjectToWorldDir(v_in[i2].bitangent_obj));
			
			// (v_out.world_normal) -> (v_out.matcap_uv)
			matcap_calc_uv(v_out[i2]);

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

		// (v_out.pos) -> (v_out.pos)
		psx_prefrag(v_out[i2]);
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


#endif // KAWA_PREFRAG_VHDGF_INCLUDED