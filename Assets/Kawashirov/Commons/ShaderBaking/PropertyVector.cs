using System.Globalization;
using System.Text;
using UnityEngine;

namespace Kawashirov.ShaderBaking {
	public class PropertyVector : Property<Vector4> {

		public PropertyVector() => defualt = Vector4.zero;

		public override void Bake(StringBuilder sb) {
			sb.AppendFormat(
				CultureInfo.InvariantCulture, "{0} (\"{0}\", Vector) = ({1}, {2}, {3}, {4})\n",
				name, defualt.x, defualt.y, defualt.z, defualt.w
			);
		}
	}
}
