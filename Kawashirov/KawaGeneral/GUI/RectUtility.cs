using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Kawashirov {
	public static class RectUtility {

		public static IEnumerable<Rect> RectSplitVerticalUniform(this Rect rect, int lines) {
			var weights = new float[lines];
			for (var i = 0; i < weights.Length; ++i)
				weights[i] = 1;
			return RectSplitVertical(rect, weights);
		}

		public static IEnumerable<Rect> RectSplitVertical(this Rect rect, params float[] weights) {
			var spacing = 2f;
			var weights_sum = weights.Sum();
			var cell = new Rect(rect);
			var reducedHeight = rect.height - spacing * (weights.Length - 1);
			for (var i = 0; i < weights.Length; ++i) {
				cell.height = reducedHeight * weights[i] / weights_sum;
				yield return cell;
				cell.y += cell.height + spacing;
			}
			yield break;
		}

		public static IEnumerable<Rect> RectSplitHorisontal(this Rect rect, params float[] weights) {
			var spacing = 2f;
			var weights_sum = weights.Sum();
			var cell = new Rect(rect);
			var reducedWidth = rect.width - spacing * (weights.Length - 1);
			for (var i = 0; i < weights.Length; ++i) {
				cell.width = reducedWidth * weights[i] / weights_sum;
				yield return cell;
				cell.x += cell.width + spacing;
			}
			yield break;
		}

#if UNITY_EDITOR

		public static Rect IndentedRect(this Rect rect, int indentLevel) {
			var oldIndentLevel = UnityEditor.EditorGUI.indentLevel;
			try {
				UnityEditor.EditorGUI.indentLevel = indentLevel;
				rect = UnityEditor.EditorGUI.IndentedRect(rect);
			} finally {
				UnityEditor.EditorGUI.indentLevel = oldIndentLevel;
			}
			return rect;
		}

#endif //UNITY_EDITOR

	}
}
