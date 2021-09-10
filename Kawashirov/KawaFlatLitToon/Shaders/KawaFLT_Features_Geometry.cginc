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


/* Infinity War features */
// (IN[i].vertex, rnd) -> (IN[i].vertex, OUT[i].iwd_tint, rnd, drop_face)
inline void iwd_geometry(inout GEOMETRY_IN IN[3], inout GEOMETRY_OUT OUT[3], inout uint rnd, inout bool drop_face) {
	// Обсчет в обджект-спейсе
	#if defined(IWD_ON)
		float3 pos_mid = (IN[0].vertex.xyz + IN[1].vertex.xyz + IN[2].vertex.xyz) / 3.0;

		_IWD_Plane.xyz = normalize(_IWD_Plane.xyz);
		float plane_distance_random = -rnd_next_float_01(rnd) * _IWD_PlaneDistRandomness;
		float plane_distance_mid = max(0, dot(float4(pos_mid, 1.0f), _IWD_Plane) + plane_distance_random);

		float3 offset = 0;
		if (plane_distance_mid > 0.001) {
			float wn = 0; // weights normalizer
			// Довольно встрато, т.к. коэфициентов 5, а не 4. Но компилятор должен заоптимизить.
			wn += _IWD_DirRandomWeight * _IWD_DirRandomWeight;
			wn += _IWD_DirPlaneWeight * _IWD_DirPlaneWeight;
			wn += _IWD_DirNormalWeight * _IWD_DirNormalWeight;
			wn += _IWD_DirObjectWeight * _IWD_DirObjectWeight;
			wn += _IWD_DirWorldWeight * _IWD_DirWorldWeight;
			wn = rsqrt(wn);

			float3 random_normal = rnd_next_direction3(rnd);
			float3 plane_normal = _IWD_Plane.xyz;
			float3 face_normal = normalize(IN[0].normal_obj + IN[1].normal_obj + IN[2].normal_obj);
			float3 object_normal = normalize(_IWD_DirObjectVector.xyz);
			float3 world_normal = normalize(UnityWorldToObjectDir(_IWD_DirWorldVector.xyz));

			float3 offset_dir = 0;
			// Здесь я тоже верю в компилятор
			offset_dir += random_normal * (_IWD_DirRandomWeight * wn);
			offset_dir += plane_normal * (_IWD_DirPlaneWeight * wn);
			offset_dir += face_normal * (_IWD_DirNormalWeight * wn);
			offset_dir += object_normal * (_IWD_DirObjectWeight * wn);
			offset_dir += world_normal * (_IWD_DirWorldWeight * wn);
			// Кстати, offset_dir не нормальный вектор, и это ОК.

			// polynomial y = x^2 * a + x * b + c = x * (x * a + b) + c;  
			float offset_ammount = plane_distance_mid * (plane_distance_mid * _IWD_MoveAccel + _IWD_MoveSpeed);
			offset = offset_dir * offset_ammount;
		}
		
		// Фактор сжатия считается от сердней точки, т.к. если считать для вершин,
		// то из-за ускорения может начаться растяжение треугольника, а не сжатие
		float factor_compress = saturate(_IWD_CmprssFar > 0.001f ? plane_distance_mid / _IWD_CmprssFar : 1.001f);

		if (factor_compress > 0.999f) {
			// Если фактор стал больше единицы, значит вертексы сожмутся в точку и фейс не будет нужен
			//drop_face = true;
		}

		UNITY_UNROLL for (int j2 = 2; j2 >= 0; j2--) {
			// Фактор потемнения считаетс яот вершин, что бы обеспечить плавность.
			float plane_distance_v = max(0, dot(float4(IN[j2].vertex.xyz, 1.0), _IWD_Plane) + plane_distance_random);
			float factor_tint = _IWD_TintFar > 0.001f ? saturate(plane_distance_v / _IWD_TintFar) : 1.001f;
			factor_tint = factor_tint * factor_tint * (3.0f - 2.0f * factor_tint); // H01
			OUT[j2].iwd_tint = factor_tint;

			IN[j2].vertex.xyz = lerp(IN[j2].vertex.xyz, pos_mid.xyz, factor_compress) + offset;
		}
	#endif
}

#endif // KAWAFLT_FEATURES_GEOMETRY_INCLUDED