using Kawashirov.KawaShade;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

		public SortedSet<ShaderInclude> includes = new SortedSet<ShaderInclude>();

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

			sb.AppendFormat(ic, "Pass {{ // {0}\n", name);
			sb.AppendFormat(ic, "Name \"{0}\"\n", name);

			sb.BakeTags(tags);
			sb.Append("\n");

			if (cullMode.HasValue)
				sb.AppendFormat(ic, "Cull {0}\n", Enum.GetName(typeof(CullMode), cullMode.Value));
			if (srcBlend.HasValue && dstBlend.HasValue) {
				var srcName = Enum.GetName(typeof(BlendMode), srcBlend.Value);
				var dstName = Enum.GetName(typeof(BlendMode), dstBlend.Value);
				sb.AppendFormat("Blend {0} {1}\n", srcName, dstName);
			}
			if (zWrite.HasValue)
				sb.AppendFormat(ic, "ZWrite {0}\n", zWrite.Value ? "On" : "Off");

			if (stencilRef.HasValue || stencilComp.HasValue || stencilPass.HasValue || stencilZFail.HasValue) {
				sb.Append("Stencil {\n");

				if (stencilRef.HasValue)
					sb.AppendFormat(ic, "ZWrite {0}\n", stencilRef.Value);
				if (stencilComp.HasValue)
					sb.AppendFormat(ic, "Comp {0}\n", Enum.GetName(typeof(CompareFunction), stencilComp.Value));
				if (stencilPass.HasValue)
					sb.AppendFormat(ic, "Pass {0}\n", Enum.GetName(typeof(StencilOp), stencilPass.Value));
				if (stencilZFail.HasValue)
					sb.AppendFormat(ic, "ZFail {0}\n", Enum.GetName(typeof(StencilOp), stencilZFail.Value));

				sb.Append("} // End of Stencil\n");
			}

			sb.Append("\nHLSLPROGRAM\n");

			foreach (var define in defines)
				sb.AppendFormat(ic, "#define {0}\n", define);
			if (!string.IsNullOrEmpty(target))
				sb.AppendFormat(ic, "#pragma target {0}\n", target);

			if (only_renderers.Count > 0) {
				sb.Append("#pragma only_renderers ");
				foreach (var renderer in only_renderers)
					sb.Append(renderer).Append(' ');
				sb.Append("\n");
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
				sb.Append("\n");
			}

			foreach (var include in includes)
				include.Bake(sb);

			if (!string.IsNullOrEmpty(vertex))
				sb.AppendFormat(ic, "#pragma vertex {0}\n", vertex);
			if (!string.IsNullOrEmpty(hull))
				sb.AppendFormat(ic, "#pragma hull {0}\n", hull);
			if (!string.IsNullOrEmpty(domain))
				sb.AppendFormat(ic, "#pragma domain {0}\n", domain);
			if (!string.IsNullOrEmpty(geometry))
				sb.AppendFormat(ic, "#pragma geometry {0}\n", geometry);
			if (!string.IsNullOrEmpty(fragment))
				sb.AppendFormat(ic, "#pragma fragment {0}\n", fragment);

			sb.Append("\nENDHLSL\n");
			sb.Append("} // End of Pass \n");
			return;
		}
	}

}
