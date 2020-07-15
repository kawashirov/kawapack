using System.Globalization;
using System.Text;
using UnityEngine;

namespace Kawashirov.ShaderBaking {
	public class PropertyColor : Property<Color> {
		public bool isHDR = true;

		public PropertyColor() => defualt = Color.white;

		public override void Bake(StringBuilder sb) {
			if (isHDR)
				sb.Append("[HDR] ");
			sb.AppendFormat(
				CultureInfo.InvariantCulture, "{0} (\"{0}\", Color) = ({1}, {2}, {3}, {4})\n",
				name, defualt.r, defualt.g, defualt.b, defualt.a
			);
		}
	}
}
