#ifndef KAWAFLT_FRAG_SHADOW_CASTER_INCLUDED
#define KAWAFLT_FRAG_SHADOW_CASTER_INCLUDED

#if !defined(KAWAFLT_PASS_SHADOWCASTER)
	#error KAWAFLT_PASS_SHADOWCASTER not defined, but KAWAFLT_SHADOW_CASTER included. 
#endif

half4 frag_shadowcaster(FRAGMENT_IN i) : SV_Target {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 
	frag_cull(i);
	fps_frag(i);
	
	float2 texST = frag_applyst(i.uv0);

	uint rnd4_sc = frag_rnd_init(i);
	
	dstfd_frag_clip(i, rnd4_sc);

	half alpha = frag_forward_get_albedo(i, texST, rnd4_sc).a;
	frag_alphatest(i, rnd4_sc, alpha);

	SHADOW_CASTER_FRAGMENT(i)
}

#endif // KAWAFLT_FRAG_SHADOW_CASTER_INCLUDED