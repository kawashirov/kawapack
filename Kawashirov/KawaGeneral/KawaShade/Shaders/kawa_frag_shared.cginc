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

inline float2 frag_applyst(float2 uv) {
	//#if defined(AVAILABLE_ST)
		//uv = TRANSFORM_TEX(uv, _MainTex /* _ST */);
	//#endif
	return uv;
}

#define ALPHATEST_RND_M 25598
#define ALPHATEST_RND_C 11497

inline void frag_alphatest(FRAGMENT_IN i, uint rnd, inout half alpha) {
	half cutoff = 0;

	#if defined(CUTOFF_CLASSIC)
		cutoff = _Cutoff;
	#elif defined(CUTOFF_RANGE)
		half spread = 0.5h;
		#if defined(CUTOFF_RANDOM)
			rnd = rnd_apply_time(rnd * ALPHATEST_RND_M + ALPHATEST_RND_C);
			spread = rnd_next_float_01(rnd);
		#elif
			// half spread = // TODO BAYER
		#endif
		#if defined(CUTOFF_H01)
			spread = smoothstep(0, 1, spread);
		#endif
		cutoff = lerp(_CutoffMin, _CutoffMax, spread);
	#endif
	
	#if defined(CUTOFF_ON)
		if (alpha < cutoff)
			discard;
		#if defined(CUTOFF_REMAP)
			alpha = (alpha - _Cutoff) / (1.0 - _Cutoff);
		#endif
	#endif
}

inline void frag_cull(FRAGMENT_IN i) {
	#if defined(NEED_CULL) && defined(KAWAFLT_PIPELINE_VF)
		// Без геометри стейджа у нас нет возможность сбрасывать примитивы, по этому сбрасываем фрагменты
		if (i.cull) discard;
	#endif
}

inline half4 frag_forward_get_albedo(FRAGMENT_IN i, float2 texST) {
	half4 albedo;
	
	// Пропуск, если всёравно будет перекрашено.
	#if defined(AVAILABLE_MAINTEX)
		#if defined(MAINTEX_SEPARATE_ALPHA)
			albedo.rgb = UNITY_SAMPLE_TEX2D(_MainTex, texST).rgb;
			albedo.a = UNITY_SAMPLE_TEX2D_SAMPLER(_MainTexAlpha, _MainTex, texST).r;
		#else
			albedo = UNITY_SAMPLE_TEX2D(_MainTex, texST);
		#endif
		#if defined(AVAILABLE_COLORMASK)
			half mask = UNITY_SAMPLE_TEX2D_SAMPLER(_ColorMask, _MainTex, texST).r;
			albedo = lerp(albedo, albedo * _Color, mask);
		#else
			albedo *= _Color;
		#endif
	#else
		albedo = _Color;
	#endif
	return albedo;
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