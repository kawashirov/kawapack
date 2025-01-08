#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class DataTex {
		// Data Tex (instance)
		public readonly Material parent;
		public readonly DataTexDesc desc;

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

		// Color is affected by Unity intrnal gamma corections, 
		// but scale is not and passed as is.
		public Color color = Color.white;
		public Vector4 scale = Vector4.one;

		public DataTex(Material parent, DataTexDesc desc) {
			this.parent = parent;
			this.desc = desc;
		}

		public Vector4 ChannelsAsVector4() => new Vector4(dstCh[0], dstCh[1], dstCh[2], dstCh[3]);

		public DataTex SetTexRGBA(Texture2D tex) {
			dstTex[0] = dstTex[1] = dstTex[2] = dstTex[3] = tex;
			(dstCh[0], dstCh[1], dstCh[2], dstCh[3]) = (0, 1, 2, 3);
			return this;
		}

		public DataTex SetTexRGB(Texture2D tex) {
			dstTex[0] = dstTex[1] = dstTex[2] = tex;
			(dstCh[0], dstCh[1], dstCh[2]) = (0, 1, 2);
			return this;
		}

		public DataTex SetTexSingleToRGB(Texture2D tex, int single_ch) {
			dstTex[0] = dstTex[1] = dstTex[2] = tex;
			(dstCh[0], dstCh[1], dstCh[2]) = (single_ch, single_ch, single_ch);
			return this;
		}

		public DataTex SetTexAlpha(Texture2D tex, int single_ch) {
			dstTex[3] = tex;
			dstCh[3] = single_ch;
			return this;
		}

		public DataTex SetTexAlphaWhite() {
			dstTex[3] = Texture2D.whiteTexture;
			dstCh[3] = 3;
			return this;
		}

		//

		public DataTex SetColor(Color color) {
			this.color = color;
			return this;
		}

		public DataTex SetColorRGB(Color color) {
			this.color.r = color.r;
			this.color.g = color.g;
			this.color.b = color.b;
			return this;
		}

		public DataTex SetColorRGB(float single) {
			color.r = single;
			color.g = single;
			color.b = single;
			return this;
		}

		public DataTex SetColorAlpha(float alpha) {
			color.a = alpha;
			return this;
		}

		public DataTex SetColorAlphaWhite() {
			color.a = 1;
			return this;
		}

		//

		public DataTex SetScaleRGB(Vector4 scale) {
			this.scale.x = scale.x;
			this.scale.y = scale.y;
			this.scale.z = scale.z;
			return this;
		}

		public DataTex SetScaleAlpha(float alpha) {
			scale.w = alpha;
			return this;
		}

		public DataTex SetScaleAlphaWhite() {
			scale.w = 1;
			return this;
		}

		//

		public DataTex SetAlphaWhite() {
			SetTexAlphaWhite();
			SetColorAlphaWhite();
			SetScaleAlphaWhite();
			return this;
		}


		public Vector2Int LargestTexSize() {
			var tex = dstTex.OrderByDescending(WeightSqr).First();
			return new Vector2Int(tex.width, tex.height);
		}

		private static float WeightSqr(Texture2D tex) => 1f * tex.width * tex.height;
		public float WeightSqr() => dstTex.Select(WeightSqr).Max();

		public override string ToString() =>
			$"{nameof(DataTex)}(desc={desc?.name}, dstTex=({dstTex[0]}, {dstTex[1]}, {dstTex[2]}, {dstTex[3]}), " +
			$"dstCh=({dstCh[0]}, {dstCh[1]}, {dstCh[2]}, {dstCh[3]}), color={color})";

	}
}
#endif