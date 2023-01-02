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
				half3 wsvd = normalize(UnityWorldSpaceViewDir(v_out.pos_world));
				half3 offset_world = v_out.normal_world * (_outline_width * 0.01h) - wsvd * (_outline_bias * 0.01h);
				v_out.pos_world.xyz += offset_world;
				v_out.normal_world *= -1;
			}
			v_out.is_outline = is_outline;
		#endif
	}
#endif // defined(GEOMETRY_OUT)

inline void outline_apply_frag(inout half3 albedo, inout half3 emissive) {
	#if defined(KAWAFLT_PASS_FORWARD) && defined(OUTLINE_ON)
		UNITY_FLATTEN if(i.is_outline) {
			#if defined(OUTLINE_COLORED)
				albedo.rgb = _outline_color.rgb;
				emissive.rgb = half3(0,0,0); // TODO
			#else
				albedo *= _outline_color.rgb;
				emissive *= _outline_color.rgb;
			#endif
		}
	#endif
}

#endif // KAWA_FEATURE_OUTLINE_INCLUDED