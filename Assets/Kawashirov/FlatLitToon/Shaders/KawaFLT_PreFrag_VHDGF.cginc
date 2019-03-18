#ifndef KAWAFLT_PREFRAG_VHDGF_INCLUDED
#define KAWAFLT_PREFRAG_VHDGF_INCLUDED

#include ".\KawaFLT_Struct_VHDGF.cginc"
#include ".\KawaFLT_Features_Lightweight.cginc"
#include ".\KawaFLT_Features_Geometry.cginc"
#include ".\KawaFLT_Features_Tessellation.cginc"

#include "Tessellation.cginc"
#include "UnityInstancing.cginc"
#include "KawaRND.cginc"

#define PIPELINE_VHDGF 1

/* General */

VERTEX_OUT vert(appdata_full v) {
	UNITY_SETUP_INSTANCE_ID(v);
	VERTEX_OUT o;
	// UNITY_INITIALIZE_OUTPUT(VERTEX_OUT, o);
	UNITY_TRANSFER_INSTANCE_ID(v, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	//uint rnd = rnd_from_float2(v.texcoord);
	
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

struct TessellationFactors {
	float edge[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

TessellationFactors hullconst(InputPatch<HULL_IN,3> v) {
	bool cull = false;
	#if defined(NEED_CULL)
		cull = v[0].cull && v[1].cull && v[2].cull;
	#endif

	TessellationFactors o;

	if (cull) {
		o.inside = o.edge[0] = o.edge[1] = o.edge[2] = 0.0;
	} else {
		unorm float3 dots;
		dots.x = saturate(0.5 - dot(v[1].normal, v[2].normal) * 0.5);
		dots.y = saturate(0.5 - dot(v[2].normal, v[0].normal) * 0.5);
		dots.z = saturate(0.5 - dot(v[0].normal, v[1].normal) * 0.5);
		dots = sqrt(dots); // TODO Ускорить?

		o.edge[0] = max(0.1, _Tsltn_Uni + _Tsltn_Nrm * dots.x);
		o.edge[1] = max(0.1, _Tsltn_Uni + _Tsltn_Nrm * dots.y);
		o.edge[2] = max(0.1, _Tsltn_Uni + _Tsltn_Nrm * dots.z);

		dsntgrt_hullconst(o.edge, v[0], v[1], v[2]);

		// o.edge[0] += 2;
		// o.edge[1] += 2;
		// o.edge[2] += 2;

		o.inside = (o.edge[0] + o.edge[1] + o.edge[2]) / 3.0 * _Tsltn_Inside;
	}
	return o;
}

// [partitioning("integer")]
#if defined(TESS_D_QUAD)
	[domain("quad")]
#else
	[domain("tri")]
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
	#error TESS_P_???
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
#else
	[domain("tri")]
#endif
DOMAIN_OUT domain (TessellationFactors tessFactors, const OutputPatch<DOMAIN_IN,3> vi, float3 bary : SV_DomainLocation) {
	DOMAIN_OUT o;

	o.uv0 = DOMAIN_INTERPOLATE_3D(vi, uv0, bary);
	//o.vertex = DOMAIN_INTERPOLATE_3D(vi, vertex, bary);
	o.normal = normalize(DOMAIN_INTERPOLATE_3D(vi, normal, bary));

	#if defined(KAWAFLT_PASS_FORWARD)
		o.uv1 = DOMAIN_INTERPOLATE_3D(vi, uv1, bary);
		o.normalDir = normalize(DOMAIN_INTERPOLATE_3D(vi, normalDir, bary));
		o.tangentDir = normalize(DOMAIN_INTERPOLATE_3D(vi, tangentDir, bary));
		o.bitangentDir = normalize(DOMAIN_INTERPOLATE_3D(vi, bitangentDir, bary));
		#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
			o.vertexlight_on = vi[0].vertexlight_on;
		#endif
	#endif

	// Phong
	o.vertex = DOMAIN_INTERPOLATE_3D(vi, vertex, bary);
	// float3 proj_0 = dot(vi[0].vertex - o.vertex.xyz, vi[0].normal) * vi[0].normal;
	// float3 proj_1 = dot(vi[1].vertex - o.vertex.xyz, vi[1].normal) * vi[1].normal;
	// float3 proj_2 = dot(vi[2].vertex - o.vertex.xyz, vi[2].normal) * vi[2].normal;
	// float3 vecOffset = bary.x * proj_0 + bary.y * proj_1 + bary.z * proj_2;
	// o.vertex.xyz += 0.6666 * vecOffset;

	// Spherize
	// o.vertex = DOMAIN_INTERPOLATE_3D(vi, vertex, bary);
	// o.vertex.xyz = normalize(o.vertex.xyz) * ( length(vi[0].vertex.xyz) * bary.x + length(vi[1].vertex.xyz) * bary.y + length(vi[2].vertex.xyz) * bary.z );
	// // o.vertex.xyz = normalize(o.vertex.xyz)* ( length(vi[0].vertex.xyz) + length(vi[1].vertex.xyz) + length(vi[2].vertex.xyz) ) / 3.0;


	#if defined(NEED_CULL)
		o.cull = vi[0].cull && vi[1].cull && vi[2].cull;
	#endif

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

	/**/

	uint rnd_tri = rnd_from_float2x3(IN[0].uv0, IN[1].uv0, IN[2].uv0); // TODO
	// IN: (vertex) -> (vertex); OUT: () -> (dsntgrtVertexRotated)
	dsntgrt_geometry(IN, OUT, rnd_tri, dropFace); 

	if (dropFace) return;

	/* Transforms finished here */
	
	// FrustumCull
	OUT[0].posWorld = mul(unity_ObjectToWorld, IN[0].vertex);
	OUT[1].posWorld = mul(unity_ObjectToWorld, IN[1].vertex);
	OUT[2].posWorld = mul(unity_ObjectToWorld, IN[2].vertex);
	dropFace = dropFace || UnityWorldViewFrustumCull(OUT[0].posWorld, OUT[1].posWorld, OUT[2].posWorld, 0.0);
	if (dropFace) return;
	
	// Prepare main triangle export
	UNITY_UNROLL for (int i2 = 0; i2 < 3; i2++) {
		// Pass-through
		OUT[i2].uv0 = IN[i2].uv0;
		#if defined(NEED_CULL)
			OUT[i2].cull = IN[i2].cull;
		#endif
		#if defined(KAWAFLT_PASS_FORWARD)
			OUT[i2].uv1 = IN[i2].uv1;
			// OUT[i].normal = IN[i].normal;
			OUT[i2].vertex = IN[i2].vertex;
			OUT[i2].normalDir = IN[i2].normalDir;
			OUT[i2].tangentDir = IN[i2].tangentDir;
			OUT[i2].bitangentDir = IN[i2].bitangentDir;
		#endif
		
		// Calculate new values
		//UNITY_SETUP_INSTANCE_ID(IN[i2]);
		OUT[i2].pos = UnityObjectToClipPos(IN[i2].vertex); 
		// OUT[i2].posWorld = mul(unity_ObjectToWorld, IN[i2].vertex);
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
			g2f out_tln = OUT[i4];
			half4 offset_normal = half4(IN[i4].normal * (_outline_width * 0.01h), 0);
			half4 offset_bias = half4(normalize(ObjSpaceViewDir(out_tln.vertex)) * (_outline_bias * 0.01h), 0);
			out_tln.is_outline = true;
			out_tln.vertex = IN[i4].vertex + offset_normal - offset_bias;
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


#endif // KAWAFLT_PREFRAG_VHDGF_INCLUDED