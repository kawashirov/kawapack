#ifndef KAWAFLT_FEATURE_GLOSSY_INCLUDED
#define KAWAFLT_FEATURE_GLOSSY_INCLUDED

// Unity GLOSSY features

#if defined(GLOSSY_ON)
	#if defined(GLOSSY_METALLIC)
		uniform float _Metallic;
		#if defined(GLOSSY_MAP)
			sampler2D _MetallicGlossMap;
		#endif
	#elif defined(GLOSSY_SPECULAR)
		// Какого-то хуя уже определен в UnityLightingCommon.cginc
		//uniform float4 _SpecColor;
		#if defined(GLOSSY_MAP)
			sampler2D _SpecGlossMap;
		#endif
	#endif
	uniform float _Glossiness;
	#if defined(GLOSSY_FROM_SEPARATE)
		sampler2D _GlossinessMap;
	#endif
#endif

inline void gloss_apply(FRAGMENT_IN i, float2 uv, inout half4 albedo, inout half3 glossy, half3 wsvd_norm, half3 normal3) {
	glossy = half3(0,0,0);
	#if defined(GLOSSY_ON)
		half occlusion = 1;
		half rawGlossiness = _Glossiness;
		
		#if defined(GLOSSY_FROM_ALBEDO)
			rawGlossiness *= albedo.a;
		#elif defined(GLOSSY_FROM_SEPARATE)
			rawGlossiness *= tex2D(_SpecGlossMap, uv).r;
		#endif
		
		half3 specular = half3(0,0,0);
		half oneMinusReflectivity = 0; // Пока не испольуется.
		#if defined(GLOSSY_METALLIC)
			half metallic = _Metallic;
			#if defined(GLOSSY_MAP)
				half4 sample = tex2D(_MetallicGlossMap, uv);
				metallic *= sample.r;
				#if defined(GLOSSY_FROM_MAP)
					rawGlossiness *= sample.a;
				#endif
			#endif
			// UnityStandardUtils.cginc
			// albedo.rgb = DiffuseAndSpecularFromMetallic (albedo.rgb, metallic, /*out*/ specular, /*out*/ oneMinusReflectivity);
			specular = albedo.rgb * metallic;
			albedo.rgb = albedo.rgb * (1 - metallic);
		#elif defined(GLOSSY_SPECULAR)
			specular = _SpecColor.rgb;
			#if defined(GLOSSY_MAP)
				half4 sample = tex2D(_SpecGlossMap, uv);
				specular *= sample.rgb;
				#if defined(GLOSSY_FROM_MAP)
					rawGlossiness *= sample.a;
				#endif
			#endif
			// UnityStandardUtils.cginc
			// albedo.rgb = EnergyConservationBetweenDiffuseAndSpecular (albedo.rgb, specular, /*out*/ oneMinusReflectivity);
			specular = albedo.rgb * specular;
			albedo.rgb = albedo.rgb * (1 - specular);
		#endif
		
		half perceptualRoughness = 1;
		#if defined(GLOSSY_SMOOTHNESS)
			perceptualRoughness = SmoothnessToPerceptualRoughness(rawGlossiness);
		#elif defined(GLOSSY_PERCEPTUAL_ROUGHNESS)
			perceptualRoughness = rawGlossiness;
		#elif defined(GLOSSY_ROUGHNESS)
			perceptualRoughness = RoughnessToPerceptualRoughness(rawGlossiness);
		#endif
		
		// Это из UnityGlossyEnvironmentSetup
		Unity_GlossyEnvironmentData g = (Unity_GlossyEnvironmentData) 0;
		g.roughness = perceptualRoughness;
		g.reflUVW = reflect(-wsvd_norm, normal3);
		
		// Это из FragmentGI из UnityStandardCore.cginc,
		// Но его сложно заинклудить напрямую, по этому копипаста.
		UnityGIInput d = (UnityGIInput) 0;
		d.worldPos = i.pos_world;
		d.probeHDR[0] = unity_SpecCube0_HDR;
		d.probeHDR[1] = unity_SpecCube1_HDR;
		#if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
			d.boxMin[0] = unity_SpecCube0_BoxMin; // .w holds lerp value for blending
		#endif
		#ifdef UNITY_SPECCUBE_BOX_PROJECTION
			d.boxMax[0] = unity_SpecCube0_BoxMax;
			d.probePosition[0] = unity_SpecCube0_ProbePosition;
			d.boxMax[1] = unity_SpecCube1_BoxMax;
			d.boxMin[1] = unity_SpecCube1_BoxMin;
			d.probePosition[1] = unity_SpecCube1_ProbePosition;
		#endif
	
		// Это из UnityGlobalIllumination.cginc через Lighting.cginc
		glossy = UnityGI_IndirectSpecular(d, occlusion, g) * specular;
	#endif
}

#endif // KAWAFLT_FEATURE_GLOSSY_INCLUDED