#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public readonly struct UVIsland {
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

		public UVIsland ExpandByUVPoint(Vector2 uv) => new UVIsland(
			Mathf.Min(umin, uv.x), Mathf.Min(vmin, uv.y),
			Mathf.Max(umax, uv.x), Mathf.Max(vmax, uv.y)
		);

		public UVIsland Expand(float value) => new UVIsland(
			umin - value, vmin - value,
			umax + value, vmax + value
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

		private static int RoundToInt(float v) => Mathf.Max(Mathf.RoundToInt(v), 1);

		public Vector2 Size() => new Vector2(umax - umin, vmax - vmin);

		public Vector2Int SizeInt() => new Vector2Int(RoundToInt(umax - umin), RoundToInt(vmax - vmin));

		public UVIsland RoundToInt() => new UVIsland(
			Mathf.Floor(umin), Mathf.Floor(vmin),
			Mathf.Ceil(umax), Mathf.Ceil(vmax)
		);

		public Texture2D MakeDullTex() {
			var s = SizeInt();
			return new Texture2D(s.x, s.y, TextureFormat.Alpha8, false);
		}

		public Vector4 ToVector4() => new Vector4(umin, vmin, umax, vmax);

		public Vector4 ToVector4Norm(float tex_width, float tex_height) => new Vector4(
			umin / tex_width, vmin / tex_height,
			umax / tex_width, vmax / tex_height
		);

		public override string ToString() => $"UVIsland(({umin}, {vmin}), ({umax}, {vmax}))";
	}
}
#endif