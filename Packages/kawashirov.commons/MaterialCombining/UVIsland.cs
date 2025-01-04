#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public readonly struct UVIsland {
		public static readonly UVIsland singual = new UVIsland(
			float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity
		);

		// preffered coord system is pixels
		public readonly float umin, vmin, umax, vmax;

		public UVIsland(Vector2 uv) {
			umin = umax = uv.x;
			vmin = vmax = uv.y;
		}

		public UVIsland(float umin, float vmin, float umax, float vmax) {
			this.umin = umin;
			this.vmin = vmin;
			this.umax = umax;
			this.vmax = vmax;
		}

		public UVIsland(Rect rect) {
			umin = rect.xMin;
			vmin = rect.yMin;
			umax = rect.xMax;
			vmax = rect.yMax;
		}

		public UVIsland(Rect rect, float normalize) {
			umin = rect.xMin / normalize;
			vmin = rect.yMin / normalize;
			umax = rect.xMax / normalize;
			vmax = rect.yMax / normalize;
		}

		public static UVIsland FromPoints(ICollection<Vector2> points) {
			return new UVIsland(
				points.Select(v => v.x).Min(),
				points.Select(v => v.y).Min(),
				points.Select(v => v.x).Max(),
				points.Select(v => v.y).Max()
			);
		}

		public static UVIsland FromPointsInt(Vector2 size, ICollection<Vector2> points) {
			return new UVIsland(
				Mathf.Floor(points.Select(v => v.x).Min() * size.x),
				Mathf.Floor(points.Select(v => v.y).Min() * size.y),
				Mathf.Ceil(points.Select(v => v.x).Max() * size.x),
				Mathf.Ceil(points.Select(v => v.y).Max() * size.y)
			);
		}

		public static bool Intersects(UVIsland left, UVIsland right, float epsilon) {
			return !(left.umin > right.umax + epsilon ||
				left.umax < right.umin - epsilon ||
				left.vmin > right.vmax + epsilon ||
				left.vmax < right.vmin - epsilon);
		}

		public static bool TryMerge(UVIsland left, UVIsland right, float epsilon, out UVIsland merged) {
			if (Intersects(left, right, epsilon)) {
				merged = new UVIsland(
					Mathf.Min(left.umin, right.umin), Mathf.Min(left.vmin, right.vmin),
					Mathf.Max(left.umax, right.umax), Mathf.Max(left.vmax, right.vmax)
				);
				return true;
			} else {
				merged = new UVIsland(Rect.zero);
				return false;
			}
		}

		// Проверяет, что inner внутри this с допуском epsilon
		public bool Inside(UVIsland inner, float epsilon) {
			return umin <= inner.umin + epsilon &&
				inner.umax - epsilon <= umax &&
				vmin <= inner.vmin + epsilon &&
				inner.vmax - epsilon <= vmax;
		}

		private static float InverseLerpUnclamped(float a, float b, float value) {
			return a != b ? (value - a) / (b - a) : 0f;
		}

		public Vector2 InverseLerp(Vector2 uv) => new Vector2(
			InverseLerpUnclamped(umin, umax, uv.x),
			InverseLerpUnclamped(vmin, vmax, uv.y)
		);

		public Vector2 Lerp(Vector2 uv) => new Vector2(
			Mathf.LerpUnclamped(umin, umax, uv.x),
			Mathf.LerpUnclamped(vmin, vmax, uv.y)
		);

		public UVIsland ExpandByUVPoint(Vector2 uv) => new UVIsland(
			Mathf.Min(umin, uv.x), Mathf.Min(vmin, uv.y),
			Mathf.Max(umax, uv.x), Mathf.Max(vmax, uv.y)
		);

		public UVIsland Expand(float value) => new UVIsland(
			umin - value, vmin - value,
			umax + value, vmax + value
		);

		private static float Align(float value, int align, bool up) {
			var rem = value % align;
			return rem == 0 ? value : up ? value + (align - rem) : value - rem;
		}

		public UVIsland Align(int value) => new UVIsland(
			Align(umin, value, false), Align(vmin, value, false),
			Align(umax, value, true), Align(vmax, value, true)
		);

		public UVIsland TransformST(Vector4 st) => new UVIsland(
			// Применить _ST преобразование к острову, аналог TRANSFORM_TEX в шейдерах
			umin * st.x + st.z, vmin * st.y + st.w,
			umax * st.x + st.z, vmax * st.y + st.w
		);

		public UVIsland TransformSTBack(Vector4 st) => new UVIsland(
			// Обратное к TransformST преобразование
			(umin - st.z) / st.x, (vmin - st.w) / st.y,
			(umax - st.z) / st.x, (vmax - st.w) / st.y
		);

		public UVIsland ToTexCoords(Vector2Int size) => new UVIsland(
			umin * size.x, vmin * size.y,
			umax * size.x, vmax * size.y
		);

		public Vector2 Size() => new Vector2(umax - umin, vmax - vmin);

		private static int RoundToInt(float v) => Mathf.Max(Mathf.RoundToInt(v), 1);

		public Vector2Int SizeInt() => new Vector2Int(RoundToInt(umax - umin), RoundToInt(vmax - vmin));

		public UVIsland RoundToInt() => new UVIsland(
			Mathf.Floor(umin), Mathf.Floor(vmin),
			Mathf.Ceil(umax), Mathf.Ceil(vmax)
		);

		public Texture2D MakeDullTex(TextureFormat format) {
			var width = RoundToInt(umax - umin);
			var height = RoundToInt(vmax - vmin);
			return new Texture2D(width, height, format, false);
		}

		public Vector4 ToVector4() => new Vector4(umin, vmin, umax, vmax);

		public Vector4 ToVector4Norm(float tex_width, float tex_height) => new Vector4(
			umin / tex_width, vmin / tex_height,
			umax / tex_width, vmax / tex_height
		);

		public override string ToString()
			=> $"{nameof(UVIsland)}(min:({umin}, {vmin}), max:({umax}, {vmax}), size:({umax - umin},{vmax - vmin}))";
	}
}
#endif