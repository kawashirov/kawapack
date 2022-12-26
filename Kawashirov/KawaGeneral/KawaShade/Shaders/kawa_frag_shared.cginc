#ifndef KAWAFLT_FRAG_SHARED_INCLUDED
#define KAWAFLT_FRAG_SHARED_INCLUDED

#include "UnityLightingCommon.cginc"
#include "UnityStandardUtils.cginc"

#include ".\kawa_feature_fps.cginc"
#include ".\kawa_feature_white_noise.cginc"

#include ".\kawa_feature_poly_color_wave.cginc"
#include ".\kawa_feature_outline.cginc"

#include ".\kawa_feature_infinity_war.cginc"

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
	if (!is_outline_colored(i)) {
		// Пропуск, если всёравно будет перекрашено.
		#if defined(AVAILABLE_MAINTEX)
			#if defined(MAINTEX_SEPARATE_ALPHA)
				color.rgb = UNITY_SAMPLE_TEX2D(_MainTex, texST).rgb;
				color.a = UNITY_SAMPLE_TEX2D_SAMPLER(_MainTexAlpha, _MainTex, texST).r;
			#else
				color = UNITY_SAMPLE_TEX2D(_MainTex, texST);
			#endif
			#if defined(AVAILABLE_COLORMASK)
				half mask = UNITY_SAMPLE_TEX2D_SAMPLER(_ColorMask, _MainTex, texST).r;
				color = lerp(color, color * _Color, mask);
			#else
				color *= _Color;
			#endif
		#else
			color = _Color;
		#endif

		color.rgb = wnoise_mix(color.rgb, i, false, rnd);
		color.rgb = fps_mix(color.rgb);
		color.rgb = pcw_mix(color.rgb, i, false);
		color = iwd_mix_albedo(color, i);
	}
	color.rgb = outline_mix(color.rgb, i);
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
#endif

#endif // KAWAFLT_FRAG_SHARED_INCLUDED