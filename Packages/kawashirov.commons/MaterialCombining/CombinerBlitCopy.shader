Shader "Kawashirov/MaterialCombiner/BlitCopy" {
	Properties
	{
		_MainTex ("_MainTex", any) = "" {}
		_Color("_Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_Scale("_Scale", Float) = 1.0

		_Override ("_Override", Vector) = (0, 0, 0, 0)
		_OverrideColor("_OverrideColor", Color) = (1.0, 1.0, 1.0, 1.0)

		_ChannelMap("_ChannelMap", Vector) = (0, 1, 2, 3)

		_SourceRect ("_SourceRect", Vector) = (0, 0, 0, 0)

		_TargetTex ("_TargetTex", any) = "" {}
	}
	SubShader {
		Pass {
			ZTest Always Cull Off ZWrite Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
			uniform float4 _MainTex_ST;

			uniform float4 _Override;
			uniform float4 _OverrideColor;

			uniform float4 _Color;
			uniform float _Scale;

			UNITY_DECLARE_SCREENSPACE_TEXTURE(_TargetTex);

			// Какой канал _MainTex записывать в каждый из этих каналов целевой текстуры?
			uniform float4 _ChannelMap;

			uniform float4 _SourceRect;
			uniform float4 _TargetRect;

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

			void map_channel(inout float4 color_ret, float idx, float value) {
				if (idx == 0)
					color_ret.r = value;
				else if (idx == 1)
					color_ret.g = value;
				else if (idx == 2)
					color_ret.b = value;
				else if (idx == 3)
					color_ret.a = value;
				// else -> don't apply anything
			}

			float4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				float4 color_dst = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_TargetTex, i.uv);

				float2 uv_window = (i.uv - _TargetRect.xy) / (_TargetRect.zw - _TargetRect.xy);
				if (
					// Проверка, находится ли текущая координата внутри окна 
					0 <= uv_window.x && uv_window.x <= 1 && 0 <= uv_window.y && uv_window.y <= 1
				) {
					// Внутри окна копируем данные из SourceRect
					float2 src_uv = uv_window * (_SourceRect.zw - _SourceRect.xy) + _SourceRect.xy;
					float4 color_src = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, src_uv);
					color_src *= _Color * _Scale;

					float4 color_ret = color_dst;
					map_channel(color_ret, _ChannelMap.r, color_src.r);
					map_channel(color_ret, _ChannelMap.g, color_src.g);
					map_channel(color_ret, _ChannelMap.b, color_src.b);
					map_channel(color_ret, _ChannelMap.a, color_src.a);

					return color_ret;
				} else {
					// Вне окна оставляем данные целевой текстуры нетронутыми 
					return color_dst;
				}
			}
			ENDCG

		}
	}
	Fallback Off
}
