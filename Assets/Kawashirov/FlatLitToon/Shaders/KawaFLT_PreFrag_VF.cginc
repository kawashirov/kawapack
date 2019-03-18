#ifndef KAWAFLT_PREFRAG_VF_INCLUDED
#define KAWAFLT_PREFRAG_VF_INCLUDED

#include ".\KawaFLT_Struct_VF.cginc"
#include ".\KawaFLT_Features_Lightweight.cginc"

#include "UnityInstancing.cginc"
#include "KawaRND.cginc"

VERTEX_OUT vert(appdata_full v) {
	UNITY_SETUP_INSTANCE_ID(v);
	VERTEX_OUT o;
	UNITY_INITIALIZE_OUTPUT(VERTEX_OUT, o);
	UNITY_TRANSFER_INSTANCE_ID(v, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	o.uv0 = v.texcoord;
	
	fps_vertex(v, o);

	o.pos = UnityObjectToClipPos(v.vertex); 

	o.posWorld = mul(unity_ObjectToWorld, v.vertex);

	screencoords_fragment_in(o);
	
	#if defined(KAWAFLT_PASS_FORWARD)
		o.uv1 = v.texcoord1;
		o.normalDir = normalize(UnityObjectToWorldNormal(v.normal));
		o.tangentDir = normalize(mul(unity_ObjectToWorld, half4(v.tangent.xyz, 0)).xyz);
		o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
		bool vertexlight = false;
		#if defined(VERTEXLIGHT_ON)
			vertexlight = true;
		#endif
		float3 wsvd = KawaWorldSpaceViewDir(o.posWorld.xyz);
		kawaflt_fragment_in(o, vertexlight, wsvd);
		TRANSFER_SHADOW(o); // v.vertex
		UNITY_TRANSFER_FOG(o, o.pos);
	#endif
	
	#if defined(KAWAFLT_PASS_SHADOWCASTER)
		TRANSFER_SHADOW_CASTER_NOPOS(o, o.pos); // v.vertex, v.normal
	#endif
	
	dstfade_frament_in(o);

	return o;
}

#endif // KAWAFLT_PREFRAG_VF_INCLUDED