#ifndef KAWAFLT_FEATURES_LIGHTWEIGHT_INCLUDED
#define KAWAFLT_FEATURES_LIGHTWEIGHT_INCLUDED

/* KawaFLT */


/* General */

// (o.pos) -> (o.pos_screen)
inline void screencoords_fragment_in(inout FRAGMENT_IN o) {
	#if defined(RANDOM_MIX_COORD) || defined(RANDOM_SEED_TEX)
		o.pos_screen = ComputeScreenPos(o.pos);
	#endif
}


#endif // KAWAFLT_FEATURES_LIGHTWEIGHT_INCLUDED