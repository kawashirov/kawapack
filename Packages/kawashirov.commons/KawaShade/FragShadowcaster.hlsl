#ifndef KAWAFLT_FRAG_SHADOW_CASTER_INCLUDED
#define KAWAFLT_FRAG_SHADOW_CASTER_INCLUDED

half4 frag_shadowcaster(FRAGMENT_IN i) : SV_Target {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
	
	frag_rnd_init(i);
	apply_bitloss_frag(i, true);
	
	frag_cull(i);
	
	float2 texST = frag_applyst(i.uv0);
	fps_apply_uv(texST);
	// apply_bitloss2(texST, uint2(63037, 49509), uint2(58373, 47867), true);
	
	dstfd_frag_clip(i);
	
	half alpha = frag_forward_get_albedo(i, texST).a;
	apply_bitloss(alpha, 64069, 38685, true);
	frag_alphatest(i, alpha);
	
	SHADOW_CASTER_FRAGMENT(i)
}

#endif // KAWAFLT_FRAG_SHADOW_CASTER_INCLUDED