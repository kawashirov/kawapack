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

inline void apply_bitloss(inout float value, uint multiplier, uint constant, bool apply_time) {
	#if defined(PRECISION_LOSS_ON)	
		uint value_uint = asuint(value);
		// _PrecLoss = 0.0;
		uint width = clamp(round(_PrecLoss), 0, 31);
		// Все линии ниже - одна bfi инструкция.
		// https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/bfi---sm5---asm-
		uint bitmask = (1 << width) - 1; // Нули слева, единицы справа.
		#if defined(PRECISION_LOSS_RANDOM)
			uint value_rnd = rnd_uint(multiplier, constant, apply_time);
			value_uint = (value_uint & ~bitmask) | (value_rnd & bitmask);
		#else
			value_uint &= ~bitmask;
		#endif
		value = asfloat(value_uint);
	#endif
}

inline void apply_bitloss2(inout float2 value, uint2 multiplier, uint2 constant, bool apply_time) {
	apply_bitloss(value.x, multiplier.x, constant.x, apply_time);
	apply_bitloss(value.y, multiplier.y, constant.y, apply_time);
}

inline void apply_bitloss3(inout float3 value, uint3 multiplier, uint3 constant, bool apply_time) {
	apply_bitloss(value.x, multiplier.x, constant.x, apply_time);
	apply_bitloss(value.y, multiplier.y, constant.y, apply_time);
	apply_bitloss(value.z, multiplier.z, constant.z, apply_time);
}

inline void apply_bitloss4(inout float4 value, uint4 multiplier, uint4 constant, bool apply_time) {
	apply_bitloss(value.x, multiplier.x, constant.x, apply_time);
	apply_bitloss(value.y, multiplier.y, constant.y, apply_time);
	apply_bitloss(value.z, multiplier.z, constant.z, apply_time);
	apply_bitloss(value.w, multiplier.w, constant.w, apply_time);
}

inline void apply_bitloss_frag(inout FRAGMENT_IN i, bool apply_time) {
	// apply_bitloss(i.pos, ...);
	// apply_bitloss2(i.uv0.xy, uint2(53579, 48502), uint2(42612, 36848), apply_time);
	apply_bitloss3(i.pos_world.xyz, uint3(54570, 42680, 59758), uint3(41909, 42442, 53100), apply_time);
	apply_bitloss3(i.normal_world.xyz, uint3(59758, 40055, 64819), uint3(50503, 35602, 42253), apply_time);
	#if defined(KAWAFLT_PASS_FORWARD)
		apply_bitloss3(i.tangent_world.xyz, uint3(63686, 64804, 62113), uint3(45232, 33406, 56733), apply_time);
		apply_bitloss3(i.bitangent_world.xyz, uint3(64960, 60585, 42288), uint3(50412, 59775, 34323), apply_time);
	#endif
}

#endif // KAWAFLT_FEATURE_PSX_INCLUDED