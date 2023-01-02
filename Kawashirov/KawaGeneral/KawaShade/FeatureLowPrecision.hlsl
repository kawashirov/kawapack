#ifndef KAWAFLT_FEATURE_PSX_INCLUDED
#define KAWAFLT_FEATURE_PSX_INCLUDED

/*
	Low precision features
*/

#if defined(PSX_ON)
	uniform float _PSX_SnapScale;
#endif

#if defined(PRECISION_LOSS_ON)
	uniform float _PrecLoss;
#endif

// v.pos -> v.pos
inline void psx_prefrag(inout FRAGMENT_IN v) { 
	// PSX Snapping
	#if defined(PSX_ON)
		float4 sv_pos = v.pos;
		sv_pos.xy /= sv_pos.w;
		float3 scaler;
		scaler.xy = _ScreenParams.xy;
		scaler.z = dot(_ScreenParams.xy, float2(0.5f, 0.5f)); // Среднее
		scaler = scaler / (_PSX_SnapScale * 2);
		sv_pos.xy = floor(scaler.xy * sv_pos.xy) / scaler;
		sv_pos.xy *= sv_pos.w;
		// Решил пока z не трогать, там пиздец.
		v.pos = sv_pos;
		// Следует ли восстанавливать world и object из экрна?
		// Изменения не должны быть сильные, так что пока не буду.
	#endif
}

inline void apply_bitloss(inout float value) {
	uint value_uint = asuint(value);
	#if defined(PRECISION_LOSS_ON)
		uint width = clamp(round(_PrecLoss), 0, 23);
		// Все линии ниже - одна bfi инструкция.
		// https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/bfi---sm5---asm-
		uint bitmask = (1 << width) - 1;
		value_uint &= ~bitmask;
	#endif
	value = asfloat(value_uint);
}

inline void apply_bitloss(inout float2 value) {
	apply_bitloss(value.x);
	apply_bitloss(value.y);
}

inline void apply_bitloss(inout float3 value) {
	apply_bitloss(value.x);
	apply_bitloss(value.y);
	apply_bitloss(value.z);
}

inline void apply_bitloss(inout float4 value) {
	apply_bitloss(value.x);
	apply_bitloss(value.y);
	apply_bitloss(value.z);
	apply_bitloss(value.w);
}

inline void apply_bitloss_vertin(inout VERTEX_IN i) {
	apply_bitloss(i.vertex);
	apply_bitloss(i.tangent);
	apply_bitloss(i.normal);
	apply_bitloss(i.texcoord);
	apply_bitloss(i.texcoord1);
	apply_bitloss(i.texcoord2);
	apply_bitloss(i.texcoord3);
	//apply_bitloss(i.color); // Не используется
}

inline void apply_bitloss_frag(inout FRAGMENT_IN i) {
	// apply_bitloss(i.pos);
	apply_bitloss(i.uv0);
	apply_bitloss(i.pos_world);
	apply_bitloss(i.normal_world);
	#if defined(KAWAFLT_PASS_FORWARD)
		apply_bitloss(i.tangent_world);
		apply_bitloss(i.bitangent_world);
	#endif
}

#endif // KAWAFLT_FEATURE_PSX_INCLUDED