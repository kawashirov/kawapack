Shader "Kawashirov/UndertaleDeath"
{
	Properties {
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Color Tint", Color) = (1,1,1,1)
		// _Cutoff ("Cutout", Float) = 0.5

		_TessH ("Tess Horizontal", Float) = 3.0
		_TessV ("Tess Vertical", Float) = 3.0

		_DecayHeight ("Decay Height", Float) = 3

		_FadeDistance ("Fade Distance", Float) = 1
		_FadePower ("Fade Power", Float) = 2

		_OffsetVector ("Offset Vector", Vector) = (0,1,1,1)
		_OffsetSpeed ("Offset Speed", Float) = 0.1


	}

	SubShader {
		Tags {
			"RenderType"="Transparent"
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"ForceNoShadowCasting"="True"
			"DisableBatching"="True"
		}

		Pass {
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off

			CGPROGRAM

			// #pragma enable_d3d11_debug_symbols

			#pragma vertex vert
			#pragma hull hull
			#pragma domain domain
			#pragma geometry geom
			#pragma fragment frag

			#pragma target 5.0
			
			#include "UnityCG.cginc"

			struct appdata {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2t {
				float2 uv : TEXCOORD0;
				float4 objPos : OBJPOS;
				float2 tf : TESS_FACTORS;
			};

			struct t2g {
				float2 uv : TEXCOORD0;
				float4 objPos : OBJPOS;
				float rnd : RANDOM;
			};

			struct g2f {
				float2 uv : TEXCOORD0;
				float4 pos : SV_Position;
				float fade : FADE;
			};

			struct tess {
				float inside[2] : SV_InsideTessFactor;
				float edge[4] : SV_TessFactor;
			};

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;
			uniform float4 _Color;
			uniform float _FadeDistance;
			uniform float _FadePower;
			uniform float4 _OffsetVector;
			uniform float _OffsetSpeed;
			// uniform float _Cutoff;
			uniform float _TessH;
			uniform float _TessV;
			// uniform float4 _TessE;
			// uniform float4 _TessI;
			uniform float _DecayHeight;

			//
			// --- рандом ---

			inline uint kawahash_1a(uint seed) {
				// Быстрый хеш для одного значения
				// Не очень качественный, но для инициализации рандома сойдет.
				// Регистры: 1 (3 компоненты)
				// Инструкции: xor, imad
				uint3 a = seed ^ uint3(0x0c49804e, 0x8e841cea, 0x4ca64728);
				return mad(a.x, a.y, a.z);
			}

			#define __KAWARND_UINT_TO_FLOAT(value) (value * (1.0 / 4294967296.0));
			// #define __KAWARND_UINT_TO_FLOAT(value) ( cos(asfloat(value) + sin(asfloat(value))) );
			// #define __KAWARND_UINT_TO_FLOAT(value) ( frac(asfloat(value)) );

			inline float2 rnd_next_float2_01(inout uint rnd) {
				uint2 ret;
				ret.x = rnd;
				rnd = kawahash_1a(rnd);
				ret.y = rnd;
				rnd = kawahash_1a(rnd);
				return __KAWARND_UINT_TO_FLOAT(float2(ret));
			}

			//
			// --- стейджы ---
			
			v2t vert(appdata v) {
				v2t o;
				o.objPos = v.vertex;
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.tf = float2(_TessV, _TessH);
				return o;
			}

			tess hullconst(InputPatch<v2t, 4> v) {
				tess o;
				float2 tf;
				// Я не знаю почему, но здесь должны быть вычисления, иначе не работает.
				// Нельзя просто так взять и использовать _TessV и _TessH.
				tf.x = v[0].tf.x * 0.5f + v[1].tf.x * 0.5f;
				tf.y = v[2].tf.y * 0.5f + v[3].tf.y * 0.5f;
				o.inside[0] = tf.x;
				o.inside[1] = tf.y;
				o.edge[0] = tf.y;
				o.edge[1] = tf.x;
				o.edge[2] = tf.y;
				o.edge[3] = tf.x;

				return o;
			}

			[domain("quad")]
			[partitioning("integer")]
			[outputtopology("triangle_cw")]
			[outputcontrolpoints(32)]
			[patchconstantfunc("hullconst")]
			v2t hull(InputPatch<v2t, 4> v, uint id : SV_OutputControlPointID) {
				return v[id];
			}

			[domain("quad")]
			t2g domain(tess tf, const OutputPatch<v2t, 4> vi, float2 loc : SV_DomainLocation) {
				t2g o;
				o.objPos = lerp(lerp(vi[0].objPos, vi[1].objPos, loc.x), lerp(vi[3].objPos, vi[2].objPos, loc.x), loc.y);
				o.uv = lerp(lerp(vi[0].uv, vi[1].uv, loc.x), lerp(vi[3].uv, vi[2].uv, loc.x), loc.y);
				uint rnd = 0x19bef9;
				uint3 rnd_t; // времянка
				rnd += asuint(loc.x);
				rnd_t = rnd ^ uint3(0x125a37, 0xffbed, 0x1ab5e5);
				rnd = mad(rnd_t.x, rnd_t.y, rnd_t.z);
				rnd += asuint(loc.y);
				rnd_t = rnd ^ uint3(0x10d11f, 0x168fd3, 0x1ce2b9);
				rnd = mad(rnd_t.x, rnd_t.y, rnd_t.z);
				o.rnd = asfloat(rnd);
				return o;
			}

			[maxvertexcount(3)]
			void geom(triangle t2g IN[3], inout TriangleStream<g2f> tristream) {
				g2f o;

				//
				// --- вычисление ориджина квада ---

				float4 len = float4(0,0,0,0);
				len.x = distance(IN[0].objPos, IN[1].objPos);
				len.y = distance(IN[1].objPos, IN[2].objPos);
				len.z = distance(IN[2].objPos, IN[0].objPos);
				len.w = max(len.x, max(len.y, len.z));

				float4 anchor = float4(0,0,0,0);
				uint rnd;

				if ( len.x >= len.w ) {
					anchor = IN[0].objPos * 0.5f + IN[1].objPos * 0.5f;
					rnd = asuint(IN[0].rnd) + asuint(IN[1].rnd);
				}
				if ( len.y >= len.w ) {
					anchor = IN[1].objPos * 0.5f + IN[2].objPos * 0.5f;
					rnd = asuint(IN[1].rnd) + asuint(IN[2].rnd);
				}
				if ( len.z >= len.w ) {
					anchor = IN[2].objPos * 0.5f + IN[0].objPos * 0.5f;
					rnd = asuint(IN[2].rnd) + asuint(IN[0].rnd);
				}

				//
				// --- вычисление эффектов ---

				float effect_depth = max(0, anchor.y - _DecayHeight);
				
				// затухание
				float fade_value = 1.0 - saturate(effect_depth / _FadeDistance);
				fade_value = pow(fade_value, _FadePower);
				// Треугольник полностью прозрачный: мы можем остановиться здесь что бы разгрузить растризер.
				if (fade_value <= 0.0 ) return;

				// разлёт
				float offset_value = effect_depth * _OffsetSpeed;
				float offset_random = (rnd_next_float2_01(rnd) - 0.5f) * _OffsetVector.zw;
				float2 offset_vector = normalize(_OffsetVector.xy + offset_random) * offset_value;
				
				//
				// --- применение эффектов и эмиты ---
				o.fade = fade_value;

				// o.uv = IN[0].uv;
				// IN[0].objPos.xy += offset_vector;
				// o.pos = UnityObjectToClipPos(IN[i1].objPos.xyz);
				// tristream.Append(o);

				UNITY_UNROLL for(uint i1 = 0; i1 < 3; ++i1) {
					o.uv = IN[i1].uv;
					IN[i1].objPos.xy += offset_vector;
					o.pos = UnityObjectToClipPos(IN[i1].objPos.xyz);
					tristream.Append(o);
				}
			}

			float4 frag (g2f i) : SV_Target {
				float4 color = tex2D(_MainTex, i.uv) * _Color;
				color.a *= i.fade;
				return color;
				// return i.color;
			}

			ENDCG
		}
	}
}
