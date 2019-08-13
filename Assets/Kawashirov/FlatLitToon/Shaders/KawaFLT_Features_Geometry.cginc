#ifndef KAWAFLT_FEATURES_GEOMETRY_INCLUDED
#define KAWAFLT_FEATURES_GEOMETRY_INCLUDED

#include "KawaRND.cginc"

/* PolyColorWave features */

// (v.normalDir, v.posWorld) -> (v.posWorld, v.vertex, v.pos, v.normalDir, v.is_outline)
inline void outline_geometry_apply_offset(inout GEOMETRY_OUT v) {
	#if defined(KAWAFLT_PASS_FORWARD) && defined(OUTLINE_ON)
		half3 offset_normal = v.normalDir * (_outline_width * 0.01h);
		half3 offset_bias = KawaWorldSpaceViewDir(v.posWorld);
		offset_bias = normalize(offset_bias) * (_outline_bias * 0.01h);

		v.posWorld.xyz = v.posWorld.xyz + offset_normal - offset_bias;
		v.vertex.xyz = mul(unity_WorldToObject, float4(v.posWorld.xyz, 1)).xyz;
		v.pos = UnityObjectToClipPos(v.vertex);
		v.normalDir = -v.normalDir;
		v.is_outline = true; 
	#endif
}


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


// (OUT[i].uv0, OUT[i].uv1, OUT[i].vertex, rnd) -> (OUT[i].pcwColor, rnd)
inline void pcw_geometry_out(inout GEOMETRY_OUT OUT[3], inout uint rnd) {
	#if defined(PCW_ON)
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
			OUT[j].pcwColor = final_color;
		}
	#endif
}


/* Infinity War features */
// (IN[i].vertex, rnd) -> (IN[i].vertex, OUT[i].dsntgrtFactor, rnd, dropFace)
inline void dsntgrt_geometry(inout GEOMETRY_IN IN[3], inout GEOMETRY_OUT OUT[3], inout uint rnd, inout bool dropFace) {
	// Geometry stage code about Infinity War encapsulated here
	#if defined(DSNTGRT_ON)
		float3 pos_mid = (IN[0].vertex.xyz + IN[1].vertex.xyz + IN[2].vertex.xyz) / 3.0;

		_Dsntgrt_Plane.xyz = normalize(_Dsntgrt_Plane.xyz);
		float3 plane_normal = _Dsntgrt_Plane.xyz;
		float plane_distance_mid = max(0, dot(float4(pos_mid, 1.0f), _Dsntgrt_Plane));

		float3 offset_normal = lerp(plane_normal, rnd_next_direction3(rnd), _Dsntgrt_TriSpreadRandomness);
		// polynomial y = x^2 * a + x * b + c = x * (x * a + b) + c;  
		float offset_ammount = plane_distance_mid * (plane_distance_mid * _Dsntgrt_TriSpreadAccel + _Dsntgrt_TriSpreadSpeed);
		offset_normal = offset_normal * offset_ammount;
		
		// Фактор сжатия считается от сердней точки, т.к. если считать для вершин,
		// то из-за ускорения может начаться растяжение треугольника, а не сжатие
		float factor_compress = saturate(_Dsntgrt_TriDecayFar > 0.001f ? plane_distance_mid / _Dsntgrt_TriDecayFar : 1.001f);

		if (factor_compress > 0.999f) {
			// Если фактор стал больше единицы, значит вертексы сожмутся в точку и фейс не будет нужен
			//dropFace = true;
		}

		UNITY_UNROLL for (int j2 = 2; j2 >= 0; j2--) {
			// Фактор потемнения считаетс яот вершин, что бы обеспечить плавность.
			float plane_distance_v = dot(float4(IN[j2].vertex.xyz, 1.0), _Dsntgrt_Plane);
			float factor_tint = _Dsntgrt_TriTintFar > 0.001f ? saturate(plane_distance_v / _Dsntgrt_TriTintFar) : 1.001f;
			factor_tint = factor_tint * factor_tint * (3.0f - 2.0f * factor_tint); // H01
			OUT[j2].dsntgrtFactor = factor_tint;

			IN[j2].vertex.xyz = lerp(IN[j2].vertex.xyz, pos_mid.xyz, factor_compress) + offset_normal;
		}
	#endif
}

/* General */

inline void geom_proxy_shadowcaster_nopos(in v2g v, inout g2f o) {
	// В рот ебал разрабов Юнити
	// Проксируем хуёвый макрос в функцию, потому что он не работает если g2f не называется `v`, макрос требует `v.vertex` и `v.normal`
	// Пересчёт `pos` нужен для того, что в режиме SHADOWS_DEPTH сдвинуть объекты по Z оси пространства экрана, что бы приминить unity_LightShadowBias.
	#if defined(KAWAFLT_PASS_SHADOWCASTER)
		TRANSFER_SHADOW_CASTER_NOPOS(o, o.pos);
	#endif
}

inline void geom_proxy_calc_shadow(in v2g v, inout g2f o) {
	// В рот ебал разрабов Юнити
	// Проксируем хуёвый макрос в функцию, потому что он не работает если v2f/g2f не называется `v`, макрос требует `v.vertex`
	#if defined(KAWAFLT_PASS_FORWARD)
		TRANSFER_SHADOW(o);
	#endif // KAWAFLT_PASS_FORWARD
}



#endif // KAWAFLT_FEATURES_GEOMETRY_INCLUDED