#ifndef KAWAFLT_FRAG_FORWARD_ADD_INCLUDED
#define KAWAFLT_FRAG_FORWARD_ADD_INCLUDED

#if !defined(KAWAFLT_PASS_FORWARDADD)
	#error KAWAFLT_PASS_FORWARDADD not defined, but KAWAFLT_STAGE_FRAGMENT_FORWARD_ADD included. 
#endif

#include ".\KawaFLT_Frag_Shared.cginc"

#if defined(SHADE_CUBEDPARADOXFLT)
	inline half3 frag_shade_cbdprdx_forward_add(FRAGMENT_IN i, half3 baseColor, half3 normal) {
		// float4 objPos = mul(unity_ObjectToWorld, float4(0,0,0,1));
		//float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
		//float3 lightColor = _LightColor0.rgb;
		UNITY_LIGHT_ATTENUATION(attenuation, i, i.pos_world.xyz);
		half lightContribution = dot(normalize(_WorldSpaceLightPos0.xyz - i.pos_world.xyz),normal)*attenuation;
		half3 directContribution = floor(saturate(lightContribution) * 2.0h);
		half lerp_v = saturate(directContribution + ((1.0h - _Sh_Cbdprdx_Shadow) * attenuation));
		return baseColor * lerp(0.0h, _LightColor0.rgb, lerp_v);
	}
#endif

#if defined(SHADE_KAWAFLT_LOG)

	inline half3 frag_shade_kawaflt_log_forward_add(FRAGMENT_IN i, half3 albedo, half3 normal) {
		float3 view_dir = normalize(KawaWorldSpaceViewDir(i.pos_world));
		float view_tangency = dot(normal, view_dir);
		half rim_factor = frag_shade_kawaflt_log_rim_factor(view_tangency);

		half3 main = frag_shade_kawaflt_log_forward_main(i, normal, rim_factor);
		return max(half3(0,0,0), albedo * main);
	}

#endif

#if defined(SHADE_KAWAFLT_RAMP)

	inline half3 frag_shade_kawaflt_ramp_forward_add(FRAGMENT_IN i, half3 albedo, half3 normal) {
		half3 main = frag_shade_kawaflt_ramp_forward_main(i, normal);
		return max(half3(0,0,0), albedo * main);
	}

#endif

#if defined(SHADE_KAWAFLT_SINGLE)

	inline half3 frag_shade_kawaflt_single_forward_add(FRAGMENT_IN i, half3 albedo, half3 normal) {
		half3 main = frag_shade_kawaflt_single_forward_main(i, normal);
		return max(half3(0,0,0), albedo * main);
	}

#endif


/* General fragment function */
half4 frag_forwardadd(FRAGMENT_IN i) : COLOR {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	frag_cull(i);
	fps_frag(i);

	float2 texST = frag_applyst(i.uv0);
	
	uint rnd4_sc = frag_rnd_init(i);
	dstfd_frag_clip(i, rnd4_sc);
	
	half3 normal3 = frag_forward_get_normal(i, texST);
	half4 albedo = frag_forward_get_albedo(i, texST, rnd4_sc);
	half originalA = albedo.a;
	
	frag_alphatest(i, rnd4_sc, albedo.a);

	albedo.rgb = matcap_apply(i, albedo.rgb);
	
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
