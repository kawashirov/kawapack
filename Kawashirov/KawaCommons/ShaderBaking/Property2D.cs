using System.Globalization;
using System.Text;

namespace Kawashirov.ShaderBaking {
	public class Property2D : Property<string> {
		public bool isNormal = false;

		public Property2D() => DefaultWhite();

		public void DefaultWhite() {
			defualt = "white";
			isNormal = false;
		}

		public void DefaultBlack() {
			defualt = "white";
			isNormal = false;
		}

		public void DefaultBump() {
			defualt = "bump";
			isNormal = true;
		}

		public override void Bake(StringBuilder sb) {
			if (isNormal)
				sb.Append("[Normal] ");
			sb.AppendFormat(CultureInfo.InvariantCulture, "{0} (\"{0}\", 2D) = \"{1}\" {2}\n", name, defualt, "{}");
		}
	}
}
