#ifndef KAWAFLT_G_FRAGMENT_FORWARD_BASE_INCLUDED
#define KAWAFLT_G_FRAGMENT_FORWARD_BASE_INCLUDED

#if !defined(KAWAFLT_PASS_FORWARDBASE)
	#error KAWAFLT_PASS_FORWAR_BASE not defined, but KAWAFLT_STAGE_FRAGMENT_FORWARD_BASE included. 
#endif

// #include "UnityInstancing.cginc"
// #include "UnityStandardUtils.cginc"

#include ".\KawaFLT_Frag_Shared.cginc"

/* ForwardBase only utils */

inline half3 frag_forward_get_emission_color(inout FRAGMENT_IN i, half3 baseColor, float2 texST) {
	half3 em = half3(0,0,0);
	#if defined(AVAILABLE_EMISSIONMAP)
		em = UNITY_SAMPLE_TEX2D(_EmissionMap, texST).rgb;
		em = em * em * _EmissionColor.rgb;
	#endif
	em = fps_mix(half4(em, 0)).rgb;
	em = pcw_mix(em, i, true); // Mix-in Poly Color Wave
	return em;
}


/* CubedParadox's Flat Lit Toon shading */
#if defined(SHADE_CUBEDPARADOXFLT)

	inline half3 frag_shade_cbdprdx_forward_base(FRAGMENT_IN i, half3 baseColor, float3 normalDirection, half3 emissive) {
		
		half3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
		UNITY_LIGHT_ATTENUATION(attenuation, i, i.posWorld.xyz);

		half grayscalelightcolor = dot(_LightColor0.rgb, grayscale_vector);
		half bottomIndirectLighting = grayscaleSH9(half3(0, -1, 0));
		half topIndirectLighting = grayscaleSH9(half3(0, 1, 0));
		half grayscaleDirectLighting = dot(lightDirection, normalDirection)*grayscalelightcolor*attenuation + grayscaleSH9(normalDirection);

		half lightDifference = topIndirectLighting + grayscalelightcolor - bottomIndirectLighting;
		half remappedLight = (grayscaleDirectLighting - bottomIndirectLighting) / lightDifference;

		half3 indirectLighting = saturate((ShadeSH9(half4(0, -1, 0, 1))));
		half3 directLighting = saturate((ShadeSH9(half4(0, 1, 0, 1)) + _LightColor0.rgb));
		half3 directContribution = saturate((1.0h - _Sh_Cbdprdx_Shadow) + floor(saturate(remappedLight) * 2.0h));

		half3 finalColor = (baseColor * lerp(indirectLighting, directLighting, directContribution)) + emissive;
		return finalColor;
	}

#endif


/* Kawashirov's Flat Lit Toon Diffuse */
#if defined(SHADE_KAWAFLT_DIFFUSE)

	inline half3 frag_shade_kawaflt_diffuse_forward_base(FRAGMENT_IN i, half3 albedo, half3 normal3, half3 emission) {
		float3 view_dir = normalize(KawaWorldSpaceViewDir(i.posWorld));
		float view_tangency = dot(normal3, view_dir);

		half rim_factor = frag_shade_kawaflt_diffuse_rim_factor(view_tangency);

		half3 vertexlight = half3(0,0,0);
		vertexlight = i.vertexlight * rim_factor;
		vertexlight = max(frag_shade_kawaflt_diffuse_steps(vertexlight), half3(0,0,0));

		half3 ambient = half3(0,0,0);
		#if defined(UNITY_SHOULD_SAMPLE_SH)
			ambient = i.ambient + SHEvalLinearL2(half4(normal3, 1));
			ambient = lerp(ambient, half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w), _Sh_Kwshrv_Smth) * rim_factor;
			ambient = max(frag_shade_kawaflt_diffuse_steps(ambient), half3(0,0,0));
		#endif

		// Основной прямой свет сцены
		UNITY_LIGHT_ATTENUATION(direct_atten, i, i.posWorld.xyz);
		float3 wsld = normalize(UnityWorldSpaceLightDir(i.posWorld.xyz));
		float direct_tangency = max(0, dot(normal3, wsld));
		direct_tangency = frag_shade_kawaflt_diffuse_smooth_tangency(direct_tangency);
		half3 direct_shaded = _LightColor0.rgb * direct_atten * direct_tangency * rim_factor;
		half3 direct_final = max(frag_shade_kawaflt_diffuse_steps(direct_shaded), half3(0,0,0));

		return albedo * (direct_final + vertexlight + ambient) + emission;
	}
#endif


/* Kawashirov's Flat Lit Toon Ramp */
#if defined(SHADE_KAWAFLT_RAMP)

	inline half3 frag_shade_kawaflt_ramp_forward_base(FRAGMENT_IN i, half3 albedo, float2 normal3, half3 emission) {
		half3 ambient = half3(0,0,0);
		#if defined(UNITY_SHOULD_SAMPLE_SH)
			ambient = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) * _Sh_KwshrvRmp_NdrctClr.rgb * _Sh_KwshrvRmp_NdrctClr.a;
		#endif

		// Основной прямой свет сцены
		UNITY_LIGHT_ATTENUATION(direct_atten, i, i.posWorld.xyz);
		float3 wsld = normalize(UnityWorldSpaceLightDir(i.posWorld.xyz));
		float ramp_uv = dot(normal3, wsld) * 0.5 + 0.5;
		half3 ramp = frag_shade_kawaflt_ramp_apply(ramp_uv);
		half3 direct = _LightColor0.rgb * ramp * direct_atten;

		return albedo * (direct + i.vertexlight + ambient) + emission;
	}
#endif


/* General fragment function */
half4 frag_forwardbase(FRAGMENT_IN i) : COLOR {
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
	half3 emissive = frag_forward_get_emission_color(i, albedo, texST);
	
	frag_alphatest(i, rnd4_sc, albedo.a);
	
	half4 finalColor;
	finalColor.a = albedo.a;
	#if defined(SHADE_CUBEDPARADOXFLT)
		finalColor.rgb = frag_shade_cbdprdx_forward_base(i, albedo.rgb, normal3, emissive);
	#endif
	#if defined(SHADE_KAWAFLT_DIFFUSE)
		finalColor.rgb = frag_shade_kawaflt_diffuse_forward_base(i, albedo.rgb, normal3, emissive);
	#endif
	#if defined(SHADE_KAWAFLT_RAMP)
		finalColor.rgb = frag_shade_kawaflt_ramp_forward_base(i, albedo.rgb, normal3, emissive);
	#endif
	
	UNITY_APPLY_FOG(i.fogCoord, finalColor);
	return finalColor;
}

#endif // KAWAFLT_G_FRAGMENT_FORWARD_BASE_INCLUDED
