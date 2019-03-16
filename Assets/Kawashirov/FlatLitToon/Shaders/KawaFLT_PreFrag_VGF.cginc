#ifndef KAWAFLT_VERT_GEOMETRY_INCLUDED
#define KAWAFLT_VERT_GEOMETRY_INCLUDED

#include ".\KawaFLT_Struct_VGF.cginc"
#include ".\KawaFLT_Features_Lightweight.cginc"
#include ".\KawaFLT_Features_Geometry.cginc"

#include "UnityInstancing.cginc"
#include "KawaRND.cginc"

/* General */

VERTEX_OUT vert(appdata_full v) {
	UNITY_SETUP_INSTANCE_ID(v);
	VERTEX_OUT o;
	// UNITY_INITIALIZE_OUTPUT(VERTEX_OUT, o);
	UNITY_TRANSFER_INSTANCE_ID(v, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	
	o.uv0 = v.texcoord;
	o.vertex = v.vertex;
	o.normal = v.normal;
	
	#if defined(KAWAFLT_PASS_FORWARD)
		o.uv1 = v.texcoord1;
		//o.tangent = v.tangent;
		// Псевдоконстантные значения
		o.normalDir = normalize(UnityObjectToWorldNormal(v.normal));
		o.tangentDir = normalize(mul(unity_ObjectToWorld, half4(v.tangent.xyz, 0)).xyz);
		o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
		#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
			o.vertexlight_on = false;
			#if defined(VERTEXLIGHT_ON)
				o.vertexlight_on = true;
			#endif
		#endif
	#endif
	
	fps_vertex(v, o);

	return o;
}

#if defined(NO_OUTLINE)
	[maxvertexcount(3)]
#else
	[maxvertexcount(6)]
#endif
void geom(triangle GEOMETRY_IN IN[3], inout TriangleStream<GEOMETRY_OUT> tristream) {
	// This is not correct, but it's should work because every vert of triange should be from one instance.
	UNITY_SETUP_INSTANCE_ID(IN[0]);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN[0]);
	GEOMETRY_OUT OUT[3];
	UNITY_UNROLL for (int i1 = 0; i1 < 3; i1++) {
		// UNITY_INITIALIZE_OUTPUT(GEOMETRY_OUT, OUT[i1]);
		UNITY_TRANSFER_INSTANCE_ID(IN[i1], OUT[i1]);
		UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(IN[i1], OUT[i1]);
	}

	bool dropFace = false;
	#if defined(NEED_CULL)
		// Удаление треугольника, если все вертексы к удалению
		dropFace = IN[0].cull && IN[1].cull && IN[2].cull
	#endif

	if (dropFace) return;

	uint rnd_tri = rnd_from_float2x3(IN[0].uv0, IN[1].uv0, IN[2].uv0);

	// IN: (vertex) -> (vertex); OUT: () -> (dsntgrtVertexRotated)
	dsntgrt_geometry(IN, OUT, rnd_tri, dropFace); 

	if (dropFace) return;
	
	// Prepare main triangle export
	UNITY_UNROLL for (int i2 = 0; i2 < 3; i2++) {
		// Pass-through
		OUT[i2].uv0 = IN[i2].uv0;
		#if defined(KAWAFLT_PASS_FORWARD)
			OUT[i2].uv1 = IN[i2].uv1;
			// OUT[i].normal = IN[i].normal;
			OUT[i2].vertex = IN[i2].vertex;
			OUT[i2].normalDir = IN[i2].normalDir;
			OUT[i2].tangentDir = IN[i2].tangentDir;
			OUT[i2].bitangentDir = IN[i2].bitangentDir;
		#endif
		
		// Calculate new values
		// UNITY_SETUP_INSTANCE_ID(IN[i2]);
		OUT[i2].pos = UnityObjectToClipPos(IN[i2].vertex); 
		OUT[i2].posWorld = mul(unity_ObjectToWorld, IN[i2].vertex);
		screencoords_fragment_in(OUT[i2]);
		#if defined(KAWAFLT_PASS_FORWARD)
			bool vertexlight_on = false;
			#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
				vertexlight_on = IN[i2].vertexlight_on;
			#endif
			float3 wsvd = KawaWorldSpaceViewDir(OUT[i2].posWorld.xyz);
			kawaflt_fragment_in(OUT[i2], vertexlight_on, wsvd);
			UNITY_TRANSFER_FOG(OUT[i2], OUT[i2].pos);
			#if defined(TINTED_OUTLINE) || defined(COLORED_OUTLINE)
				OUT[i2].is_outline = false;
			#endif
		#endif

		geom_proxy_calc_shadow(IN[i2], OUT[i2]);
		geom_proxy_shadowcaster_nopos(IN[i2], OUT[i2]);

		dstfade_frament_in(OUT[i2]);
		//#endif
	}

	pcw_geometry_out(OUT, rnd_tri);

	UNITY_UNROLL for (int i3 = 0; i3 < 3; i3++) {
		tristream.Append(OUT[i3]);
	}

	tristream.RestartStrip();
	
	#if defined(KAWAFLT_PASS_FORWARD) && (defined(TINTED_OUTLINE) || defined(COLORED_OUTLINE))
		// Loop in reversed order
		UNITY_UNROLL for (int i4 = 2; i4 >= 0; i4--) {
			// Copy and rewrite for outline
			GEOMETRY_OUT out_tln = OUT[i4];
			half3 offset_normal = IN[i4].normal * (_outline_width * 0.01h);
			half3 offset_bias = UnityWorldToObjectDir(KawaWorldSpaceViewDir(out_tln.posWorld));
			offset_bias = normalize(offset_bias) * (_outline_bias * 0.01h);
			out_tln.is_outline = true; 
			out_tln.vertex = IN[i4].vertex + half4(offset_normal - offset_bias, 0);
			// Recalculate dependent
			out_tln.pos = UnityObjectToClipPos(out_tln.vertex);
			out_tln.posWorld = mul(unity_ObjectToWorld, out_tln.vertex);
			out_tln.normalDir = -out_tln.normalDir;
			UNITY_TRANSFER_FOG(out_tln, out_tln.pos);
			// Other values should stay the same
			tristream.Append(out_tln);
		}
		tristream.RestartStrip(); 
	#endif
}


#endif // KAWAFLT_VERT_GEOMETRY_INCLUDED