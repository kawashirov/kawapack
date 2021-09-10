#ifndef KAWAFLT_FEATURE_PSX_INCLUDED
#define KAWAFLT_FEATURE_PSX_INCLUDED

/*
	PSX Snapping features
*/

#if defined(PSX_ON)
	uniform float _PSX_SnapScale;
#endif

#if defined(FRAGMENT_IN)
	// v.pos -> v.pos
	inline void psx_prefrag(inout FRAGMENT_IN v) { 
		#if defined(PSX_ON)
			float4 sv_pos = v.pos;
			sv_pos.xy /= sv_pos.w;
			float2 scaler = _ScreenParams.xy / _PSX_SnapScale;
			sv_pos.xy = floor(scaler * sv_pos.xy) / scaler;
			sv_pos.xy *= sv_pos.w;
			v.pos = sv_pos;
			// Следует ли восстанавливать world и object из экрна?
			// Изменения не должны быть сильные, так что пока не буду.
		#endif
	}
#endif // defined(FRAGMENT_IN)

#endif // KAWAFLT_FEATURE_PSX_INCLUDED