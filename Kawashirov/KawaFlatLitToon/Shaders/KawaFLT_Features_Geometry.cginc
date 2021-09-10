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


#endif // KAWAFLT_FEATURES_GEOMETRY_INCLUDED