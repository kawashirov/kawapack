#ifndef KAWA_FEATURE_OUTLINE_INCLUDED
#define KAWA_FEATURE_OUTLINE_INCLUDED

/*
	Outline features
*/

#if defined(KAWAFLT_PASS_FORWARD)
	#if !defined(OUTLINE_OFF)
		uniform float _outline_width;
		uniform float4 _outline_color;
		uniform float _outline_bias;
	#endif
#endif

#if defined(GEOMETRY_OUT)
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
#endif // defined(GEOMETRY_OUT)

#if defined(FRAGMENT_IN)

	inline bool is_outline_colored(FRAGMENT_IN i) {
		// Возвращает true, если фейс будет покрашен в кастомный цвет в outline_mix, позволяет избежать лишних операций.
		#if defined(KAWAFLT_PASS_FORWARD) && defined(OUTLINE_ON) && defined(OUTLINE_COLORED)
			return i.is_outline
		#else
			return false;
		#endif
	}

	inline half3 outline_mix(half3 color, FRAGMENT_IN i) {
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
#endif // defined(FRAGMENT_IN)

#endif // KAWA_FEATURE_OUTLINE_INCLUDED