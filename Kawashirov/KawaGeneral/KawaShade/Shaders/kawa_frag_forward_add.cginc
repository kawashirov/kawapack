#ifndef KAWAFLT_FRAG_FORWARD_ADD_INCLUDED
#define KAWAFLT_FRAG_FORWARD_ADD_INCLUDED

#if !defined(KAWAFLT_PASS_FORWARDADD)
	#error KAWAFLT_PASS_FORWARDADD not defined, but KAWAFLT_STAGE_FRAGMENT_FORWARD_ADD included. 
#endif

/* General fragment function */
half4 frag_forwardadd(FRAGMENT_IN i) : COLOR {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	frag_cull(i);
	fps_frag(i);

	float2 texST = frag_applyst(i.uv0);
	
	uint rnd = frag_rnd_init(i);
	
	dstfd_frag_clip(i, rnd);
	
	half4 albedo = frag_forward_get_albedo(i, texST);
	half3 emissive_dummy = half3(0,0,0); // Оптимизируется компилятором.
	half3 normal3 = frag_forward_get_normal(i, texST);
	
	frag_alphatest(i, rnd, albedo.a);
	
	// Заменяюще-аддетивные эффекты
	matcap_apply(i, albedo.rgb);
	pcw_apply(i, albedo.rgb, emissive_dummy);
	glitter_apply(texST, rnd, albedo.rgb, emissive_dummy);
	
	// Заменяюще-затеняющие эффекты
	fps_apply_frag(albedo.rgb, emissive_dummy);
	wnoise_apply(i, rnd, albedo.rgb, emissive_dummy);
	outline_apply_frag(albedo.rgb, emissive_dummy);
	
	// Последний, т.к. должен затенить все предыдущие эффекты.
	iwd_apply(i, albedo.rgb, emissive_dummy);
	
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
