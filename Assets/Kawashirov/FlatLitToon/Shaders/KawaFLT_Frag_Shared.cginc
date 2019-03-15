#ifndef KAWAFLT_FRAG_SHARED_INCLUDED
#define KAWAFLT_FRAG_SHARED_INCLUDED

#include "UnityLightingCommon.cginc"
#include "UnityStandardUtils.cginc"

// All users of this func should define NEED_SCREENPOS
inline float2 frag_pixelcoords(FRAGMENT_IN i) {
	float2 pxc = float2(0, 0);
	#if defined(NEED_SCREENPOS)
		pxc =  i.screenPos.xy / i.screenPos.w * _ScreenParams.xy;
	#endif
	return pxc;
}


/* Disintegration features */

inline void dsntgrt_frag_clip(inout FRAGMENT_IN i, inout uint rnd) {
	// #if defined(DSNTGRT_PIXEL)
	// 	half float01 = rnd_next_float_01(rnd);
	// 	half clip_val = lerp(
	// 		_Dsntgrt_FragDecayNear, _Dsntgrt_FragDecayFar, pow(float01, _Dsntgrt_FragPowerAdjust)
	// 	) + i.dsntgrtVertexRotated.x + _Dsntgrt_Plane.w;
	// 	clip(clip_val);
	// #endif
}

inline half4 dsntgrt_mix(half4 color, FRAGMENT_IN i) {
	#if defined(DSNTGRT_FACE)
		color = lerp(color, _Dsntgrt_Tint, i.dsntgrtFactor);
	#endif
	return color;
}


/* Distance Fade features */

inline void dstfd_frag_clip(inout FRAGMENT_IN i, uint rnd) {
	#if defined(DSTFD_ON)
		half rnd_01; // Равномерный рандом от 0 до 1
		#if defined(DSTFD_RANDOM_PIXEL)
			rnd = rnd_fork(rnd, asuint(_Time.y));
			rnd_01 = rnd_next_float_01(rnd);
		#elif defined(DSTFD_RANDOM_PATTERN)
			float2 pattren_uv = fmod(frag_pixelcoords(i), _DstFd_Pattern_TexelSize.zw) * _DstFd_Pattern_TexelSize.xy;
			rnd_01 = UNITY_SAMPLE_TEX2D(_DstFd_Pattern, pattren_uv).r;
		#endif

		half clip_v;
		#if defined(DSTFD_RANGE)
			half rnd_nonlin = pow(rnd_01, _DstFd_AdjustPower);
			half dist = lerp(_DstFd_Near, _DstFd_Far, rnd_nonlin);
			clip_v = dist - i.dstfdDistance;
		#elif defined(DSTFD_INFINITY)
			half rnd_nonlin = pow((1.0h - rnd_01) / rnd_01, 1.0h / _DstFd_AdjustPower) * _DstFd_AdjustScale;
			half dist = rnd_nonlin + _DstFd_Near;
			clip_v = dist - i.dstfdDistance;
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

inline half4 fps_mix(half4 color) {
	#if defined(FPS_ON)
		color *= lerp(_FPS_TLo, _FPS_THi, unity_DeltaTime.w / 91.0h);
	#endif
	return color;
}

/* PolyColorWave features */

inline half3 pcw_mix(half3 color, FRAGMENT_IN i, bool is_emission) {
	#if defined(PCW_ON)
		color = lerp(color, i.pcwColor.rgb, i.pcwColor.a * (is_emission ? _PCW_Em : (1.0 - _PCW_Em)));
	#endif
	return color;
}


inline float2 frag_applyst(float2 uv) {
	#if defined(AVAILABLE_ST)
		uv = TRANSFORM_TEX(uv, _MainTex /* _ST */);
	#endif
	return uv;
}

inline void frag_alphatest(FRAGMENT_IN i, inout uint rnd, in half alpha) {
	#if defined(CUTOFF_CLASSIC)
		clip(alpha - _Cutoff);
	#elif defined(CUTOFF_RANDOM) || defined(CUTOFF_PATTERN)
		float spread;
		#if defined(CUTOFF_RANDOM)
			spread = rnd_next_float_01(rnd);
		#elif defined(CUTOFF_PATTERN)
			float2 pattren_uv = fmod(frag_pixelcoords(i), _CutoffPattern_TexelSize.zw) * _CutoffPattern_TexelSize.xy;
			spread = UNITY_SAMPLE_TEX2D(_CutoffPattern, pattren_uv).r;
		#endif
		clip(alpha - lerp(_CutoffMin, _CutoffMax, spread));
	#endif
}

// All users og this func should define NEED_SCREENPOS and NEED_SCREENPOS_RANDOM
inline uint frag_rnd_screencoords(FRAGMENT_IN i) {
	#if defined(NEED_SCREENPOS_RANDOM)
		return rnd_from_float2(floor(frag_pixelcoords(i)));
	#else
		return rnd_from_uint(0);
	#endif
}

inline uint frag_rnd_time(FRAGMENT_IN i) {
	return rnd_from_float(_Time.y);
}

inline void frag_cull(FRAGMENT_IN i) {
	#if defined(NEED_CULL)
		if (i.cull) discard;
	#endif
}

inline half4 frag_forward_get_albedo(FRAGMENT_IN i, float2 texST) {
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

	color = fps_mix(color);
	color.rgb = pcw_mix(color.rgb, i, false); // Mix-in Poly Color Wave
	color = dsntgrt_mix(color, i);

	#if defined(TINTED_OUTLINE) || defined(COLORED_OUTLINE)
		UNITY_FLATTEN if(i.is_outline) {
			#if defined(COLORED_OUTLINE)
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
			i.normalDir = normalize(i.normalDir);
			half3x3 tangentTransform = half3x3(i.tangentDir, i.bitangentDir, i.normalDir);
			half3 bump = UnpackScaleNormal(UNITY_SAMPLE_TEX2D(_BumpMap, texST), _BumpScale);
			half3 normalDirection = normalize(mul(bump.rgb, tangentTransform)); // Perturbed normals
		#else
			half3 normalDirection = normalize(i.normalDir);
		#endif
		return normalDirection;
	}

	inline half frag_forward_get_light_attenuation(FRAGMENT_IN i) {
		UNITY_LIGHT_ATTENUATION(attenuation, i, i.posWorld.xyz);
		return attenuation;
	}

	#if defined(SHADE_CUBEDPARADOXFLT)
		// ???
	#endif

	#if defined(SHADE_KAWAFLT_DIFFUSE)

		inline half3 frag_shade_kawaflt_diffuse_round(half value) {
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

		inline half frag_shade_kawaflt_diffuse_rim_factor(half tangency) {
			return 1.0h + (pow(1.0h - abs(tangency), _Sh_Kwshrv_RimPwr) + _Sh_Kwshrv_RimBs) * _Sh_Kwshrv_RimScl;
		}

		inline float frag_shade_kawaflt_diffuse_smooth_tangency(float tangency) {
			return saturate(lerp(tangency, _Sh_Kwshrv_Smth_Tngnt, _Sh_Kwshrv_Smth));
		}

		inline half3 frag_shade_kawaflt_diffuse_steps(half3 color) {
			UNITY_BRANCH if ( _Sh_Kwshrv_FltFctr > 0.01 && _Sh_Kwshrv_BndSmth < 0.99 ) {
				// Only apply steps when flatness noticeble
				float luma = Luminance(color);
				float layers = luma;
				layers = log(layers) * _Sh_Kwshrv_FltLogSclA;
				layers = frag_shade_kawaflt_diffuse_round(layers);
				layers = exp(layers / _Sh_Kwshrv_FltLogSclA);
				color = lerp(color, color * (layers / luma), _Sh_Kwshrv_FltFctr);
			}
			return color;
		}

	#endif

	#if defined(SHADE_KAWAFLT_RAMP)
	
		inline half3 frag_shade_kawaflt_ramp_apply(half uv) {
			uv = pow(uv, _Sh_KwshrvRmp_Pwr);
			return UNITY_SAMPLE_TEX2D(_Sh_KwshrvRmp_Tex, half2(uv, uv)).rgb;
		}

	#endif


#endif

#endif // KAWAFLT_FRAG_SHARED_INCLUDED