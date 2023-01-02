#ifndef KAWAFLT_FRAG_FORWARD_ADD_INCLUDED
#define KAWAFLT_FRAG_FORWARD_ADD_INCLUDED

/* General fragment function */
half4 frag_forwardadd(FRAGMENT_IN i) : COLOR {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	apply_bitloss_frag(i);

	frag_cull(i);

	half2 uv = frag_applyst(i.uv0);
	fps_apply_uv(uv);
	apply_bitloss(uv);
	
	uint rnd = frag_rnd_init(i);
	
	float3 wsvd = UnityWorldSpaceViewDir(i.pos_world.xyz);
	half3 wsvd_norm = normalize(wsvd);
	
	dstfd_frag_clip(i, rnd);
	
	half4 albedo = frag_forward_get_albedo(i, uv);
	half3 normal3 = frag_forward_get_normal(i, uv);
	half3 emissive_dummy = half3(0,0,0); // Оптимизируется компилятором.
	
	apply_bitloss(albedo);
	apply_bitloss(normal3);
	
	frag_alphatest(i, rnd, albedo.a);
	
	// Заменяюще-аддетивные эффекты
	matcap_apply(i, albedo.rgb);
	pcw_apply(i, albedo.rgb, emissive_dummy);
	glitter_apply_color(i, uv, rnd, normal3, wsvd_norm, albedo.rgb, emissive_dummy);
	
	// Заменяюще-затеняющие эффекты
	fps_apply_colors(albedo.rgb, emissive_dummy);
	wnoise_apply(i, rnd, albedo.rgb, emissive_dummy);
	outline_apply_frag(albedo.rgb, emissive_dummy);
	
	// Последний, т.к. должен затенить все предыдущие эффекты.
	iwd_apply(i, albedo.rgb, emissive_dummy);
	
	apply_bitloss(albedo);
	apply_bitloss(normal3);
	
	half3 glossy_dummy = half3(0,0,0);
	gloss_apply(i, uv, /*out*/ albedo, /*out*/ glossy_dummy, wsvd_norm, normal3);
	
	apply_bitloss(albedo.rgb);
	
	half3 shading = half3(0,0,0);
	#if defined(SHADE_CUBEDPARADOXFLT)
		shading = frag_shade_cbdprdx_forward_add(i, normal3);
	#elif defined(SHADE_KAWAFLT_LOG)
		shading = frag_shade_kawaflt_log_forward_add(i, normal3);
	#elif defined(SHADE_KAWAFLT_RAMP)
		shading = frag_shade_kawaflt_ramp_forward_add(i, normal3);
	#elif defined(SHADE_KAWAFLT_SINGLE)
		shading = frag_shade_kawaflt_single_forward_add(i, normal3);
	#endif
	
	half4 finalColor;
	finalColor.a = albedo.a;
	finalColor.rgb = albedo * shading;
	
	UNITY_APPLY_FOG(i.fogCoord, finalColor);
	apply_bitloss(finalColor);
	return finalColor;
}

#endif // KAWAFLT_FRAG_FORWARD_ADD_INCLUDED
