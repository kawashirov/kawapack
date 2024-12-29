#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class DataChannel {
		public readonly Material parent;
		public readonly DataChannelDescriptor descriptor;

		/*
			в финальную текстуру атласа будет записан:
			- в канал R (№0): (канал № dstCh[0] из dstTex[0]) * scaleColor.r * scale
			- в канал G (№1): (канал № dstCh[1] из dstTex[1]) * scaleColor.g * scale
			- в канал B (№2): (канал № dstCh[2] из dstTex[2]) * scaleColor.b * scale
			- в канал A (№3): (канал № dstCh[3] из dstTex[3]) * scaleColor.a * scale
			Такой сложный подход позволяет легко мультиплексить каналы на уровне адаптеров.
			Для простого RGBA <-> RGBA есть SetTextureRGBA
			Для простого RGB1 <-> RGB1 есть SetTextureRGBWhiteAlpha
		*/
		public readonly Texture2D[] dstTex = new Texture2D[4] {
			Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture
		};
		public readonly int[] dstCh = new int[4] { 0, 1, 2, 3 };

		public Color color = Color.white;

		public Vector2Int size = Vector2Int.zero;

		public DataChannel(Material parent, DataChannelDescriptor descriptor) {
			this.parent = parent;
			this.descriptor = descriptor;
		}

		public Vector4 ChannelsAsVector4() => new Vector4(dstCh[0], dstCh[1], dstCh[2], dstCh[3]);

		public DataChannel SetTextureRGBA(Texture2D tex) {
			dstTex[0] = dstTex[1] = dstTex[2] = dstTex[3] = tex;
			(dstCh[0], dstCh[1], dstCh[2], dstCh[3]) = (0, 1, 2, 3);
			return this;
		}

		public DataChannel SetTextureRGB(Texture2D tex) {
			dstTex[0] = dstTex[1] = dstTex[2] = tex;
			(dstCh[0], dstCh[1], dstCh[2]) = (0, 1, 2);
			return this;
		}

		public DataChannel SetTextureSingleToRGB(Texture2D tex, int single_ch) {
			dstTex[0] = dstTex[1] = dstTex[2] = tex;
			(dstCh[0], dstCh[1], dstCh[2]) = (single_ch, single_ch, single_ch);
			return this;
		}

		public DataChannel SetAlpha(Texture2D tex, int single_ch) {
			dstTex[3] = tex;
			dstCh[3] = single_ch;
			return this;
		}

		public DataChannel SetWhiteAlpha() {
			dstTex[3] = Texture2D.whiteTexture;
			dstCh[3] = 0;
			return this;
		}

		public DataChannel SetColor(Color color) {
			this.color = color;
			return this;
		}

		public Vector2Int LargestTexSize() {
			var tex = dstTex.OrderByDescending(t => t.height * t.width).First();
			return new Vector2Int(tex.width, tex.height);
		}

		private static float WeightSqr(Texture2D tex) => 1f * tex.height * tex.width;
		public float WeightSqr() => dstTex.Select(WeightSqr).Max();

	}
}
#endif