#ifndef KAWAFLT_FRAG_FORWARD_ADD_INCLUDED
#define KAWAFLT_FRAG_FORWARD_ADD_INCLUDED

/* General fragment function */
half4 frag_forwardadd(FRAGMENT_IN i) : COLOR {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	frag_rnd_init(i);
	apply_bitloss_frag(i, true);

	frag_cull(i);

	half2 uv = frag_applyst(i.uv0);
	fps_apply_uv(uv);
	// apply_bitloss2(uv, uint2(60621, 46799), uint2(42319, 43484), true);
	
	float3 wsvd = UnityWorldSpaceViewDir(i.pos_world.xyz);
	apply_bitloss3(wsvd, uint3(44380, 44971, 50874), uint3(65110, 35381, 38937), true);
	half3 wsvd_norm = normalize(wsvd);
	
	dstfd_frag_clip(i);
	
	half4 albedo = frag_forward_get_albedo(i, uv);
	apply_bitloss4(albedo, uint4(33052, 58550, 61876, 44574), uint4(55670, 52512, 44344, 61902), true);
	
	half3 normal3 = frag_forward_get_normal(i, uv);
	apply_bitloss3(normal3, uint3(54825, 61421, 55913), uint3(50283, 41736, 38679), true);
	
	half3 emissive_dummy = half3(0,0,0); // Оптимизируется компилятором.
		
	frag_alphatest(i, albedo.a);
	
	// Заменяюще-аддетивные эффекты
	matcap_apply(i, albedo.rgb);
	pcw_apply(i, albedo.rgb, emissive_dummy);
	glitter_apply_color(i, uv, normal3, wsvd_norm, albedo.rgb, emissive_dummy);
	
	// Заменяюще-затеняющие эффекты
	fps_apply_colors(albedo.rgb, emissive_dummy);
	wnoise_apply(i, albedo.rgb, emissive_dummy);
	outline_apply_frag(albedo.rgb, emissive_dummy);
	
	// Последний, т.к. должен затенить все предыдущие эффекты.
	iwd_apply(i, albedo.rgb, emissive_dummy);
	
	apply_bitloss4(albedo, uint4(47036, 39207, 42739, 44766), uint4(46675, 57241, 59872, 58313), true);
	apply_bitloss3(normal3, uint3(55021, 43714, 34327), uint3(42554, 33194, 62625), true);
	
	half3 glossy_dummy = half3(0,0,0); // Оптимизируется компилятором.
	gloss_apply(i, uv, /*out*/ albedo, /*out*/ glossy_dummy, wsvd_norm, normal3);
	
	apply_bitloss3(albedo.rgb, uint3(51722, 39913, 38803), uint3(61850, 47277, 62755), true);
	
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
	apply_bitloss4(finalColor, uint4(57356, 55098, 48753, 57138), uint4(56598, 43682, 39613, 47326), true);
	return finalColor;
}

#endif // KAWAFLT_FRAG_FORWARD_ADD_INCLUDED
