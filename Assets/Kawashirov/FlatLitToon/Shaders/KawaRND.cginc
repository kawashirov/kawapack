#ifndef KAWARND_INCLUDED
#define KAWARND_INCLUDED


/* Hashes */

inline uint kawahash_1a(uint seed) {
	// Быстрый хеш для одного значения
	// Не очень качественный, но для инициализации рандома сойдет.
	// Регистры: 1 (3 компоненты)
	// Инструкции: xor, imad
	uint3 a = seed ^ uint3(0x0c49804e, 0x8e841cea, 0x4ca64728);
	return mad(a.x, a.y, a.z);
}

inline uint2 kawahash_2a(uint2 seed) {
	// Не очень качественный, но для инициализации рандома сойдет.
	// Быстрый хеш для двух значений, хуже чем два по kawahash_1a, но использует то же кол-во инструкций и регистров.
	// Регистры: 1 (4 компоненты)
	// Инструкции: xor, imad
	uint4 a = seed.xyxy ^ uint4(0x48290df2, 0x6c163a2b, 0x875d5d20, 0x9a73e87c);
	return mad(seed, a.xy, a.zw);
}

inline uint4 kawahash_4a(uint4 seed) {
	// Сложно назвать хешем, соль для инициализаций.
	// Быстрый хеш для двух значений
	// Регистры: 1 (4 компоненты)
	// Инструкции: xor
	return seed ^ uint4(0x9c40919c, 0x5bf1ef3f, 0x7f065408, 0x6406cb82);
}

/* Random One */

inline void rnd_next(inout uint seed) {
	seed = kawahash_1a(seed);
}

#define __KAWARND_UINT_TO_FLOAT(value) (value * (1.0 / 4294967296.0));

inline uint rnd_from_uint(uint u1) {
	return kawahash_1a(u1);
}

inline uint rnd_from_float(float f1) {
	return rnd_from_uint(asuint(f1));
}

inline uint rnd_from_float2(float2 f2) {
	uint rnd = asuint(f2.x);
	rnd = kawahash_1a(rnd);
	rnd += asuint(f2.y);
	rnd = kawahash_1a(rnd);
	return rnd;
}

inline uint rnd_from_float2x3(float2 f1, float2 f2, float2 f3) {
	uint2 rnd = mad(asuint(f1), asuint(f2), asuint(f3));
	rnd = kawahash_1a(rnd.y + kawahash_1a(rnd.x));
	return rnd;
}

inline uint rnd_fork(uint rnd, uint seed) {
	rnd += seed;
	rnd = kawahash_1a(rnd);
	return rnd;
}

inline float rnd_next_float_01(inout uint rnd) {
	float float01 = __KAWARND_UINT_TO_FLOAT(float(rnd));
	rnd = kawahash_1a(rnd);
	return float01;
}

inline float2 rnd_next_float2_01(inout uint rnd) {
	uint2 ret;
	ret.x = rnd;
	rnd = kawahash_1a(rnd);
	ret.y = rnd;
	rnd = kawahash_1a(rnd);
	return __KAWARND_UINT_TO_FLOAT(float2(ret));
}

inline float3 rnd_next_float3_01(inout uint rnd) {
	uint3 ret;
	ret.x = rnd;
	rnd = kawahash_1a(rnd);
	ret.y = rnd;
	rnd = kawahash_1a(rnd);
	ret.z = rnd;
	rnd = kawahash_1a(rnd);
	return __KAWARND_UINT_TO_FLOAT(float3(ret));
}

inline float2 rnd_circle(inout uint rnd) {
	// uniformly random point within a circle
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