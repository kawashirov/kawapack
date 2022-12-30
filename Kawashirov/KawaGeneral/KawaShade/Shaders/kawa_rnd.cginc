#ifndef KAWARND_INCLUDED
#define KAWARND_INCLUDED

inline uint rnd_init_noise_uint(in uint value) {
	uint rnd1 = 1;
	#if defined(RANDOM_SEED_TEX)
		uint2 size;
		_Rnd_Seed.GetDimensions(size.x, size.y);
		uint2 sc_mod;
		sc_mod.x = value % size.x;
		sc_mod.y = (value / size.x) % size.y;
		uint4 rnd4 = _Rnd_Seed.Load(uint3(sc_mod.x, sc_mod.y, 0));
		rnd1 = rnd4.x;
	#endif
	return rnd1;
}

inline uint rnd_init_noise_coords(uint2 coords) {
	uint rnd1 = 1;
	#if defined(RANDOM_SEED_TEX)
		uint2 size;
		_Rnd_Seed.GetDimensions(size.x, size.y);
		uint2 sc_mod = coords % size;
		uint4 rnd4 = _Rnd_Seed.Load(uint3(sc_mod.x, sc_mod.y, 0));
		rnd1 = rnd4.x;
	#endif
	return rnd1;
}

inline uint rnd_next_c(uint seed, uint c) {
	return seed * 134775813 + c;
}


inline void rnd_next(inout uint seed) {
	seed = rnd_next_c(seed, 1);
}

inline uint rnd_apply_uint(uint rnd, uint salt) {
	// Применяет соль к рандому, salt может быть 0.
	// Тоже, что и rnd_next, но с другой константой
	return rnd_next_c(rnd, salt);
}

inline uint rnd_apply_uint2(uint rnd, uint2 salt) {
	rnd = rnd_next_c(rnd, salt.x);
	rnd = rnd_next_c(rnd, salt.y);
	return rnd;
}

inline uint rnd_apply_uint3(uint rnd, uint3 salt) {
	rnd = rnd_next_c(rnd, salt.x);
	rnd = rnd_next_c(rnd, salt.y);
	rnd = rnd_next_c(rnd, salt.z);
	return rnd;
}

inline uint rnd_apply_uint4(uint rnd, uint4 salt) {
	rnd = rnd_next_c(rnd, salt.x);
	rnd = rnd_next_c(rnd, salt.y);
	rnd = rnd_next_c(rnd, salt.z);
	rnd = rnd_next_c(rnd, salt.w);
}

inline uint rnd_apply_time(uint rnd) {
	rnd = rnd * asuint(_SinTime.x) + asuint(_CosTime.x);
	return rnd;
}

inline float rnd_next_float_01(inout uint rnd) {
	float float01 = float(rnd) * (1.0 / 0xffffffff); // 1/(2^32-1) aka 1/4294967295
	rnd_next(rnd);
	return float01;
}

inline float2 rnd_next_float2_01(inout uint rnd) {
	return float2(rnd_next_float_01(rnd), rnd_next_float_01(rnd));
}

inline float3 rnd_next_float3_01(inout uint rnd) {
	return float3(rnd_next_float_01(rnd), rnd_next_float_01(rnd), rnd_next_float_01(rnd));
}

inline float3 rnd_next_direction3(inout uint rnd) {
	// Случайная точка на поверхности сферы
	float3 float01 = rnd_next_float3_01(rnd) * 2.0 - 1.0;
	float01 = float01 / cos(float01);
	return normalize(float01);
}

inline float2 rnd_next_direction2(inout uint rnd) {
	// Случайная точка на окружности
	float3 float01 = rnd_next_float3_01(rnd) * 2.0 - 1.0;
	float01 = float01 / cos(float01);
	return normalize(float01);
}

inline float2 rnd_next_disc2(inout uint rnd) {
	// Случайная точка внутри диска
	float a = rnd_next_float_01(rnd) * UNITY_TWO_PI;
	float r = sqrt(rnd_next_float_01(rnd));
	float2 sc;
	sincos(a, sc.x, sc.y);
	return sc * r;
}

/* Leagcy */

inline float rnd_cubedparadox(float2 uv0) {
	float2 node_8453_skew = uv0 + 0.2127+uv0.x*0.3713*uv0.y;
	float2 node_8453_rnd = 4.789*sin(489.123*(node_8453_skew));
	float node_8453 = frac(node_8453_rnd.x*node_8453_rnd.y*(1+node_8453_skew.x));
	return node_8453;
}

#endif // KAWARND_INCLUDED