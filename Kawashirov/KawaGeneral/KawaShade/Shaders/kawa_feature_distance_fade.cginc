#ifndef KAWAFLT_FEATURE_DSTFD_INCLUDED
#define KAWAFLT_FEATURE_DSTFD_INCLUDED

/*
	Distance Fade features
*/

#define DSTFD_RND_M 36179
#define DSTFD_RND_C 34836

#if defined(DSTFD_ON)
	uniform float _DstFd_Near;
	uniform float _DstFd_AdjustPower;
	uniform float4 _DstFd_Axis;

	#if defined(DSTFD_RANGE)
		uniform float _DstFd_Far;
	#endif

	#if defined(DSTFD_INFINITY)
		// uniform float _DstFd_Far;
		uniform float _DstFd_AdjustScale;
	#endif
#endif

#if defined(FRAGMENT_IN)
	// (o.pos_world) -> (o.dstfd_distance)
	inline void dstfade_frament_in(inout FRAGMENT_IN o) {
		#if defined(DSTFD_ON)
			o.dstfd_distance = length(KawaWorldSpaceViewDir(o.pos_world.xyz) * _DstFd_Axis.xyz);
		#endif
	}
	
	inline void dstfd_frag_clip(inout FRAGMENT_IN i, uint rnd) {
		#if defined(DSTFD_ON)
			// Равномерный рандом от 0 до 1
			rnd = rnd_apply_time(rnd * DSTFD_RND_M + DSTFD_RND_C);
			half rnd_01 = rnd_next_float_01(rnd); 

			half clip_v;
			#if defined(DSTFD_RANGE)
				half rnd_nonlin = pow(rnd_01, _DstFd_AdjustPower);
				half dist = lerp(_DstFd_Near, _DstFd_Far, rnd_nonlin);
				clip_v = dist - i.dstfd_distance;
			#elif defined(DSTFD_INFINITY)
				half rnd_nonlin = pow((1.0h - rnd_01) / rnd_01, 1.0h / _DstFd_AdjustPower) * _DstFd_AdjustScale;
				half dist = rnd_nonlin + _DstFd_Near;
				clip_v = dist - i.dstfd_distance;
			#endif
			clip(clip_v * _DstFd_Axis.w);
		#endif
	}
#endif // defined(FRAGMENT_IN)

#endif // KAWAFLT_FEATURE_DSTFD_INCLUDED