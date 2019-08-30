using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace Kawashirov.FLT {

	public enum TessDomain { Triangles, Quads }
	public enum ShaderComplexity { VF, VGF, VHDGF }
	public enum TessPartitioning { Integer, FractionalEven, FractionalOdd, Pow2 }
	public enum BlendTemplate { Opaque, Cutout, Fade, Custom = 256 }
	public enum MainTexKeywords { NoMainTex, NoMask, ColorMask }
	public enum CutoutMode { Classic, RangeRandom, RangePattern }
	public enum EmissionMode { AlbedoNoMask, AlbedoMask, Custom }

	public enum ShadingMode { CubedParadoxFLT, KawashirovFLTSingle, KawashirovFLTRamp }

	public enum DistanceFadeMode { Range, Infinity }
	//public enum DistanceFadeRandom { PerPixel, ScreenPattern }

	public enum FPSMode { ColorTint, DigitsTexture, DigitsMesh }

	public enum OutlineMode { Tinted, Colored }

	public enum PolyColorWaveMode { Classic, KawaColorfulWaves }

	static class Commons {
		
		public static readonly string Unity_Feature_DisableBatching = "DisableBatching";
		public static readonly string Unity_Feature_ForceNoShadowCasting = "ForceNoShadowCasting";
		public static readonly string Unity_Feature_IgnoreProjector = "IgnoreProjector";
		public static readonly string Unity_RenderType = "RenderType";

		public static readonly string KawaFLT_GenaratorGUID = "KawaFLT_GenaratorGUID";
		public static readonly string KawaFLT_RenderType = "KawaFLT_RenderType";
		public static readonly string KawaFLT_Feature_Instancing = "KawaFLT_Feature_Instancing";

		public static readonly string KawaFLT_Feature_Geometry = "KawaFLT_Feature_Geometry";
		public static readonly string KawaFLT_Feature_Tessellation = "KawaFLT_Feature_Tessellation";
		public static readonly string KawaFLT_Feature_Partitioning = "KawaFLT_Feature_Partitioning";
		public static readonly string KawaFLT_Feature_Domain = "KawaFLT_Feature_Domain";

		public static readonly string KawaFLT_Feature_Random = "KawaFLT_Feature_Random";

		public static readonly string KawaFLT_Feature_MainTex = "KawaFLT_Feature_MainTex";
		public static readonly string KawaFLT_Feature_Cutout = "KawaFLT_Feature_Cutout";
		public static readonly string KawaFLT_Feature_Emission = "KawaFLT_Feature_Emission";
		public static readonly string KawaFLT_Feature_EmissionMode = "KawaFLT_Feature_EmissionMode";
		public static readonly string KawaFLT_Feature_NormalMap = "KawaFLT_Feature_NormalMap";

		public static readonly string KawaFLT_Feature_Shading = "KawaFLT_Feature_Shading";

		public static readonly string KawaFLT_Feature_DistanceFade = "KawaFLT_Feature_DistanceFade";
		public static readonly string KawaFLT_Feature_DistanceFadeMode = "KawaFLT_Feature_DistanceFadeMode";
		//public static readonly string KawaFLT_Feature_DistanceFadeRandom = "KawaFLT_Feature_DistanceFadeRandom";

		public static readonly string KawaFLT_Feature_FPS = "KawaFLT_Feature_FPS";
		public static readonly string KawaFLT_Feature_FPSMode = "KawaFLT_Feature_FPSMode";

		public static readonly string KawaFLT_Feature_Outline = "KawaFLT_Feature_Outline";
		public static readonly string KawaFLT_Feature_OutlineMode = "KawaFLT_Feature_OutlineMode";

		public static readonly string KawaFLT_Feature_InfinityWarDecimation = "KawaFLT_Feature_InfinityWarDecimation";

		public static readonly string KawaFLT_Feature_PCW = "KawaFLT_Feature_PCW";
		public static readonly string KawaFLT_Feature_PCWMode = "KawaFLT_Feature_PCWMode";


		public static readonly string[] blendModeNames = Enum.GetNames(typeof(BlendMode));
		public static readonly string[] cullModeNames = Enum.GetNames(typeof(CullMode));

		public static readonly string[] blendTemplateNames = Enum.GetNames(typeof(BlendTemplate));

		public static readonly Dictionary<ShaderComplexity, string> shaderComplexityNames = new Dictionary<ShaderComplexity, string> {
			{ ShaderComplexity.VF, "VF Lightweight (Vertex/Fragment)" },
			{ ShaderComplexity.VGF, "VGF Geometry (Vertex/Geometry/Fragment)" },
			{ ShaderComplexity.VHDGF, "VHDGF Tessellation+Geometry (Vertex/Hull/Domain/Geometry/Fragment)" },
		};

		public static readonly Dictionary<MainTexKeywords, string> mainTexKeywordsNames = new Dictionary<MainTexKeywords, string> {
			{ MainTexKeywords.NoMainTex, "No Main Texture (Albedo Color Only)" },
			{ MainTexKeywords.NoMask, "Main Texture without Color Mask" },
			{ MainTexKeywords.ColorMask, "Main Texture with Color Mask" },
		};

		public static readonly Dictionary<EmissionMode, string> emissionMode = new Dictionary<EmissionMode, string> {
			{ EmissionMode.AlbedoNoMask, "Emission from Main Texture without Mask" },
			{ EmissionMode.AlbedoMask, "Emission from Main Texture with Mask" },
			{ EmissionMode.Custom, "Custom Emission Texture" },
		};

		public static readonly string[] cutoutModeNames = Enum.GetNames(typeof(CutoutMode));

		public static readonly Dictionary<ShadingMode, string> shadingModeDesc = new Dictionary<ShadingMode, string>() {
			{ ShadingMode.CubedParadoxFLT, "CubedParadox Flat Lit Toon. Legacy. Not recommended. And I dislike this." },
			{ ShadingMode.KawashirovFLTSingle, "Kawashirov Flat Lit Toon, Single-Step, Diffuse-based, Simple. Like CubedParadox, but better: supports more standard unity lighting features and also fast as fuck compare to other cbd-flt-like shaders." },
			{ ShadingMode.KawashirovFLTRamp, "Kawashirov Flat Lit Toon, Ramp-based, In dev yet, need extra tests in various conditions, but you can use it, It should work well." },
		};

		public static readonly string[] distanceFadeModeNames = Enum.GetNames(typeof(DistanceFadeMode));
		//public static readonly string[] distanceFadeRandomNames = Enum.GetNames(typeof(DistanceFadeRandom));

		public static readonly string[] FPSModeNames = Enum.GetNames(typeof(FPSMode));

		public static readonly string[] outlineModeNames = Enum.GetNames(typeof(OutlineMode));

		public static readonly string[] polyColorWaveModeNames = Enum.GetNames(typeof(PolyColorWaveMode));


		static Commons()
		{
		
		}

		public static string MaterialTagGet(object material, string tag)
		{
			var m = material as Material;
			var tag_v = m ? m.GetTag(tag, false, "") : null;
			return tag_v;
		}

		public static bool MaterialTagIsSet(object material, string tag)
		{
			return !string.IsNullOrEmpty(MaterialTagGet(material, tag));
		}

		public static bool MaterialTagCheck(object material, string tag, string value)
		{
			var tag_v = MaterialTagGet(material, tag);
			return !string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(tag_v) && string.Equals(value, tag_v, StringComparison.InvariantCultureIgnoreCase);
		}

		public static bool MaterialTagContainsCheck(object material, string tag, string value)
		{
			var tag_v = MaterialTagGet(material, tag);
			return string.IsNullOrEmpty(tag_v)
				? false
				: tag_v.Split(',').ToList<string>().Any(v => string.Equals(value, v, StringComparison.InvariantCultureIgnoreCase));
		}

		public static bool MaterialTagBoolCheck(object material, string tag) {
			return MaterialTagCheck(material, tag, "True");
		}

		public static bool MaterialTagEnumCheck<E>(object material, string tag, E value)
		{
			return MaterialTagCheck(material, tag, Enum.GetName(typeof(E), value));
		}

		public static E MaterialTagEnumGet<E>(object material, string tag) where E : struct
		{
			var m = material as Material;
			if (!m)
				throw new ArgumentException(string.Format("No vaild material provided: {0}", material));
			var tag_v = m.GetTag(tag, false, "");
			if (string.IsNullOrEmpty(tag_v))
				throw new ArgumentException(string.Format("No vaild tag set in material: {0}.{1} = {2}", material, tag, tag_v));
			return (E) Enum.Parse(typeof(E), tag_v, true);
		}

		public static E MaterialTagEnumGet<E>(object material, string tag, E defualt) where E : struct
		{
			try {
				return MaterialTagEnumGet<E>(material, tag);
			} catch (Exception exc) {
				return defualt;
			}
		}

		public static E? MaterialTagEnumGetSafe<E>(object material, string tag, E? defualt = null) where E : struct
		{
			try {
				return MaterialTagEnumGet<E>(material, tag);
			} catch (Exception exc) {
				return defualt;
			}
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
			this.TagBool("KawaFLT_Feature_Debug", debug);
			this.forward.enable_d3d11_debug_symbols = debug;
			this.forward_add.enable_d3d11_debug_symbols = debug;
			this.shadowcaster.enable_d3d11_debug_symbols = debug;
		}

		public void TagBool(string tag, bool value)
		{
			this.tags[tag] = value ? "True" : "False";
		}

		public void TagEnum<E>(string tag, E value)
		{
			this.tags[tag] = Enum.GetName(typeof(E), value);
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
			if (this.enable_d3d11_debug_symbols)
				sb.Append("#pragma enable_d3d11_debug_symbols\n");
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