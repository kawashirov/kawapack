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
	public enum BlendTemplate { Opaque, Cutout, Fade, Custom = 256 }
	// public enum BlendKeywords { None, AlphaTest, AlphaBlend, AlphaPreMultiply }
	public enum MainTexKeywords { Off, NoMask, ColorMask }
	public enum CutoutMode { Classic, RangeRandom, RangePattern }
	public enum EmissionMode { Off, AlbedoNoMask, AlbedoMask, Custom }

	public enum ShadingMode { CubedParadoxFLT, KawashirovFLTSingle, KawashirovFLTRamp }

	public enum DistanceFadeMode { None, Range, Infinity }
	public enum DistanceFadeRandom { PerPixel, ScreenPattern }

	public enum FPSMode { None, Color, Texture, Mesh }

	public enum OutlineMode { None, Tinted, Colored }

	public enum DisintegrationMode { None, Pixel, Face, PixelAndFace }

	public enum PolyColorWaveMode { None, Enabled }


	static class Commons {
		public static string[] blendModeNames = Enum.GetNames(typeof(BlendMode));
		public static string[] cullModeNames = Enum.GetNames(typeof(CullMode));

		public static string[] TessDomainNames = Enum.GetNames(typeof(TessDomain));
		public static string[] TessPartioningNames = Enum.GetNames(typeof(TessPartioning));

		public static string[] blendTemplateNames = Enum.GetNames(typeof(BlendTemplate));

		public static Dictionary<ShaderComplexity, string> shaderComplexityNames = new Dictionary<ShaderComplexity, string> {
			{ ShaderComplexity.VF, "VF: Lightweight (Vertex/Fragment)" },
			{ ShaderComplexity.VGF, "VGF: Geometry (Vertex/Geometry/Fragment)" },
			{ ShaderComplexity.VHDGF, "VHDGF: Tessellation (Vertex/Hull/Domain/Geometry/Fragment)" },
		};

		public static Dictionary<MainTexKeywords, string> mainTexKeywordsNames = new Dictionary<MainTexKeywords, string> {
			{ MainTexKeywords.Off, "No Main Texture (Albedo Color Only)" },
			{ MainTexKeywords.NoMask, "Main Texture without Color Mask" },
			{ MainTexKeywords.ColorMask, "Main Texture with Color Mask" },
		};

		public static Dictionary<EmissionMode, string> emissionMode = new Dictionary<EmissionMode, string> {
			{ EmissionMode.Off, "No Emission (at all)" },
			{ EmissionMode.AlbedoNoMask, "Emission from Main Texture without Mask" },
			{ EmissionMode.AlbedoMask, "Emission from Main Texture with Mask" },
			{ EmissionMode.Custom, "Custom Emission Texture" },
		};

		public static string[] cutoutModeNames = Enum.GetNames(typeof(CutoutMode));

		public static Dictionary<ShadingMode, string> shadingModeNames = new Dictionary<ShadingMode, string>() {
			{ ShadingMode.CubedParadoxFLT, "CubedParadox FLT (I dislike this)" },
			{ ShadingMode.KawashirovFLTSingle, "Kawashirov FLT Single-Step Diffuse-based (Like CubedParadox, but better, also fast as fuck)" },
			{ ShadingMode.KawashirovFLTRamp, "Kawashirov FLT Ramp-based (In dev, but you can use it)" },
		};

		public static string[] distanceFadeModeNames = Enum.GetNames(typeof(DistanceFadeMode));
		public static string[] distanceFadeRandomNames = Enum.GetNames(typeof(DistanceFadeRandom));

		public static string[] FPSModeNames = Enum.GetNames(typeof(FPSMode));

		public static string[] outlineModeNames = Enum.GetNames(typeof(OutlineMode));

		public static string[] disintegrationModeNames = Enum.GetNames(typeof(DisintegrationMode));

		public static string[] polyColorWaveModeNames = Enum.GetNames(typeof(PolyColorWaveMode));

		static Commons()
		{
		
		}

		public static bool MaterialCheckTag(object material, string tag, string value)
		{
			var m = material as Material;
			var tag_v = m ? m.GetTag(tag, false, "") : null;
			// Debug.Log(String.Format("{0}: {1}={2}", material, tag, tag_v));
			return m && string.Equals(value, tag_v, StringComparison.InvariantCultureIgnoreCase);
		}

		public static bool CheckMaterialTagContains(object material, string tag, string value)
		{
			var m = material as Material;
			if (!m)
				return false;
			var tag_v = m.GetTag(tag, false, "");
			return tag_v.Split(',').ToList<string>().Any(v => string.Equals(value, v, StringComparison.InvariantCultureIgnoreCase));
		}

		public static bool CheckAllMaterialsTagContains(object[] materials, string tag, string value)
		{
			foreach (var material in materials)
				if (!CheckMaterialTagContains(material, tag, value))
					return false;
			return true;
		}

		public static void BakeTags(this StringBuilder sb, IDictionary<string, string> tags)
		{
			sb.Append("Tags {\n");
			foreach (var tag in tags)
				sb.Append("\"").Append(tag.Key).Append("\" = \"").Append(tag.Value).Append("\" ");
			sb.Append("}\n");
		}


		public static void BakeProperties(this StringBuilder sb, List<Property> properties)
		{
			sb.Append("Properties { ");
			foreach (var property in properties) {
				property.Bake(ref sb);
			}
			sb.Append("} ");
		}

		public static int PopupMaterialProperty(string label, MaterialProperty property, string[] displayedOptions, Action<bool> isChanged = null)
		{
			var value = (int)property.floatValue;
			EditorGUI.showMixedValue = property.hasMixedValue;
			EditorGUI.BeginChangeCheck();
			value = EditorGUILayout.Popup(label, value, displayedOptions);
			if (EditorGUI.EndChangeCheck()) {
				property.floatValue = (float)value;
				if (isChanged != null)
					isChanged(true);
			} else {
				if (isChanged != null)
					isChanged(false);
			}
			EditorGUI.showMixedValue = false;
			return value;
		}


	}


	public abstract class Property {
		public string name = null;
		public bool hidden = false; // TODO

		public abstract string Bake(ref StringBuilder sb);
	}

	public class Property2D : Property {
		public string defualt = "white";
		public bool isNormal = false;

		public void DefaultWhite()
		{
			this.defualt = "white";
			this.isNormal = false;
		}

		public void DefaultBlack()
		{
			this.defualt = "white";
			this.isNormal = false;
		}
		public void DefaultBump()
		{
			this.defualt = "bump";
			this.isNormal = true;
		}

		public override string Bake(ref StringBuilder sb)
		{
			if (this.isNormal)
				sb.Append("[Normal] ");
			sb.Append(this.name).Append("(\"").Append(this.name).Append("\", 2D) = \"").Append(this.defualt).Append("\" {}\n");
			return sb.ToString();
		}
	}

	public class PropertyColor : Property {
		public bool isHDR = true;
		public Color defualt = Color.white;

		public override string Bake(ref StringBuilder sb)
		{
			if (this.isHDR)
				sb.Append("[HDR] ");
			sb.Append(this.name).Append("(\"").Append(this.name).Append("\", Color) = (");
			sb.Append(this.defualt.r).Append(", ");
			sb.Append(this.defualt.g).Append(", ");
			sb.Append(this.defualt.b).Append(", ");
			sb.Append(this.defualt.a).Append(")\n");
			return sb.ToString();
		}
	}

	public class PropertyFloat : Property {
		public float defualt = 0;
		public Vector2? range = null;
		public float? power = null;


		public override string Bake(ref StringBuilder sb)
		{
			if (this.power.HasValue)
				sb.Append("[PowerSlider(").Append(this.power.Value).Append(")] ");
			sb.Append(this.name).Append("(\"").Append(this.name).Append("\", ");
			if (this.range.HasValue)
				sb.Append("Range(").Append(this.range.Value.x).Append(", ").Append(this.range.Value.y).Append(")");
			else
				sb.Append("Float");
			sb.Append(") = ").Append(this.defualt).Append("\n");
			return sb.ToString();
		}
	}

	public class PropertyVector : Property {
		public Vector4 defualt = Vector4.zero;

		public override string Bake(ref StringBuilder sb)
		{
			sb.Append(this.name).Append("(\"").Append(this.name).Append("\", Vector) = (");
			sb.Append(this.defualt.x).Append(", ");
			sb.Append(this.defualt.y).Append(", ");
			sb.Append(this.defualt.z).Append(", ");
			sb.Append(this.defualt.w).Append(")\n");
			return sb.ToString();
		}
	}

	public class ShaderSetup {
		public string name;
		public List<Property> properties = new List<Property>();
		public Dictionary<string, string> tags = new Dictionary<string, string>();
		public PassSetup forward = new PassSetup();
		public PassSetup forward_add = new PassSetup();
		public PassSetup shadowcaster = new PassSetup();

		public ShaderSetup()
		{
			this.forward.name = "FORWARD";
			this.forward.tags["LightMode"] = "ForwardBase";
			this.forward.multi_compile_fwdbase = true;
			this.forward.multi_compile_fwdadd_fullshadows = false;
			this.forward.multi_compile_shadowcaster = false;

			this.forward_add.name = "FORWARD_DELTA";
			this.forward_add.tags["LightMode"] = "ForwardAdd";
			this.forward_add.multi_compile_fwdbase = false;
			this.forward_add.multi_compile_fwdadd_fullshadows = true;
			this.forward_add.multi_compile_shadowcaster = false;

			this.shadowcaster.name = "SHADOW_CASTER";
			this.shadowcaster.tags["LightMode"] = "ShadowCaster";
			this.shadowcaster.multi_compile_fwdbase = false;
			this.shadowcaster.multi_compile_fwdadd_fullshadows = false;
			this.shadowcaster.multi_compile_shadowcaster = true;
		}

		public void SkipCommonStaticVariants(){
			this.forward.SkipCommonStaticVariants();
			this.forward_add.SkipCommonStaticVariants();
			this.shadowcaster.SkipCommonStaticVariants();
		}

		public void Define(string define)
		{
			this.forward.defines.Add(define);
			this.forward_add.defines.Add(define);
			this.shadowcaster.defines.Add(define);
		}
		public void Include(string include)
		{
			this.forward.includes.Add(include);
			this.forward_add.includes.Add(include);
			this.shadowcaster.includes.Add(include);
		}

		public void Debug(bool debug)
		{
			this.forward.enable_d3d11_debug_symbols = debug;
			this.forward_add.enable_d3d11_debug_symbols = debug;
			this.shadowcaster.enable_d3d11_debug_symbols = debug;
		}
		public void Bake(ref StringBuilder sb){
			sb.Append("Shader \"Kawashirov/Flat Lit Toon/");
			sb.Append(this.name);
			sb.Append("\" {\n");

			sb.BakeProperties(this.properties);

			sb.Append("SubShader {\n");
			sb.BakeTags(this.tags);

			this.forward.Bake(ref sb);
			this.forward_add.Bake(ref sb);
			this.shadowcaster.Bake(ref sb);

			sb.Append("}\n");
			sb.Append("FallBack \"Mobile/Diffuse\"\n");
			sb.Append("CustomEditor \"KawaFLTMaterialEditor\"\n");
			sb.Append("}\n");
		}

	}

	public class PassSetup {
		public string name;

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

		public void Clear()
		{
			this.cullMode = null;
			this.srcBlend = null;
			this.dstBlend = null;
			this.zWrite = null;

			this.stencilRef = null;
			this.stencilComp = null;
			this.stencilPass = null;
			this.stencilZFail = null;

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

		public string Bake(ref StringBuilder sb)
		{
			sb.Append("Pass { Name \"").Append(this.name).Append("\"\n");

			sb.BakeTags(this.tags);
			sb.Append("\n");

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

				sb.Append("}\n");
			}

			sb.Append("\nCGPROGRAM\n");

			foreach (var define in this.defines)
				sb.Append("#define ").Append(define).Append('\n');
			if (!string.IsNullOrEmpty(this.target))
				sb.Append("#pragma target ").Append(this.target).Append('\n');

			if (this.only_renderers.Count > 0) {
				sb.Append("#pragma only_renderers ");
				foreach (var renderer in this.only_renderers)
					sb.Append(renderer).Append(' ');
				sb.Append('\n');
			}
			if (this.multi_compile_fwdbase)
				sb.Append("#pragma multi_compile_fwdbase\n");
			if (this.multi_compile_fwdadd_fullshadows)
				sb.Append("#pragma multi_compile_fwdadd_fullshadows\n");
			if (this.multi_compile_shadowcaster)
				sb.Append("#pragma multi_compile_shadowcaster\n");
			if (this.multi_compile_fog)
				sb.Append("#pragma multi_compile_fog\n");
			if (this.multi_compile_instancing)
				sb.Append("#pragma multi_compile_instancing\n");
			if (!this.multi_compile_instancing)
				this.skip_variants.Add("INSTANCING_ON");
			if (this.skip_variants.Count > 0) {
				sb.Append("#pragma skip_variants ");
				foreach (var skip_variant in this.skip_variants)
					sb.Append(skip_variant).Append(' ');
				sb.Append('\n');
			}

			foreach (var include in this.includes)
				sb.Append("#include \"").Append(include).Append("\"\n");

			if (!string.IsNullOrEmpty(this.vertex))
				sb.Append("#pragma vertex ").Append(this.vertex).Append('\n');
			if (!string.IsNullOrEmpty(this.hull))
				sb.Append("#pragma hull ").Append(this.hull).Append('\n');
			if (!string.IsNullOrEmpty(this.domain))
				sb.Append("#pragma domain ").Append(this.domain).Append('\n');
			if (!string.IsNullOrEmpty(this.geometry))
				sb.Append("#pragma geometry ").Append(this.geometry).Append('\n');
			if (!string.IsNullOrEmpty(this.fragment))
				sb.Append("#pragma fragment ").Append(this.fragment).Append('\n');

			sb.Append("\nENDCG\n");
			sb.Append("}\n");
			return sb.ToString();
		}
	}


}