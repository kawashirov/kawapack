#ifndef KAWAFLT_FRAG_SHARED_INCLUDED
#define KAWAFLT_FRAG_SHARED_INCLUDED

#include "UnityLightingCommon.cginc"
#include "UnityStandardUtils.cginc"

inline float2 frag_pixelcoords(FRAGMENT_IN i) {
	float2 pxc = float2(1, 1);
	#if defined(RANDOM_MIX_COORD) || defined(RANDOM_SEED_TEX)
		pxc =  i.pos_screen.xy / i.pos_screen.w * _ScreenParams.xy;
	#endif
	//float4 sp = ComputeScreenPos(UnityPixelSnap(i.pos));
	//pxc = sp.xy * _ScreenParams.xy / sp.w;
	return pxc;
}

inline uint frag_rnd_init(FRAGMENT_IN i) {
	float2 sc_raw = frag_pixelcoords(i);
	float2 sc_floor = floor(sc_raw);
	#if defined(RANDOM_SCREEN_SCALE)
		sc_floor = floor(sc_raw / _Rnd_ScreenScale) * _Rnd_ScreenScale;
	#endif
	uint rnd = rnd_init_noise_coords((uint2) sc_floor);
	#if defined(RANDOM_MIX_COORD)
		rnd = rnd_apply_uint2(rnd, asuint(sc_floor));
	#endif
	#if defined(RANDOM_MIX_TIME)
		rnd = rnd_apply_time(rnd);
	#endif
	return rnd;
}


/* Infinity War features */

inline half4 iwd_mix_albedo(half4 color, FRAGMENT_IN i) {
	#if defined(IWD_ON)
		color.rgb = lerp(color.rgb, _IWD_TintColor.rgb, _IWD_TintColor.a * i.iwd_tint);
	#endif
	return color;
}

inline half3 iwd_mix_emission(half3 color, FRAGMENT_IN i) {
	#if defined(IWD_ON)
		// Затенение эмишона.
		color = color * saturate(1.0 - _IWD_TintColor.a * i.iwd_tint);
	#endif
	return color;
}

/* Distance Fade features */

inline void dstfd_frag_clip(inout FRAGMENT_IN i, inout uint rnd) {
	#if defined(DSTFD_ON)
		// Равномерный рандом от 0 до 1
		half rnd_01 = rnd_next_float_01(rnd); 

		half clip_v;
		#if defined(DSTFD_RANGE)
			half rnd_nonlin = pow(rnd_01, _DstFd_AdjustPower);
			half dist = lerp(_DstFd_Near, _DstFd_Far, rnd_nonlin);
			clip_v = dist - i.dstfd_distance;
		#elif defined(DSTFD_INFINITY)
			half rnd_nonlin = pow((1.0h - rnd_01) / rnd_01, 1.0h / _DstFd_AdjustPower) * _DstFd_AdjustScale;
			half dist = rnd_nonlin + _DstFd_Near;
			clip_v = dist - i.dstfd_distance;
		#endif

		clip(clip_v * _DstFd_Axis.w);
	#endif
}

/* FPS features */
// (i.uv0) -> (i.uv0)
inline void fps_frag(inout FRAGMENT_IN i) {
	#if defined(FPS_TEX)
		uint fps = clamp( (uint) round(unity_DeltaTime.w), 0, 99);
		uint digit = (i.uv0.x > 0.5 ? fps : (fps / 10)) % 10;
		i.uv0.x = frac(i.uv0.x * 2) / 10 + half(digit) / 10;
	#endif
}

inline half3 fps_mix(half3 color) {
	#if defined(FPS_ON)
		color *= lerp(_FPS_TLo, _FPS_THi, unity_DeltaTime.w / 91.0h);
	#endif
	return color;
}

/* White Noise features */

inline half3 wnoise_mix(half3 color, FRAGMENT_IN i, bool is_emission, inout uint rnd) {
	#if defined(WNOISE_ON)
		float wnoise = rnd_next_float_01(rnd);
		float factor_em = 0;
		#if defined(EMISSION_ON)
			factor_em = _WNoise_Em;
		#endif
		float factor = is_emission ? factor_em : _WNoise_Albedo;
		color.rgb = lerp(color.rgb, wnoise.rrr, factor);
	#endif
	return color;
}

/* PolyColorWave features */

inline half3 pcw_mix(half3 color, FRAGMENT_IN i, bool is_emission) {
	// Mix-in Poly Color Wave but only in BASE pass
	#if defined(PCW_ON) && defined(KAWAFLT_PASS_FORWARDBASE)
		color = lerp(color, i.pcw_color.rgb, i.pcw_color.a * (is_emission ? _PCW_Em : (1.0 - _PCW_Em)));
	#endif
	return color;
}


inline float2 frag_applyst(float2 uv) {
	//#if defined(AVAILABLE_ST)
		//uv = TRANSFORM_TEX(uv, _MainTex /* _ST */);
	//#endif
	return uv;
}

inline void frag_alphatest(FRAGMENT_IN i, inout uint rnd, inout half alpha) {
	#if defined(CUTOFF_FADE)
		// Первичное преобразование
		alpha = (alpha - _Cutoff) / (1.0 - _Cutoff);
		clip(alpha);
	#endif
	#if defined(CUTOFF_CLASSIC) 
		#if defined(CUTOFF_FADE)
			#error "CUTOFF_CLASSIC defined (with) CUTOFF_FADE"
		#endif
		clip(alpha - _Cutoff);
	#elif defined(CUTOFF_RANDOM)
		float spread = rnd_next_float_01(rnd);
		#if defined(CUTOFF_RANDOM_H01)
			spread = smoothstep(0, 1, spread);
		#endif
			float rnd_cutoff = lerp(_CutoffMin, _CutoffMax, spread);
		clip(alpha - rnd_cutoff);
	#endif
}

inline void frag_cull(FRAGMENT_IN i) {
	#if defined(NEED_CULL) && defined(KAWAFLT_PIPELINE_VF)
		// Без геометри стейджа у нас нет возможность сбрасывать примитивы, по этому сбрасываем фрагменты
		if (i.cull) discard;
	#endif
}

inline half4 frag_forward_get_albedo(FRAGMENT_IN i, float2 texST, inout uint rnd) {
	half4 color;
	#if defined(AVAILABLE_MAINTEX)
		color = UNITY_SAMPLE_TEX2D(_MainTex, texST);
		#if defined(AVAILABLE_COLORMASK)
			half mask = UNITY_SAMPLE_TEX2D(_ColorMask, texST).r;
			color = lerp(color, color * _Color, mask);
		#else
		 	color *= _Color;
		#endif
	#else
		color = _Color;
	#endif

	color.rgb = wnoise_mix(color.rgb, i, false, rnd);
	color.rgb = fps_mix(color.rgb);
	color.rgb = pcw_mix(color.rgb, i, false); // Mix-in Poly Color Wave
	color = iwd_mix_albedo(color, i);

	#if defined(KAWAFLT_PASS_FORWARD) && defined(OUTLINE_ON)
		UNITY_FLATTEN if(i.is_outline) {
			#if defined(OUTLINE_COLORED)
				color.rgb = _outline_color.rgb;
			#else
				color.rgb *= _outline_color.rgb;
			#endif
		}
	#endif

	return color;
}

#if defined(KAWAFLT_PASS_FORWARD)
	inline half3 frag_forward_get_normal(FRAGMENT_IN i, float2 texST) {
		#if defined(_NORMALMAP)
			i.normal_world = normalize(i.normal_world);
			half3x3 tangent_world_basis = half3x3(i.tangent_world, i.bitangent_world, i.normal_world);
			half3 bump = UnpackScaleNormal(UNITY_SAMPLE_TEX2D(_BumpMap, texST), _BumpScale);
			half3 normal_world_bumped = normalize(mul(bump, tangent_world_basis)); // Perturbed normals
		#else
			half3 normal_world_bumped = normalize(i.normal_world);
		#endif
		return normal_world_bumped;
	}

	inline half frag_forward_get_light_attenuation(FRAGMENT_IN i) {
		UNITY_LIGHT_ATTENUATION(attenuation, i, i.pos_world.xyz);
		return attenuation;
	}

	#if defined(SHADE_CUBEDPARADOXFLT)
		// ???
	#endif

	#if defined(SHADE_KAWAFLT)

		// Тоже, что и UNITY_LIGHT_ATTENUATION из AutoLight.cginc, но без учёта теней.
		inline half frag_shade_kawaflt_attenuation_no_shadow(half3 worldPos) {
			#if defined(POINT)
				unityShadowCoord3 lightCoord = mul(unity_WorldToLight, unityShadowCoord4(worldPos, 1)).xyz;
				return tex2D(_LightTexture0, dot(lightCoord, lightCoord).rr).UNITY_ATTEN_CHANNEL;
			#elif defined(SPOT)
				unityShadowCoord4 lightCoord = mul(unity_WorldToLight, unityShadowCoord4(worldPos, 1));
				return (lightCoord.z > 0) * UnitySpotCookie(lightCoord) * UnitySpotAttenuate(lightCoord.xyz);
			#elif defined(DIRECTIONAL)
				return 1.0;
			#elif defined(POINT_COOKIE)
				unityShadowCoord3 lightCoord = mul(unity_WorldToLight, unityShadowCoord4(worldPos, 1)).xyz;
				return tex2D(_LightTextureB0, dot(lightCoord, lightCoord).rr).UNITY_ATTEN_CHANNEL * texCUBE(_LightTexture0, lightCoord).w;
			#elif defined(DIRECTIONAL_COOKIE)
				unityShadowCoord2 lightCoord = mul(unity_WorldToLight, unityShadowCoord4(worldPos, 1)).xy;
				return tex2D(_LightTexture0, lightCoord).w;
			#else
				#error
			#endif
		}

	#endif

	#if defined(SHADE_KAWAFLT_LOG)

		inline half3 frag_shade_kawaflt_log_round(half value) {
			// only apply bound smooth when it's noticeble
			// <0.01 full-sharp bound
			// 0.01..0.99 mixed bound
			// >0.99 full-smooth bound
			UNITY_BRANCH if (_Sh_Kwshrv_BndSmth < 0.01) {
				value = round(value);
			} else {
				half smooth = _Sh_Kwshrv_BndSmth / 2.0h;
				half value_frac = frac(value + 0.5);
				half left_pulse = (saturate(value_frac / smooth) - 1.0h) / 2.0h;
				half right_pulse = saturate((value_frac - 1.0h) / smooth + 1.0h) / 2.0h;
				value = round(value) + left_pulse + right_pulse; 
			}
			return value;
		}

		inline half frag_shade_kawaflt_log_rim_factor(half tangency) {
			return 1.0h + (pow(1.0h - abs(tangency), _Sh_Kwshrv_RimPwr) + _Sh_Kwshrv_RimBs) * _Sh_Kwshrv_RimScl;
		}

		inline float frag_shade_kawaflt_log_smooth_tangency(float tangency) {
			return saturate(lerp(tangency, _Sh_Kwshrv_Smth_Tngnt, _Sh_Kwshrv_Smth));
		}

		inline half frag_shade_kawaflt_log_steps_mono(half atten) {
			UNITY_BRANCH if ( _Sh_KwshrvLog_Fltnss > 0.01 && _Sh_Kwshrv_BndSmth < 0.99 && atten > 0.01) {
				// Only apply steps when flatness noticeble
				float layers = atten;
				layers = log(layers) * _Sh_Kwshrv_FltLogSclA;
				layers = frag_shade_kawaflt_log_round(layers);
				layers = exp(layers / _Sh_Kwshrv_FltLogSclA);
				atten = lerp(atten, layers, _Sh_KwshrvLog_Fltnss);
			}
			return atten;
		}

		inline half frag_shade_kawaflt_log_steps_color(half color) {
			UNITY_BRANCH if ( _Sh_KwshrvLog_Fltnss > 0.01 && _Sh_Kwshrv_BndSmth < 0.99 && all(color > half3(0.01, 0.01, 0.01) )) {
				// Only apply steps when flatness noticeble
				float luma = Luminance(color);
				float layers = color;
				layers = log(layers) * _Sh_Kwshrv_FltLogSclA;
				layers = frag_shade_kawaflt_log_round(layers);
				layers = exp(layers / _Sh_Kwshrv_FltLogSclA);
				color = lerp(color, layers * (layers / luma), _Sh_KwshrvLog_Fltnss);
			}
			return color;
		}

		inline half3 frag_shade_kawaflt_log_forward_main(FRAGMENT_IN i, half3 normal, half rim_factor) {
			half light_atten = frag_shade_kawaflt_attenuation_no_shadow(i.pos_world.xyz);

			float3 wsld = normalize(UnityWorldSpaceLightDir(i.pos_world.xyz));
			float tangency = max(0, dot(normal, wsld));
			tangency = frag_shade_kawaflt_log_smooth_tangency(tangency);
			half shadow_atten = UNITY_SHADOW_ATTENUATION(i, i.pos_world.xyz);
			half shade_blended = frag_shade_kawaflt_log_steps_mono(tangency * rim_factor * shadow_atten);
			half shade_separated = frag_shade_kawaflt_log_steps_mono(tangency * rim_factor) * shadow_atten;
			half shade = lerp(shade_separated, shade_blended, _Sh_Kwshrv_ShdBlnd);

			return _LightColor0.rgb * max(0.0h, light_atten * shade);
		}

	#endif

	#if defined(SHADE_KAWAFLT_RAMP)
	
		inline half3 frag_shade_kawaflt_ramp_apply(half uv) {
			uv = pow(uv, _Sh_KwshrvRmp_Pwr);
			uv = uv * uv * (3.0 - 2.0 * uv); // Cubic Hermite H01 interoplation
			return UNITY_SAMPLE_TEX2D(_Sh_KwshrvRmp_Tex, half2(uv, uv)).rgb;
		}

		inline half3 frag_shade_kawaflt_ramp_forward_main(FRAGMENT_IN i, half3 normal) {
			half light_atten = frag_shade_kawaflt_attenuation_no_shadow(i.pos_world.xyz);

			half shadow_atten = UNITY_SHADOW_ATTENUATION(i, i.pos_world.xyz);
			float3 wsld = normalize(UnityWorldSpaceLightDir(i.pos_world.xyz));
			half ramp_uv = dot(normal, wsld) * 0.5 + 0.5;
			half3 shade_blended = frag_shade_kawaflt_ramp_apply(ramp_uv * shadow_atten);
			half3 shade_separated = frag_shade_kawaflt_ramp_apply(ramp_uv) * shadow_atten;
			half3 shade = lerp(shade_separated, shade_blended, _Sh_Kwshrv_ShdBlnd);

			return _LightColor0.rgb * max(0.0h, light_atten * shade);
		}

	#endif

	#if defined(SHADE_KAWAFLT_SINGLE)
	
		inline half3  frag_shade_kawaflt_single_forward_main(FRAGMENT_IN i, half3 normal) {
			half light_atten = frag_shade_kawaflt_attenuation_no_shadow(i.pos_world.xyz);

			half shadow_atten = UNITY_SHADOW_ATTENUATION(i, i.pos_world.xyz);
			float3 dir = normalize(UnityWorldSpaceLightDir(i.pos_world.xyz));
			half tangency = dot(normal, dir);
			half shade = shade_kawaflt_single(tangency, shadow_atten);

			return _LightColor0.rgb * max(0.0h, light_atten * shade);
		}

	#endif





#endif

#endif // KAWAFLT_FRAG_SHARED_INCLUDED