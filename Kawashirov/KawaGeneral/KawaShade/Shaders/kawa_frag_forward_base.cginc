#ifndef KAWAFLT_FRAG_FORWARD_BASE_INCLUDED
#define KAWAFLT_FRAG_FORWARD_BASE_INCLUDED

#if !defined(KAWAFLT_PASS_FORWARDBASE)
	#error KAWAFLT_PASS_FORWARDBASE not defined, but KAWAFLT_STAGE_FRAGMENT_FORWARD_BASE included. 
#endif

// #include "UnityInstancing.cginc"
// #include "UnityStandardUtils.cginc"

/* ForwardBase only utils */

inline half3 frag_forward_get_emission_color(inout FRAGMENT_IN i, half3 baseColor, float2 texST) {
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
	return em;
}


/* General fragment function */
half4 frag_forwardbase(FRAGMENT_IN i) : COLOR {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	frag_cull(i);

	float2 texST = frag_applyst(i.uv0);
	fps_apply_uv(texST);

	uint rnd = frag_rnd_init(i);
	
	float3 wsvd = UnityWorldSpaceViewDir(i.pos_world.xyz);
	half3 wsvd_norm = normalize(wsvd);
	
	dstfd_frag_clip(i, rnd);

	half4 albedo = frag_forward_get_albedo(i, texST);
	half3 normal3 = frag_forward_get_normal(i, texST);
	half3 emissive = frag_forward_get_emission_color(i, albedo, texST);
	
	frag_alphatest(i, rnd, albedo.a);

	// Заменяюще-аддетивные эффекты
	matcap_apply(i, albedo.rgb);
	pcw_apply(i, albedo.rgb, emissive);
	glitter_apply_color(i, texST, rnd, normal3, wsvd_norm, albedo.rgb, emissive);
	
	// Заменяюще-затеняющие эффекты
	fps_apply_colors(albedo.rgb, emissive);
	wnoise_apply(i, rnd, albedo.rgb, emissive);
	outline_apply_frag(albedo.rgb, emissive);
	
	// Последний, т.к. должен затенить все предыдущие эффекты.
	iwd_apply(i, albedo.rgb, emissive);
	
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
