using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Kawashirov.ShaderBaking {
	public static class Commons {
		public static readonly string GenaratorGUID = "Kawa_GenaratorGUID";
		public static readonly string Feature_Debug = "Kawa_Feature_Debug";

		public static void BakeTags(this StringBuilder sb, IDictionary<string, string> tags) {
			sb.Append("Tags {\n");
			var ic = CultureInfo.InvariantCulture;
			foreach (var tag in tags)
				sb.AppendFormat(ic, "\"{0}\" = \"{1}\"\n", tag.Key, tag.Value);
			sb.Append("}\n");
		}

		public static void BakeProperties(this StringBuilder sb, List<Property> properties) {
			sb.Append("Properties { ");
			foreach (var property in properties) {
				property.Bake(sb);
			}
			sb.Append("} ");
		}
	}
}
