using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kawashirov.ShaderBaking {

	public class PassSetup {
		public string name;
		public bool active = true;

		public Dictionary<string, string> tags = new Dictionary<string, string>();

		public CullMode? cullMode;
		public BlendMode? srcBlend;
		public BlendMode? dstBlend;
		public bool? zWrite;

		public byte? stencilRef;
		public CompareFunction? stencilComp;
		public StencilOp? stencilPass;
		public StencilOp? stencilZFail;

		public List<string> defines = new List<string>();
		public string target = "5.0";
		public HashSet<string> only_renderers = new HashSet<string>();
		public bool enable_d3d11_debug_symbols = false;
		public bool multi_compile_fwdbase = false;
		public bool multi_compile_fwdadd_fullshadows = false;
		public bool multi_compile_shadowcaster = false;
		public bool multi_compile_fog = true;
		public bool multi_compile_instancing = true;
		public HashSet<string> skip_variants = new HashSet<string>();

		public List<string> includes = new List<string>();

		public string vertex = null;
		public string hull = null;
		public string domain = null;
		public string geometry = null;
		public string fragment = null;

		public void Clear() {
			cullMode = null;
			srcBlend = null;
			dstBlend = null;
			zWrite = null;

			stencilRef = null;
			stencilComp = null;
			stencilPass = null;
			stencilZFail = null;

			defines.Clear();
			target = "5.0";
			only_renderers.Clear();
			enable_d3d11_debug_symbols = false;
			multi_compile_fwdbase = false;
			multi_compile_fwdadd_fullshadows = false;
			multi_compile_shadowcaster = false;
			multi_compile_fog = true;
			multi_compile_instancing = true;
			skip_variants.Clear();

			includes.Clear();

			vertex = null;
			hull = null;
			domain = null;
			geometry = null;
			fragment = null;
		}

		public void SkipCommonStaticVariants() {
			skip_variants.Add("LIGHTMAP_ON");
			skip_variants.Add("DIRLIGHTMAP_COMBINED");
			skip_variants.Add("DYNAMICLIGHTMAP_ON");
			skip_variants.Add("LIGHTMAP_SHADOW_MIXING");
		}

		public void Bake(StringBuilder sb) {
			if (!active)
				return;

			var ic = CultureInfo.InvariantCulture;

			sb.Append("Pass {\n");
			sb.AppendFormat(ic, "Name \"{0}\"\n", name);

			sb.BakeTags(tags);
			sb.Append("\n");

			if (cullMode.HasValue)
				sb.AppendFormat("Cull {0} \n", Enum.GetName(typeof(CullMode), cullMode.Value));
			if (srcBlend.HasValue && dstBlend.HasValue) {
				sb.AppendFormat(
						"Blend {0} {1} \n",
						Enum.GetName(typeof(BlendMode), srcBlend.Value),
						Enum.GetName(typeof(BlendMode), dstBlend.Value)
				);
			}
			if (zWrite.HasValue)
				sb.AppendFormat("ZWrite {0} \n", zWrite.Value ? "On" : "Off");

			if (stencilRef.HasValue || stencilComp.HasValue || stencilPass.HasValue || stencilZFail.HasValue) {
				sb.Append("Stencil { \n");

				if (stencilRef.HasValue)
					sb.AppendFormat("ZWrite {0} \n", stencilRef.Value);
				if (stencilComp.HasValue)
					sb.AppendFormat("Comp {0} \n", Enum.GetName(typeof(CompareFunction), stencilComp.Value));
				if (stencilPass.HasValue)
					sb.AppendFormat("Pass {0} \n", Enum.GetName(typeof(StencilOp), stencilPass.Value));
				if (stencilZFail.HasValue)
					sb.AppendFormat("ZFail {0} \n", Enum.GetName(typeof(StencilOp), stencilZFail.Value));

				sb.Append("}\n");
			}

			sb.Append("\nCGPROGRAM\n");

			foreach (var define in defines)
				sb.Append("#define ").Append(define).Append('\n');
			if (!string.IsNullOrEmpty(target))
				sb.Append("#pragma target ").Append(target).Append('\n');

			if (only_renderers.Count > 0) {
				sb.Append("#pragma only_renderers ");
				foreach (var renderer in only_renderers)
					sb.Append(renderer).Append(' ');
				sb.Append('\n');
			}
			if (enable_d3d11_debug_symbols)
				sb.Append("#pragma enable_d3d11_debug_symbols\n");
			if (multi_compile_fwdbase)
				sb.Append("#pragma multi_compile_fwdbase\n");
			if (multi_compile_fwdadd_fullshadows)
				sb.Append("#pragma multi_compile_fwdadd_fullshadows\n");
			if (multi_compile_shadowcaster)
				sb.Append("#pragma multi_compile_shadowcaster\n");
			if (multi_compile_fog)
				sb.Append("#pragma multi_compile_fog\n");
			if (multi_compile_instancing)
				sb.Append("#pragma multi_compile_instancing\n");
			if (!multi_compile_instancing)
				skip_variants.Add("INSTANCING_ON");
			if (skip_variants.Count > 0) {
				sb.Append("#pragma skip_variants ");
				foreach (var skip_variant in skip_variants)
					sb.Append(skip_variant).Append(' ');
				sb.Append('\n');
			}

			foreach (var include in includes)
				sb.Append("#include \"").Append(include).Append("\"\n");

			if (!string.IsNullOrEmpty(vertex))
				sb.Append("#pragma vertex ").Append(vertex).Append('\n');
			if (!string.IsNullOrEmpty(hull))
				sb.Append("#pragma hull ").Append(hull).Append('\n');
			if (!string.IsNullOrEmpty(domain))
				sb.Append("#pragma domain ").Append(domain).Append('\n');
			if (!string.IsNullOrEmpty(geometry))
				sb.Append("#pragma geometry ").Append(geometry).Append('\n');
			if (!string.IsNullOrEmpty(fragment))
				sb.Append("#pragma fragment ").Append(fragment).Append('\n');

			sb.Append("\nENDCG\n");
			sb.Append("}\n");
			return;
		}
	}

}
