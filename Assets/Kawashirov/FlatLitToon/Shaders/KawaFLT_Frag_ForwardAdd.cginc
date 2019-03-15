#ifndef KAWAFLT_G_FRAGMENT_FORWARD_ADD_INCLUDED
#define KAWAFLT_G_FRAGMENT_FORWARD_ADD_INCLUDED

#if !defined(KAWAFLT_PASS_FORWARDADD)
	#error KAWAFLT_PASS_FORWARDADD not defined, but KAWAFLT_STAGE_FRAGMENT_FORWARD_ADD included. 
#endif

#include ".\KawaFLT_Frag_Shared.cginc"

/* CubedParadox's Flat Lit Toon shading */
#if defined(SHADE_CUBEDPARADOXFLT)
	inline half3 frag_shade_cbdprdx_forward_add(FRAGMENT_IN i, half3 baseColor, half3 normal) {
		// float4 objPos = mul(unity_ObjectToWorld, float4(0,0,0,1));
		//float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
		//float3 lightColor = _LightColor0.rgb;
		UNITY_LIGHT_ATTENUATION(attenuation, i, i.posWorld.xyz);
		half lightContribution = dot(normalize(_WorldSpaceLightPos0.xyz - i.posWorld.xyz),normal)*attenuation;
		half3 directContribution = floor(saturate(lightContribution) * 2.0h);
		half lerp_v = saturate(directContribution + ((1.0h - _Sh_Cbdprdx_Shadow) * attenuation));
		return baseColor * lerp(0.0h, _LightColor0.rgb, lerp_v);
	}
#endif


/* Kawashirov's Flat Lit Toon Diffuse */
#if defined(SHADE_KAWAFLT_DIFFUSE)

	inline half3 frag_shade_kawaflt_diffuse_forward_add(FRAGMENT_IN i, half3 albedo, half3 normal) {
		float3 view_dir = normalize(KawaWorldSpaceViewDir(i.posWorld));
		float view_tangency = dot(normal, view_dir);
		float rim_factor = frag_shade_kawaflt_diffuse_rim_factor(view_tangency);

		UNITY_LIGHT_ATTENUATION(light_atten, i, i.posWorld.xyz);
		float3 dir = normalize(UnityWorldSpaceLightDir(i.posWorld.xyz));
		float direct_tangency = max(0, dot(normal, dir));
		direct_tangency = frag_shade_kawaflt_diffuse_smooth_tangency(direct_tangency);
		half3 light_shaded = _LightColor0.rgb * light_atten * direct_tangency * rim_factor;
		half3 light_final = max(frag_shade_kawaflt_diffuse_steps(light_shaded), half3(0,0,0));

		return albedo * light_final;
	}

#endif


/* Kawashirov's Flat Lit Toon Ramp */
#if defined(SHADE_KAWAFLT_RAMP)

	inline half3 frag_shade_kawaflt_ramp_forward_add(FRAGMENT_IN i, half3 albedo, half3 normal) {
		UNITY_LIGHT_ATTENUATION(atten, i, i.posWorld.xyz);
		float3 wsld = normalize(UnityWorldSpaceLightDir(i.posWorld.xyz));
		float ramp_uv = dot(normal, wsld) * 0.5 + 0.5;
		half3 ramp = frag_shade_kawaflt_ramp_apply(ramp_uv);
		return albedo * _LightColor0.rgb * ramp * atten;
	}

#endif


/* General fragment function */
half4 frag_forwardadd(FRAGMENT_IN i) : COLOR {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	frag_cull(i);
	fps_frag(i);

	float2 texST = frag_applyst(i.uv0);
	
	uint rnd4_sc = frag_rnd_screencoords(i);
	dstfd_frag_clip(i, rnd4_sc);
	dsntgrt_frag_clip(i, rnd4_sc);
	
	half3 normal3 = frag_forward_get_normal(i, texST);
	half4 albedo = frag_forward_get_albedo(i, texST);
	half originalA = albedo.a;
	
	frag_alphatest(i, rnd4_sc, albedo.a);
	
	half4 finalColor;
	finalColor.a = albedo.a;
	#if defined(SHADE_CUBEDPARADOXFLT)
		finalColor.rgb = frag_shade_cbdprdx_forward_add(i, albedo.rgb, normal3);
	#endif
	#if defined(SHADE_KAWAFLT_DIFFUSE)
		finalColor.rgb = frag_shade_kawaflt_diffuse_forward_add(i, albedo.rgb, normal3);
	#endif
	#if defined(SHADE_KAWAFLT_RAMP)
		finalColor.rgb = frag_shade_kawaflt_ramp_forward_add(i, albedo.rgb, normal3);
	#endif
	
	UNITY_APPLY_FOG(i.fogCoord, finalColor);
	return finalColor;
}

#endif // KAWAFLT_G_FRAGMENT_FORWARD_ADD_INCLUDED
