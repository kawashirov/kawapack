#ifndef KAWAFLT_FRAG_FORWARD_ADD_INCLUDED
#define KAWAFLT_FRAG_FORWARD_ADD_INCLUDED

/* General fragment function */
half4 frag_forwardadd(FRAGMENT_IN i) : COLOR {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	apply_bitloss_frag(i);

	frag_cull(i);

	half2 texST = frag_applyst(i.uv0);
	fps_apply_uv(texST);
	apply_bitloss(texST);
	
	uint rnd = frag_rnd_init(i);
	
	float3 wsvd = UnityWorldSpaceViewDir(i.pos_world.xyz);
	half3 wsvd_norm = normalize(wsvd);
	
	dstfd_frag_clip(i, rnd);
	
	half4 albedo = frag_forward_get_albedo(i, texST);
	half3 normal3 = frag_forward_get_normal(i, texST);
	half3 emissive_dummy = half3(0,0,0); // Оптимизируется компилятором.
	
	apply_bitloss(albedo);
	apply_bitloss(normal3);
	
	frag_alphatest(i, rnd, albedo.a);
	
	// Заменяюще-аддетивные эффекты
	matcap_apply(i, albedo.rgb);
	pcw_apply(i, albedo.rgb, emissive_dummy);
	glitter_apply_color(i, texST, rnd, normal3, wsvd_norm, albedo.rgb, emissive_dummy);
	
	// Заменяюще-затеняющие эффекты
	fps_apply_colors(albedo.rgb, emissive_dummy);
	wnoise_apply(i, rnd, albedo.rgb, emissive_dummy);
	outline_apply_frag(albedo.rgb, emissive_dummy);
	
	// Последний, т.к. должен затенить все предыдущие эффекты.
	iwd_apply(i, albedo.rgb, emissive_dummy);
	
	apply_bitloss(albedo);
	apply_bitloss(normal3);
	
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
	apply_bitloss(finalColor);
	return finalColor;
}

#endif // KAWAFLT_FRAG_FORWARD_ADD_INCLUDED
