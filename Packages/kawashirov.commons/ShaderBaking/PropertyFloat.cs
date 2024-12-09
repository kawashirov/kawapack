using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kawashirov.ShaderBaking {
	public class PropertyFloat : Property<float> {
		public Vector2? range = null;
		public float? power = null;

		public override void Bake(StringBuilder sb) {
			var ic = CultureInfo.InvariantCulture;
			if (power.HasValue)
				sb.AppendFormat(ic, "[PowerSlider({0})] ", power.Value);
			var float_or_range = range.HasValue ? string.Format(ic, "Range({0}, {1})", range.Value.x, range.Value.y) : "Float";
			sb.AppendFormat(ic, "{0} (\"{0}\", {1}) = {2}\n", name, float_or_range, defualt);
		}
	}
}
