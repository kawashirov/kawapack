using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kawashirov.ShaderBaking {
	public class ShaderSetup {
		public string name;
		public List<Property> properties = new List<Property>();
		public Dictionary<string, string> tags = new Dictionary<string, string>();
		public PassSetup forward = new PassSetup();
		public PassSetup forward_add = new PassSetup();
		public PassSetup shadowcaster = new PassSetup();
		public string custom_editor = null;

		public ShaderSetup() {
			forward.name = "FORWARD";
			forward.tags["LightMode"] = "ForwardBase";
			forward.multi_compile_fwdbase = true;
			forward.multi_compile_fwdadd_fullshadows = false;
			forward.multi_compile_shadowcaster = false;

			forward_add.name = "FORWARD_DELTA";
			forward_add.tags["LightMode"] = "ForwardAdd";
			forward_add.multi_compile_fwdbase = false;
			forward_add.multi_compile_fwdadd_fullshadows = true;
			forward_add.multi_compile_shadowcaster = false;

			shadowcaster.name = "SHADOW_CASTER";
			shadowcaster.tags["LightMode"] = "ShadowCaster";
			shadowcaster.multi_compile_fwdbase = false;
			shadowcaster.multi_compile_fwdadd_fullshadows = false;
			shadowcaster.multi_compile_shadowcaster = true;
		}

		public void SkipCommonStaticVariants() {
			forward.SkipCommonStaticVariants();
			forward_add.SkipCommonStaticVariants();
			shadowcaster.SkipCommonStaticVariants();
		}

		public void Define(string define) {
			forward.defines.Add(define);
			forward_add.defines.Add(define);
			shadowcaster.defines.Add(define);
		}

		public void Include(string include) {
			forward.includes.Add(include);
			forward_add.includes.Add(include);
			shadowcaster.includes.Add(include);
		}

		public void Debug(bool debug) {
			TagBool(Commons.Feature_Debug, debug);
			forward.enable_d3d11_debug_symbols = debug;
			forward_add.enable_d3d11_debug_symbols = debug;
			shadowcaster.enable_d3d11_debug_symbols = debug;
		}

		public void TagBool(string tag, bool value) {
			tags[tag] = value ? "True" : "False";
		}

		public void TagEnum<E>(string tag, E value) {
			tags[tag] = Enum.GetName(typeof(E), value);
		}

		public void Bake(StringBuilder sb) {
			sb.AppendFormat("Shader \"{0}\" {{\n", name);

			sb.BakeProperties(properties);

			sb.Append("SubShader {\n");
			sb.BakeTags(tags);

			forward.Bake(sb);
			forward_add.Bake(sb);
			shadowcaster.Bake(sb);

			sb.Append("}\n");
			sb.Append("FallBack \"Mobile/Diffuse\"\n");
			if (!string.IsNullOrWhiteSpace(custom_editor))
				sb.AppendFormat("CustomEditor \"{0}\"\n", custom_editor);
			sb.Append("}\n");
		}

	}
}
