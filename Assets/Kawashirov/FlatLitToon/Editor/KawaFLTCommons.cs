using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;

namespace Kawashirov.FLT {

	public enum TessDomain { Triangles, Quads }
	public enum TessPartioning { integer, fractional_even, fractional_odd, pow2 }

	public enum ShaderComplexity { VF, VGF, VHDGF }
	public enum BlendTemplate { Opaque, Cutout, Fade, Transparent, Custom }
	// public enum BlendKeywords { None, AlphaTest, AlphaBlend, AlphaPreMultiply }
	public enum MainTexKeywords { Off, NoMask, ColorMask }
	public enum EmissionMode { Off, MainTexNoMask, MainTexMask, Custom }
	public enum CutoutMode { Classic, RangeRandom, RangePattern }

	public enum ShadingMode { CubedParadoxFLT, KawashirovFLTLog, KawashirovFLTRamp, KawashirovFLTSingle }

	public enum DistanceFadeMode { None, Range, Infinity }
	public enum DistanceFadeRandom { PerPixel, PerVertex, ScreenPattern }

	public enum FPSMode { None, Color, Texture, Mesh }

	public enum OutlineMode { None, Tinted, Colored }

	public enum DisintegrationMode { None, Pixel, Face, PixelAndFace }

	public enum PolyColorWaveMode { None, Enabled }

	public class CGProgram {
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

		public void Clear()
		{
			this.defines.Clear();
			this.target = "5.0";
			this.only_renderers.Clear();
			this.enable_d3d11_debug_symbols = false;
			this.multi_compile_fwdbase = false;
			this.multi_compile_fwdadd_fullshadows = false;
			this.multi_compile_shadowcaster = false;
			this.multi_compile_fog = true;
			this.multi_compile_instancing = true;
			this.skip_variants.Clear();
			this.includes.Clear();
			this.vertex = null;
			this.hull = null;
			this.domain = null;
			this.geometry = null;
			this.fragment = null;
		}

		public void SkipCommonStaticVariants()
		{
			this.skip_variants.Add("LIGHTMAP_ON");
			this.skip_variants.Add("DIRLIGHTMAP_COMBINED");
			this.skip_variants.Add("DYNAMICLIGHTMAP_ON");
			this.skip_variants.Add("LIGHTMAP_SHADOW_MIXING");
		}

		public string Bake() {
			// "\nCGPROGRAM\n"
			var cgp = new StringBuilder();
			foreach (var define in this.defines)
				cgp.Append("#define ").Append(define).Append('\n');
			if (!string.IsNullOrEmpty(this.target))
				cgp.Append("#pragma target ").Append(this.target).Append('\n');

			if (this.only_renderers.Count > 0) {
				cgp.Append("#pragma only_renderers ");
				foreach (var renderer in this.only_renderers)
					cgp.Append(renderer).Append(' ');
				cgp.Append('\n');
			}
			if (this.multi_compile_fwdbase)
				cgp.Append("#pragma multi_compile_fwdbase\n");
			if (this.multi_compile_fwdadd_fullshadows)
				cgp.Append("#pragma multi_compile_fwdadd_fullshadows\n");
			if (this.multi_compile_shadowcaster)
				cgp.Append("#pragma multi_compile_shadowcaster\n");
			if (this.multi_compile_fog)
				cgp.Append("#pragma multi_compile_fog\n");
			if (this.multi_compile_instancing)
				cgp.Append("#pragma multi_compile_instancing\n");
			if (!this.multi_compile_instancing)
				this.skip_variants.Add("INSTANCING_ON");
			if (this.skip_variants.Count > 0){
				cgp.Append("#pragma skip_variants ");
				foreach (var skip_variant in this.skip_variants)
					cgp.Append(skip_variant).Append(' ');
				cgp.Append('\n');
			}

			foreach (var include in this.includes)
				cgp.Append("#include \"").Append(include).Append("\"\n");

			if (!string.IsNullOrEmpty(this.vertex))
				cgp.Append("#pragma vertex ").Append(this.vertex).Append('\n');
			if (!string.IsNullOrEmpty(this.hull))
				cgp.Append("#pragma hull ").Append(this.hull).Append('\n');
			if (!string.IsNullOrEmpty(this.domain))
				cgp.Append("#pragma domain ").Append(this.domain).Append('\n');
			if (!string.IsNullOrEmpty(this.geometry))
				cgp.Append("#pragma geometry ").Append(this.geometry).Append('\n');
			if (!string.IsNullOrEmpty(this.fragment))
				cgp.Append("#pragma fragment ").Append(this.fragment).Append('\n');
			//cgp.Append("\nENDCG\n");
			return cgp.ToString();
		}

	}

	public class PassSetup {
		public CullMode? cullMode;
		public BlendMode? srcBlend;
		public BlendMode? dstBlend;
		public bool? zWrite;

		public byte? stencilRef;
		public CompareFunction? stencilComp;
		public StencilOp? stencilPass;
		public StencilOp? stencilZFail;

		public void Clear() {
			this.cullMode = null;
			this.srcBlend = null;
			this.dstBlend = null;
			this.zWrite = null;

			this.stencilRef = null;
			this.stencilComp = null;
			this.stencilPass = null;
			this.stencilZFail = null;
		}

		public string Bake()
		{
			var sb = new StringBuilder("\n");
			if (this.cullMode.HasValue)
				sb.AppendFormat("Cull {0} \n", Enum.GetName(typeof(CullMode), this.cullMode.Value));
			if (this.srcBlend.HasValue && this.dstBlend.HasValue) {
				sb.AppendFormat(
					"Blend {0} {1} \n",
					Enum.GetName(typeof(BlendMode), this.srcBlend.Value),
					Enum.GetName(typeof(BlendMode), this.dstBlend.Value)
				);
			}
			if (this.zWrite.HasValue)
				sb.AppendFormat("ZWrite {0} \n", this.zWrite.Value ? "On" : "Off");

			if (this.stencilRef.HasValue || this.stencilComp.HasValue || this.stencilPass.HasValue || this.stencilZFail.HasValue) {
				sb.Append("Stencil { \n");

				if (this.stencilRef.HasValue)
					sb.AppendFormat("ZWrite {0} \n", this.stencilRef.Value);
				if (this.stencilComp.HasValue)
					sb.AppendFormat("Comp {0} \n", Enum.GetName(typeof(CompareFunction), this.stencilComp.Value));
				if (this.stencilPass.HasValue)
					sb.AppendFormat("Pass {0} \n", Enum.GetName(typeof(StencilOp), this.stencilPass.Value));
				if (this.stencilZFail.HasValue)
					sb.AppendFormat("ZFail {0} \n", Enum.GetName(typeof(StencilOp), this.stencilZFail.Value));

				sb.Append(" }\n");
			}

			return sb.ToString();
		}
	}

	static class Commons {
		public static string[] blendModeNames = Enum.GetNames(typeof(BlendMode));
		public static string[] cullModeNames = Enum.GetNames(typeof(CullMode));

		public static string[] TessDomainNames = Enum.GetNames(typeof(TessDomain));
		public static string[] TessPartioningNames = Enum.GetNames(typeof(TessPartioning));

		public static string[] blendTemplateNames = Enum.GetNames(typeof(BlendTemplate));
		public static string[] shaderComplexityNames = new string[] {
			"VF: Lightweight (Vertex/Fragment)",
			"VGF: Geometry (Vertex/Geometry/Fragment)",
			"VHDGF: Tessellation (Vertex/Hull/Domain/Geometry/Fragment)",
		};
		// public static string[] blendKeywordsNames = Enum.GetNames(typeof(BlendKeywords));
		public static string[] mainTexKeywordsNames = new string[] {
			"No Main Texture (Albedo Color Only)",
			"Main Texture without Color Mask",
			"Main Texture with Color Mask",
		};
		
		public static string[] emissionMode = new string[] {
			"No Emission (at all)",
			"Emission from Main Texture without Mask",
			"Emission from Main Texture with Mask",
			"Custom Emission Texture",
		};

		public static string[] cutoutModeNames = Enum.GetNames(typeof(CutoutMode));

		public static string[] shadingModeNames = new string[] {
			"CubedParadox FLT",
			"Kawashirov FLT Logarithmic Diffuse-based (Пластилиновый™)",
			"Kawashirov FLT Ramp-based (В разработке)",
			"Kawashirov FLT Single Diffuse-based (Похож на CubedParadox, но лучше)"
		};

		public static string[] distanceFadeModeNames = Enum.GetNames(typeof(DistanceFadeMode));
		public static string[] distanceFadeRandomNames = Enum.GetNames(typeof(DistanceFadeRandom));

		public static string[] FPSModeNames = Enum.GetNames(typeof(FPSMode));

		public static string[] outlineModeNames = Enum.GetNames(typeof(OutlineMode));

		public static string[] disintegrationModeNames = Enum.GetNames(typeof(DisintegrationMode));

		public static string[] polyColorWaveModeNames = Enum.GetNames(typeof(PolyColorWaveMode));

		static Commons()
		{
			var rem = "[ Feature removed, do not use it ]";
			distanceFadeRandomNames[1] = rem;
			disintegrationModeNames[1] = rem;
			disintegrationModeNames[3] = rem;
		}

		public static bool MaterialCheckTag(object material, string tag, string value)
		{
			Material m = material as Material;
			string tag_v = m ? m.GetTag(tag, false, "") : null;
			// Debug.Log(String.Format("{0}: {1}={2}", material, tag, tag_v));
			return m && string.Equals(value, tag_v, StringComparison.InvariantCultureIgnoreCase);
		}

		public static bool CheckMaterialTagContains(object material, string tag, string value)
		{
			Material m = material as Material;
			if (!m)
				return false;
			string tag_v = m.GetTag(tag, false, "");
			return tag_v.Split(',').ToList<string>().Any(v => string.Equals(value, v, StringComparison.InvariantCultureIgnoreCase));
		}

		public static bool CheckAllMaterialsTagContains(object[] materials, string tag, string value)
		{
			foreach (var material in materials)
				if (!CheckMaterialTagContains(material, tag, value))
					return false;
			return true;
		}

		public static string BakeTags(this IDictionary<string, string> tags)
		{
			return tags
				.Select(x => string.Format("\"{0}\" = \"{1}\"", x.Key, x.Value))
				.Aggregate((a, b) => a + " " + b);
		}
	}

}