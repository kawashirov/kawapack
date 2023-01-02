#ifndef KAWAFLT_FRAG_SHADOW_CASTER_INCLUDED
#define KAWAFLT_FRAG_SHADOW_CASTER_INCLUDED

half4 frag_shadowcaster(FRAGMENT_IN i) : SV_Target {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
	
	apply_bitloss_frag(i);
	
	frag_cull(i);
	
	float2 texST = frag_applyst(i.uv0);
	fps_apply_uv(texST);
	apply_bitloss(texST);
	
	uint rnd = frag_rnd_init(i);
	
	dstfd_frag_clip(i, rnd);
	
	half alpha = frag_forward_get_albedo(i, texST).a;
	apply_bitloss(alpha);
	frag_alphatest(i, rnd, alpha);
	
	SHADOW_CASTER_FRAGMENT(i)
}

#endif // KAWAFLT_FRAG_SHADOW_CASTER_INCLUDED