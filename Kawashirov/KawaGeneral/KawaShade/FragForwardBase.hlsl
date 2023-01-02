#ifndef KAWAFLT_FRAG_FORWARD_BASE_INCLUDED
#define KAWAFLT_FRAG_FORWARD_BASE_INCLUDED

// ForwardBase only utils

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

	apply_bitloss_frag(i);

	frag_cull(i);

	float2 uv = frag_applyst(i.uv0);
	fps_apply_uv(uv);
	apply_bitloss(uv);

	uint rnd = frag_rnd_init(i);
	
	float3 wsvd = UnityWorldSpaceViewDir(i.pos_world.xyz);
	half3 wsvd_norm = normalize(wsvd);
	
	dstfd_frag_clip(i, rnd);

	half4 albedo = frag_forward_get_albedo(i, uv);
	half3 normal3 = frag_forward_get_normal(i, uv);
	half3 emissive = frag_forward_get_emission_color(i, albedo, uv);
	
	apply_bitloss(albedo);
	apply_bitloss(normal3);
	apply_bitloss(emissive);
	
	frag_alphatest(i, rnd, albedo.a);

	// Заменяюще-аддетивные эффекты
	matcap_apply(i, albedo.rgb);
	pcw_apply(i, albedo.rgb, emissive);
	glitter_apply_color(i, uv, rnd, normal3, wsvd_norm, albedo.rgb, emissive);
	
	// Заменяюще-затеняющие эффекты
	fps_apply_colors(albedo.rgb, emissive);
	wnoise_apply(i, rnd, albedo.rgb, emissive);
	outline_apply_frag(albedo.rgb, emissive);
	
	// Последний, т.к. должен затенить все предыдущие эффекты.
	iwd_apply(i, albedo.rgb, emissive);
	
	apply_bitloss(albedo);
	apply_bitloss(normal3);
	apply_bitloss(emissive);
	
	half3 glossy = half3(0,0,0);
	half3 diffuse = half3(0,0,0);
	gloss_apply(i, uv, /*out*/ albedo, /*out*/ glossy, wsvd_norm, normal3);
	
	apply_bitloss(albedo.rgb);
	apply_bitloss(glossy);
	
	half3 shading = half3(0,0,0);
	#if defined(SHADE_CUBEDPARADOXFLT)
		shading = frag_shade_cbdprdx_forward_base(i, normal3);
	#elif defined(SHADE_KAWAFLT_LOG)
		shading = frag_shade_kawaflt_log_forward_base(i, normal3);
	#elif defined(SHADE_KAWAFLT_RAMP)
		shading = frag_shade_kawaflt_ramp_forward_base(i, normal3);
	#elif defined(SHADE_KAWAFLT_SINGLE)
		shading = frag_shade_kawaflt_single_forward_base(i, normal3);
	#endif
	
	half4 finalColor;
	finalColor.a = albedo.a;
	finalColor.rgb = albedo.rgb * shading + glossy + emissive;
	
	UNITY_APPLY_FOG(i.fogCoord, finalColor);
	apply_bitloss(finalColor);
	return finalColor;
}

#endif // KAWAFLT_FRAG_FORWARD_BASE_INCLUDED
