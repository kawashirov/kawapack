using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Kawashirov.KawaShade {
	public struct ShaderInclude : IComparable<ShaderInclude>, IEquatable<ShaderInclude> {
		public enum IncludeType { SYSTEM, DIRECT, CODE }

		public static ShaderInclude System(int order, string module) {
			return new ShaderInclude() { order = order, type = IncludeType.SYSTEM, content = module };
		}

		public static ShaderInclude Direct(int order, string file_path) {
			return new ShaderInclude() { order = order, type = IncludeType.DIRECT, content = file_path };
		}

		public static ShaderInclude Code(int order, string code) {
			return new ShaderInclude() { order = order, type = IncludeType.CODE, content = code };
		}

		public int order;
		public IncludeType type;
		public string content;

		public int CompareTo(ShaderInclude other) {
			var cmp = order.CompareTo(other.order);
			if (cmp == 0)
				cmp = content.CompareTo(other.content);
			return cmp;
		}

		public bool Equals(ShaderInclude other) {
			return order.Equals(other.order) && type.Equals(other.type) && content.Equals(other.content);
		}

		public void Bake(StringBuilder sb) {
			var ic = CultureInfo.InvariantCulture;
			if (type == IncludeType.SYSTEM) {
				// content is something like "UnityCG.cginc"
				sb.AppendFormat(ic, "#include \"{0}\" // Order {1}\n", content, order);
			} else if (type == IncludeType.DIRECT) {
				// content is something like "Assets/BlahBlah/MyCG.cginc"
				sb.AppendFormat(ic, "// Begin of direct include of file {0}, Order {1}\n", content, order);
				using (var reader = new StreamReader(content, Encoding.UTF8)) {
					string line = null;
					while ((line = reader.ReadLine()) != null) {
						sb.Append(line).Append("\n");
					}
				}
				sb.AppendFormat(ic, "// End of direct include of file {0}, Order {1}\n", content, order);
			} else if (type == IncludeType.CODE) {
				// content is HLSL code
				var hash = content.GetHashCode();
				sb.AppendFormat(ic, "// Begin of direct include of text {0}, Order {1}\n", hash, order);
				sb.Append(content).Append("\n");
				sb.AppendFormat(ic, "// End of direct include of text {0}, Order {1}\n", hash, order);
			}
		}
	}
}
