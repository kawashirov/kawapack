#ifndef KAWA_FEATURE_PCW_INCLUDED
#define KAWA_FEATURE_PCW_INCLUDED

/*
	PolyColorWave features
*/

#define PCW_RND_SEED 22443
#if defined(PCW_ON)
	#define NEED_UV1
	uniform float _PCW_WvTmLo;
	uniform float _PCW_WvTmAs;
	uniform float _PCW_WvTmHi;
	uniform float _PCW_WvTmDe;
	uniform float4 _PCW_WvTmUV;
	uniform float4 _PCW_WvTmVtx;
	uniform float _PCW_WvTmRnd;
	uniform float _PCW_Em;
	uniform float4 _PCW_Color;
	uniform float _PCW_RnbwTm;
	uniform float _PCW_RnbwTmRnd;
	uniform float _PCW_RnbwStrtn;
	uniform float _PCW_RnbwBrghtnss;
	uniform float _PCW_Mix;

	inline float pcw_wave_intensity(float time_offset) {
		float4 period = _PCW_WvTmLo + _PCW_WvTmAs + _PCW_WvTmHi + _PCW_WvTmDe;
		float tp = frac((_Time.y + time_offset) / period) * period; // time in cycle from 0 to period
		if (tp < _PCW_WvTmLo)
			// Low phase
			return 0; 
		tp -= _PCW_WvTmLo;
		if (tp < _PCW_WvTmAs)
			// Ascent phase
			return 1.0 - (_PCW_WvTmAs - tp) / _PCW_WvTmAs;
		tp -= _PCW_WvTmAs;
		if (tp < _PCW_WvTmHi)
			// High phase
			return 1;
		tp -= _PCW_WvTmHi;
			// Descent phase
			return (_PCW_WvTmDe - tp) / _PCW_WvTmDe;
	}
#endif

#if defined(GEOMETRY_OUT)
	// (OUT[i].uv0, OUT[i].uv1, OUT[i].vertex, rnd) -> (OUT[i].pcw_color, rnd)
	inline void pcw_geometry_out(inout GEOMETRY_OUT OUT[3], inout uint rnd) {
		#if defined(PCW_ON) && defined(KAWAFLT_PASS_FORWARDBASE)
			// Используется только в базовом проходе
			// assuming NEED_UV1
			float2 uv0Mid = (OUT[0].uv0 + OUT[1].uv0 + OUT[2].uv0) / 3.0;
			float2 uv1Mid = (OUT[0].uv1 + OUT[1].uv1 + OUT[2].uv1) / 3.0;
			float4 vertexMid = (OUT[0].vertex + OUT[1].vertex + OUT[2].vertex) / 3.0;
			float rnd01 = rnd_next_float_01(rnd);
			float wave_offset =  dot(float4(uv0Mid, uv1Mid), _PCW_WvTmUV) + dot(vertexMid, _PCW_WvTmVtx) + rnd01 * _PCW_WvTmRnd;

			half hue = frac((_Time.y + rnd_next_float_01(rnd) * _PCW_RnbwTmRnd) / _PCW_RnbwTm);
			half3 rainbow_color = hsv2rgb(half3(hue, _PCW_RnbwStrtn, _PCW_RnbwBrghtnss));

			float wave = pcw_wave_intensity(wave_offset);
			half4 final_color = half4(lerp(_PCW_Color.rgb, rainbow_color, _PCW_Mix), _PCW_Color.a * wave);
			UNITY_UNROLL for (int j = 0; j < 3; j++) {
				OUT[j].pcw_color = final_color;
			}
		#endif
	}
#endif // defined(GEOMETRY_OUT)


#if defined(FRAGMENT_IN)
	inline half3 pcw_mix(half3 color, FRAGMENT_IN i, bool is_emission) {
		// Mix-in Poly Color Wave but only in BASE pass
		#if defined(PCW_ON) && defined(KAWAFLT_PASS_FORWARDBASE)
			color = lerp(color, i.pcw_color.rgb, i.pcw_color.a * (is_emission ? _PCW_Em : (1.0 - _PCW_Em)));
		#endif
		return color;
	}
#endif // defined(FRAGMENT_IN)

#endif // KAWA_FEATURE_PCW_INCLUDED