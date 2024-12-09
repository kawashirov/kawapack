#if !defined(KAWA_RS_SHARED_INCLUDED)
	#define KAWA_RS_SHARED_INCLUDED

#if defined(LIGHTMAP_ON)
	#error No support for LIGHTMAP_ON
#endif

#if defined(DYNAMICLIGHTMAP_ON)
	#error No support for DYNAMICLIGHTMAP_ON
#endif


#if defined(SIDES_TWO) || defined(SIDES_FOUR) || defined(SIDES_EIGHT)
	#define SIDES_MULTI
#endif

#if defined(SIDES_ONE) || defined(SIDES_MULTI)
	#define SIDES_ON
#endif

#if defined(SIDES_ONE) || defined(SIDES_OFF)
	UNITY_DECLARE_TEX2D(_TexFront);
	// No get_tex_id
	// No sample_tex
#endif

#if defined(SIDES_TWO)
	UNITY_DECLARE_TEX2D(_TexFront); // #0
	UNITY_DECLARE_TEX2D(_TexBack); // #1

	inline float get_tex_id(float angle) {
		return (abs(angle) > UNITY_PI/2) ? 1 : 0;
	}

	inline float4 sample_tex(uint tex_id, float2 uv) {
		float4 smpl = float4(1,0,1,0);
		switch(tex_id) {
			case 0: smpl = UNITY_SAMPLE_TEX2D(_TexFront, uv); break;
			case 1: smpl = UNITY_SAMPLE_TEX2D(_TexBack, uv); break;
		}
		return smpl;
	}

#endif

#if defined(SIDES_FOUR)
	UNITY_DECLARE_TEX2D(_TexFront); // #0
	UNITY_DECLARE_TEX2D(_TexRight); // #1
	UNITY_DECLARE_TEX2D(_TexBack); // #2
	UNITY_DECLARE_TEX2D(_TexLeft); // #3

	inline float get_tex_id(float angle) {
		float tex_id = -1;
		if ( abs(angle) < UNITY_PI/4 ) {
			tex_id = 0; // Front
		}
		if ( UNITY_PI/4 < abs(angle) && abs(angle) < UNITY_PI*3/4 ) {
			tex_id = (angle > 0) ? 1 : 3; // Right/Left
		}
		if ( UNITY_PI*3/4 < abs(angle) ) {
			tex_id = 2; // Back
		}
		return tex_id;
	}

	inline float4 sample_tex(uint tex_id, float2 uv) {
		float4 smpl = float4(1,0,1,0);
		switch(tex_id) {
			case 0: smpl = UNITY_SAMPLE_TEX2D(_TexFront, uv); break;
			case 1: smpl = UNITY_SAMPLE_TEX2D(_TexRight, uv); break;
			case 2: smpl = UNITY_SAMPLE_TEX2D(_TexBack, uv); break;
			case 3: smpl = UNITY_SAMPLE_TEX2D(_TexLeft, uv); break;
		}
		return smpl;
	}
#endif

#if defined(SIDES_EIGHT)
	UNITY_DECLARE_TEX2D(_TexFront); // #0
	UNITY_DECLARE_TEX2D(_TexFrontRight); // #1
	UNITY_DECLARE_TEX2D(_TexRight); // #2
	UNITY_DECLARE_TEX2D(_TexBackRight); // #3
	UNITY_DECLARE_TEX2D(_TexBack); // #4
	UNITY_DECLARE_TEX2D(_TexBackLeft); // #5
	UNITY_DECLARE_TEX2D(_TexLeft); // #6
	UNITY_DECLARE_TEX2D(_TexFrontLeft); // #7

	inline float get_tex_id(float angle) {
		float tex_id = -1;
		if ( abs(angle) < UNITY_PI/8 ) {
			tex_id = 0; // Front
		}
		if ( UNITY_PI/8 < abs(angle) && abs(angle) < UNITY_PI*3/8 ) {
			tex_id = (angle > 0) ? 1 : 7; // Front Right/Left
		}
		if ( UNITY_PI*3/8 < abs(angle) && abs(angle) < UNITY_PI*5/8 ) {
			tex_id = (angle > 0) ? 2 : 6; // Right/Left
		}
		if ( UNITY_PI*5/8 < abs(angle) && abs(angle) < UNITY_PI*7/8 ) {
			tex_id = (angle > 0) ? 3 : 5; // Back Right/Left
		}
		if ( UNITY_PI*7/8 < abs(angle) ) {
			tex_id = 4; // Back
		}
		return tex_id;
	}

	inline float4 sample_tex(uint tex_id, float2 uv) {
		float4 smpl = float4(1,0,1,0);
		switch(tex_id) {
			case 0: smpl = UNITY_SAMPLE_TEX2D(_TexFront, uv); break;
			case 1: smpl = UNITY_SAMPLE_TEX2D(_TexFrontRight, uv); break;
			case 2: smpl = UNITY_SAMPLE_TEX2D(_TexRight, uv); break;
			case 3: smpl = UNITY_SAMPLE_TEX2D(_TexBackRight, uv); break;
			case 4: smpl = UNITY_SAMPLE_TEX2D(_TexBack, uv); break;
			case 5: smpl = UNITY_SAMPLE_TEX2D(_TexBackLeft, uv); break;
			case 6: smpl = UNITY_SAMPLE_TEX2D(_TexLeft, uv); break;
			case 7: smpl = UNITY_SAMPLE_TEX2D(_TexFrontLeft, uv); break;
		}
		return smpl;
	}
#endif

uniform float4 _Color;
uniform float4 _Emission;

uniform uint _xtiles;
uniform uint _ytiles;
uniform uint _framerate;
uniform half _frame;


inline fixed4 LightingSprite (SurfaceOutput s, UnityGI gi) {
	fixed4 c;
	// fixed diff = max (0, dot (s.Normal, gi.light.dir));
	fixed diff = 1.0;
	c.rgb = s.Albedo * gi.light.color * diff;
	c.a = s.Alpha;
	#ifdef UNITY_LIGHT_FUNCTION_APPLY_INDIRECT
		c.rgb += s.Albedo * gi.indirect.diffuse;
	#endif
	return c;
}

inline void LightingSprite_GI (SurfaceOutput s, UnityGIInput data, inout UnityGI gi) { 
	// Based on:
	//		inline UnityGI UnityGlobalIllumination (UnityGIInput data, half occlusion, half3 normalWorld)
	//		inline UnityGI UnityGI_Base(UnityGIInput data, half occlusion, half3 normalWorld)
	// Don't even need SurfaceOutput s

	UnityGI o_gi;
	ResetUnityGI(o_gi);

	gi.light = data.light;
	gi.light.color *= data.atten;

	#if UNITY_SHOULD_SAMPLE_SH
		// It's like ShadeSH but w/o direction
		float3 light;
		light.r = unity_SHAr.w;
		light.g = unity_SHAg.w;
		light.b = unity_SHAb.w;

		#ifdef VERTEXLIGHT_ON
			// to light vectors
			float4 toLightX = unity_4LightPosX0 - data.worldPos.x;
			float4 toLightY = unity_4LightPosY0 - data.worldPos.y;
			float4 toLightZ = unity_4LightPosZ0 - data.worldPos.z;
			// squared lengths
			float4 lengthSq = 0;
			lengthSq += toLightX * toLightX;
			lengthSq += toLightY * toLightY;
			lengthSq += toLightZ * toLightZ;
			// don't produce NaNs if some vertex position overlaps with the light
			lengthSq = max(lengthSq, 0.000001);
			
			// attenuation
			float4 atten = 1.0 / (1.0 + lengthSq * unity_4LightAtten0);
			// final color
			light += unity_LightColor[0].rgb * atten.x;
			light += unity_LightColor[1].rgb * atten.y;
			light += unity_LightColor[2].rgb * atten.z;
			light += unity_LightColor[3].rgb * atten.w;
		#endif
		gi.indirect.diffuse = light;
	#endif
}

struct Input {
	float2 custom_uv;
	#if defined(SIDES_MULTI)
		// В surface режиме нельзя своботно управлять интерполяцией,
		// по этому uint нормально не получается использовать.
		float tex_id;
	#endif
};

void vert(inout appdata_base v, out Input o) {
	#if defined(SIDES_ON)
		float4 cameraPos;
		#if UNITY_SINGLE_PASS_STEREO
			// Get the position in between the two cameras if the viewer is in VR, otherwise get the position of the
			// camera. If you don't do this, the sprite will look very stereo-incorrect as it will be oriented toward
			// both eyes simultaneously
			cameraPos.xyz = unity_StereoWorldSpaceCameraPos[0] * 0.5 + unity_StereoWorldSpaceCameraPos[1] * 0.5;
			cameraPos.w = 1;
		#else
			cameraPos = mul(unity_CameraToWorld, float4(0,0,0,1));
		#endif

		cameraPos =  mul(unity_WorldToObject, cameraPos);

		// In object space, find the cos and sin of the angle between the camera's position and (0,0,1) in the xz plane
		// so we can rotate the plane to face the camera
		float2 sincosa = -normalize(cameraPos.xz);
		float4x4 rotation = float4x4(
			sincosa.y, 0, sincosa.x, 0,
			0, 1, 0, 0,
			-sincosa.x,	0,sincosa.y,0,
			0,0,0,1
		);

		// Rotate the vertices about the object origin to face the camera
		v.vertex = mul(rotation, v.vertex);
		v.normal = mul(rotation, float4(v.normal, 1)).xyz;
		#if defined(SIDES_MULTI)
			//Get the angle between the camera and (0,0,1) in the xz plane
			float angle = atan2(cameraPos.x, cameraPos.z);
			o.tex_id = get_tex_id(angle);
		#endif
	#endif
	
	//From the frame number, get the row and column of the frame on the sprite sheet.
	float frame_num = _Time[1]*_framerate + _frame;
	int2 frame;
	frame.x = floor(fmod(frame_num, _xtiles));
	frame.y = floor(fmod((frame_num/float(_xtiles)), _ytiles));
	float2 new_texcoord = v.texcoord.xy;
	new_texcoord.x = (v.texcoord.x + frame.x)/_xtiles;
	new_texcoord.y = (v.texcoord.y - frame.y)/_ytiles + (_ytiles - 1.0f)/_ytiles;
	o.custom_uv = new_texcoord;
}

void surf (in Input i, inout SurfaceOutput o) {
	float4 smpl;
	#if defined(SIDES_MULTI)
		uint tex_id = (uint) round(i.tex_id);
		smpl = sample_tex(tex_id, i.custom_uv);
	#else
		smpl = UNITY_SAMPLE_TEX2D(_TexFront, i.custom_uv);
	#endif

	smpl *= _Color;

	o.Albedo = smpl.rgb;
	o.Alpha = smpl.a;
	o.Emission = smpl.rgb * _Emission.rgb * _Emission.a;
}

#endif // KAWA_RS_SHARED_INCLUDED