Shader "Kawashirov/MaterialCombiner/BlitCopy" {
	Properties
	{
		_MainTex ("MainTex NOT USED", any) = "" {}

		_TexR ("_TexR", any) = "" {}
		_TexG ("_TexG", any) = "" {}
		_TexB ("_TexB", any) = "" {}
		_TexA ("_TexA", any) = "" {}
		_Channels ("_Channels", Vector) = (0, 1, 2, 3)
		// _ColorSpaces ("_ColorSpaces", Vector) = (0, 0, 0, 0)

		_Color("_Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_Scale("_Scale", Vector) = (1.0, 1.0, 1.0, 1.0)

		_ColorSpace ("_ColorSpace", Integer) = 0
		_BumpMode ("_BumpMode", Integer) = 0
		_ParallaxMode ("_ParallaxMode", Integer) = 0
		_ParallaxRef ("_ParallaxRef", Float) = 0.08
		_OcclusionMode ("_OcclusionMode", Integer) = 0
		
		_SourceRect ("_SourceRect", Vector) = (0, 0, 1, 1)
		_TargetRect ("_TargetRect", Vector) = (0, 0, 1, 1)

		_TargetTex ("_TargetTex", any) = "" {}
	}
	SubShader {
		Pass {
			ZTest Always Cull Off ZWrite Off

			CGPROGRAM
			#pragma enable_d3d11_debug_symbols
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex); // Не используется
			uniform float4 _MainTex_ST;

			// В DataChannel написано как используется каждая текстура.
			UNITY_DECLARE_SCREENSPACE_TEXTURE(_TexR);
			UNITY_DECLARE_SCREENSPACE_TEXTURE(_TexG);
			UNITY_DECLARE_SCREENSPACE_TEXTURE(_TexB);
			UNITY_DECLARE_SCREENSPACE_TEXTURE(_TexA);
			uniform float4 _Channels;
			// uniform float4 _ColorSpaces;

			uniform float4 _Color;
			uniform float4 _Scale;

			uniform int _ColorSpace;
			uniform int _BumpMode;
			uniform int _ParallaxMode;
			uniform float _ParallaxRef;
			uniform int _OcclusionMode;

			uniform float4 _SourceRect;
			uniform float4 _TargetRect;

			UNITY_DECLARE_SCREENSPACE_TEXTURE(_TargetTex);

			struct v2f {
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			v2f vert (appdata_base v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord.xy;
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				float2 uv_window = (i.uv - _TargetRect.xy) / (_TargetRect.zw - _TargetRect.xy);
				if (
					// Проверка, находится ли текущая координата внутри окна 
					0 <= uv_window.x && uv_window.x <= 1 && 0 <= uv_window.y && uv_window.y <= 1
				) {
					// Внутри окна копируем данные из SourceRect
					float2 src_uv = uv_window * (_SourceRect.zw - _SourceRect.xy) + _SourceRect.xy;
					float color_src_r = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_TexR, src_uv)[(int)_Channels.r];
					float color_src_g = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_TexG, src_uv)[(int)_Channels.g];
					float color_src_b = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_TexB, src_uv)[(int)_Channels.b];
					float color_src_a = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_TexA, src_uv)[(int)_Channels.a];
					float4 color_src = float4(color_src_r, color_src_g, color_src_b, color_src_a);

					color_src *= _Color;

					if (_ColorSpace < 0) {
						color_src.rgb = GammaToLinearSpace(color_src.rgb);
					} else if (_ColorSpace > 0) {
						color_src.rgb = LinearToGammaSpace(color_src.rgb);
					}

					if (_BumpMode > 0) {
						float scale = _Scale.r;
						float3 normal = UnpackNormalWithScale(color_src, scale);
						color_src.rgb = (normal + 1.0) / 2.0;
						color_src.a = 1;

					} else if (_ParallaxMode > 0) {
						float scale = _Scale.g; // _Parallax
						float h = scale * (color_src.g - 0.5);
						h = h / _ParallaxRef + 0.5;
						color_src.rgb = h;
						color_src.a = 1;

					} else if (_OcclusionMode > 0) {
						color_src.rgb = 1 - (1 - color_src.rgb) * _Scale;
						color_src.a = 1;
						
					} else {
						color_src.rgba *= _Scale;
					}

					return color_src;
				} else {
					// Вне окна оставляем данные целевой текстуры нетронутыми 
					float4 color_dst = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_TargetTex, i.uv);
					return color_dst;
				}
			}
			ENDCG

		}
	}
	Fallback Off
}
