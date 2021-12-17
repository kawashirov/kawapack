using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Kawashirov {
	static class KawaGUIUtility {

		public static IEnumerable<Rect> RectSplitHorisontal(this Rect rect, params float[] weights) {
			var weights_sum = weights.Sum();
			var cell = new Rect(rect);
			for (var i = 0; i < weights.Length; ++i) {
				cell.width = rect.width * weights[i] / weights_sum;
				yield return cell;
				cell.x += cell.width;
			}
			yield break;
		}

	}
}
