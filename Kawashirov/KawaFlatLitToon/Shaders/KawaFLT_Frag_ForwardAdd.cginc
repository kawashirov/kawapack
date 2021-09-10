#ifndef KAWAFLT_FRAG_FORWARD_ADD_INCLUDED
#define KAWAFLT_FRAG_FORWARD_ADD_INCLUDED

#if !defined(KAWAFLT_PASS_FORWARDADD)
	#error KAWAFLT_PASS_FORWARDADD not defined, but KAWAFLT_STAGE_FRAGMENT_FORWARD_ADD included. 
#endif

#include ".\KawaFLT_Frag_Shared.cginc"

#include ".\kawa_feature_matcap.cginc"

#include ".\kawa_shading_cubedparadox.cginc"
#include ".\kawa_shading_kawaflt_log.cginc"
#include ".\kawa_shading_kawaflt_ramp.cginc"
#include ".\kawa_shading_kawaflt_single.cginc"

/* General fragment function */
half4 frag_forwardadd(FRAGMENT_IN i) : COLOR {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	frag_cull(i);
	fps_frag(i);

	float2 texST = frag_applyst(i.uv0);
	
	uint rnd4_sc = frag_rnd_init(i);
	dstfd_frag_clip(i, rnd4_sc);
	
	half3 normal3 = frag_forward_get_normal(i, texST);
	half4 albedo = frag_forward_get_albedo(i, texST, rnd4_sc);
	half originalA = albedo.a;
	
	frag_alphatest(i, rnd4_sc, albedo.a);

	albedo.rgb = matcap_apply(i, albedo.rgb);
	
	half4 finalColor;
	finalColor.a = albedo.a;
	#if defined(SHADE_CUBEDPARADOXFLT)
		finalColor.rgb = frag_shade_cbdprdx_forward_add(i, albedo.rgb, normal3);
	#endif
	#if defined(SHADE_KAWAFLT_LOG)
		finalColor.rgb = frag_shade_kawaflt_log_forward_add(i, albedo.rgb, normal3);
	#endif
	#if defined(SHADE_KAWAFLT_RAMP)
		finalColor.rgb = frag_shade_kawaflt_ramp_forward_add(i, albedo.rgb, normal3);
	#endif
	#if defined(SHADE_KAWAFLT_SINGLE)
		finalColor.rgb = frag_shade_kawaflt_single_forward_add(i, albedo.rgb, normal3);
	#endif
	
	UNITY_APPLY_FOG(i.fogCoord, finalColor);
	return finalColor;
}

#endif // KAWAFLT_FRAG_FORWARD_ADD_INCLUDED
