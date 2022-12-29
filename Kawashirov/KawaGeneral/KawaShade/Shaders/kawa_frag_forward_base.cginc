#ifndef KAWAFLT_FRAG_FORWARD_BASE_INCLUDED
#define KAWAFLT_FRAG_FORWARD_BASE_INCLUDED

#if !defined(KAWAFLT_PASS_FORWARDBASE)
	#error KAWAFLT_PASS_FORWARDBASE not defined, but KAWAFLT_STAGE_FRAGMENT_FORWARD_BASE included. 
#endif

// #include "UnityInstancing.cginc"
// #include "UnityStandardUtils.cginc"

/* ForwardBase only utils */

inline half3 frag_forward_get_emission_color(inout FRAGMENT_IN i, half3 baseColor, float2 texST, inout uint rnd) {
	half3 em = half3(0,0,0);
	#if defined(EMISSION_ALBEDO_NOMASK)
		em = baseColor;
	#elif defined(EMISSION_ALBEDO_MASK)
		em = baseColor * UNITY_SAMPLE_TEX2D(_EmissionMask, texST).r;
	#elif defined(EMISSION_CUSTOM)
		em = UNITY_SAMPLE_TEX2D(_EmissionMap, texST).rgb;
	#endif
	#if defined(EMISSION_ON)
		em = em * _EmissionColor.rgb * _EmissionColor.a;
		em = em * em; // TODO FIXME Gamma fix?
	#endif
	em = wnoise_mix(em, i, true, rnd);
	em = fps_mix(half4(em, 0)).rgb;
	em = pcw_mix(em, i, true); // Mix-in Poly Color Wave
	em = iwd_mix_emission(em, i);
	return em;
}


/* General fragment function */
half4 frag_forwardbase(FRAGMENT_IN i) : COLOR {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	frag_cull(i);
	fps_frag(i);

	float2 texST = frag_applyst(i.uv0);

	uint rnd4_sc = frag_rnd_init(i);
	uint rnd = rnd4_sc;
	
	dstfd_frag_clip(i, rnd4_sc);

	half3 normal3 = frag_forward_get_normal(i, texST);
	half4 albedo = frag_forward_get_albedo(i, texST, rnd4_sc);
	half3 emissive = frag_forward_get_emission_color(i, albedo, texST, rnd4_sc);
	
	frag_alphatest(i, rnd4_sc, albedo.a);

	albedo.rgb = matcap_apply(i, albedo.rgb);
	
	apply_glitter(albedo.rgb, emissive, texST, rnd);
	
	half4 finalColor;
	finalColor.a = albedo.a;
	#if defined(SHADE_CUBEDPARADOXFLT)
		finalColor.rgb = frag_shade_cbdprdx_forward_base(i, albedo.rgb, normal3, emissive);
	#endif
	#if defined(SHADE_KAWAFLT_LOG)
		finalColor.rgb = frag_shade_kawaflt_log_forward_base(i, albedo.rgb, normal3, emissive);
	#endif
	#if defined(SHADE_KAWAFLT_RAMP)
		finalColor.rgb = frag_shade_kawaflt_ramp_forward_base(i, albedo.rgb, normal3, emissive);
	#endif
	#if defined(SHADE_KAWAFLT_SINGLE)
		finalColor.rgb = frag_shade_kawaflt_single_forward_base(i, albedo.rgb, normal3, emissive);
	#endif
	
	UNITY_APPLY_FOG(i.fogCoord, finalColor);
		
	return finalColor;
}

#endif // KAWAFLT_FRAG_FORWARD_BASE_INCLUDED
