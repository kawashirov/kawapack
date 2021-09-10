#ifndef KAWAFLT_FEATURES_GEOMETRY_INCLUDED
#define KAWAFLT_FEATURES_GEOMETRY_INCLUDED

#include "KawaRND.cginc"

/* Offset feature */

// (v_out.pos_world, v_out.normal_world) -> (v_out.pos_world, v_out.normal_world, v_out.is_outline)
inline void outline_geometry_apply_offset(inout GEOMETRY_OUT v_out, bool is_outline) {
	// Меняет GEOMETRY_IN прежде, чем другие моды начнут что-то делать.
	#if defined(KAWAFLT_PASS_FORWARD) && defined(OUTLINE_ON)
		if (is_outline) {
			half3 wsvd = normalize(KawaWorldSpaceViewDir(v_out.pos_world));
			half3 offset_world = v_out.normal_world * (_outline_width * 0.01h) - wsvd * (_outline_bias * 0.01h);
			v_out.pos_world.xyz += offset_world;
			v_out.normal_world *= -1;
		}
		v_out.is_outline = is_outline;
	#endif
}


/* PolyColorWave feature */

#if defined(PCW_ON)
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



#endif // KAWAFLT_FEATURES_GEOMETRY_INCLUDED