using System;
using System.Collections.Generic;
using Kawashirov.ShaderBaking;

namespace Kawashirov.FLT {

	public enum TessDomain { Triangles, Quads }
	public enum ShaderComplexity { VF, VGF, VHDGF }
	public enum TessPartitioning { Integer, FractionalEven, FractionalOdd, Pow2 }
	public enum BlendTemplate { Opaque, Cutout, Fade, FadeCutout, Custom = 256 }
	public enum MainTexKeywords { NoMainTex, NoMask, ColorMask }
	public enum CutoutMode { Classic, RangeRandom, RangeRandomH01 }
	public enum EmissionMode { AlbedoNoMask, AlbedoMask, Custom }
	public enum ShadingMode { CubedParadoxFLT, KawashirovFLTSingle, KawashirovFLTRamp }
	public enum MatcapMode { Replace, Multiple, Add }

	public enum DistanceFadeMode { Range, Infinity }
	//public enum DistanceFadeRandom { PerPixel, ScreenPattern }

	public enum FPSMode { ColorTint, DigitsTexture, DigitsMesh }

	public enum OutlineMode { Tinted, Colored }

	[Flags]
	public enum IWDDirections {
		Plane = 1,
		Random = 2,
		Normal = 4,
		ObjectVector = 8,
		WorldVector = 16,
	}

	public enum PolyColorWaveMode { Classic, KawaColorfulWaves }

	internal static class Commons {
		internal static readonly string RenderType = "KawaFLT_RenderType";
		internal static readonly string F_Debug = "KawaFLT_Feature_Debug"; // TODO
		internal static readonly string F_Instancing = "KawaFLT_Feature_Instancing";

		internal static readonly string F_Geometry = "KawaFLT_Feature_Geometry";
		internal static readonly string F_Tessellation = "KawaFLT_Feature_Tessellation";
		internal static readonly string F_Partitioning = "KawaFLT_Feature_Partitioning";
		internal static readonly string F_Domain = "KawaFLT_Feature_Domain";

		internal static readonly string F_Random = "KawaFLT_Feature_Random";

		internal static readonly string F_MainTex = "KawaFLT_Feature_MainTex";
		internal static readonly string F_Cutout_Forward = "KawaFLT_Feature_Cutout_Forward";
		internal static readonly string F_Cutout_ShadowCaster = "KawaFLT_Feature_Cutout_ShadowCaster";
		internal static readonly string F_Cutout_Classic = "KawaFLT_Feature_Cutout_Classic";
		internal static readonly string F_Cutout_RangeRandom = "KawaFLT_Feature_Cutout_RangeRandom";
		internal static readonly string F_Emission = "KawaFLT_Feature_Emission";
		internal static readonly string F_EmissionMode = "KawaFLT_Feature_EmissionMode";
		internal static readonly string F_NormalMap = "KawaFLT_Feature_NormalMap";

		internal static readonly string F_Shading = "KawaFLT_Feature_Shading";

		internal static readonly string F_DistanceFade = "KawaFLT_Feature_DistanceFade";
		internal static readonly string F_DistanceFadeMode = "KawaFLT_Feature_DistanceFadeMode";

		internal static readonly string F_Matcap = "KawaFLT_Feature_Matcap";
		internal static readonly string F_MatcapMode = "KawaFLT_Feature_MatcapMode";

		internal static readonly string F_WNoise = "KawaFLT_Feature_WNoise";

		internal static readonly string F_FPS = "KawaFLT_Feature_FPS";
		internal static readonly string F_FPSMode = "KawaFLT_Feature_FPSMode";

		internal static readonly string F_Outline = "KawaFLT_Feature_Outline";
		internal static readonly string F_OutlineMode = "KawaFLT_Feature_OutlineMode";

		internal static readonly string F_IWD = "KawaFLT_Feature_IWD";
		internal static readonly string F_IWDDirections = "KawaFLT_Feature_IWDDirections";

		internal static readonly string F_PCW = "KawaFLT_Feature_PCW";
		internal static readonly string F_PCWMode = "KawaFLT_Feature_PCWMode";

		internal static readonly Dictionary<ShaderComplexity, string> shaderComplexityNames = new Dictionary<ShaderComplexity, string> {
			{ ShaderComplexity.VF, "VF Lightweight (Vertex, Fragment)" },
			{ ShaderComplexity.VGF, "VGF Geometry (Vertex, Geometry, Fragment)" },
			{ ShaderComplexity.VHDGF, "VHDGF Tessellation+Geometry (Vertex, Hull, Domain, Geometry, Fragment)" },
		};

		internal static readonly Dictionary<MainTexKeywords, string> mainTexKeywordsNames = new Dictionary<MainTexKeywords, string> {
			{ MainTexKeywords.NoMainTex, "No Main Texture (Color Only)" },
			{ MainTexKeywords.NoMask, "Main Texture without Color Mask" },
			{ MainTexKeywords.ColorMask, "Main Texture with Color Mask" },
		};

		internal static readonly Dictionary<EmissionMode, string> emissionMode = new Dictionary<EmissionMode, string> {
			{ EmissionMode.AlbedoNoMask, "Emission from Main Texture without Mask" },
			{ EmissionMode.AlbedoMask, "Emission from Main Texture with Mask" },
			{ EmissionMode.Custom, "Custom Emission Texture" },
		};

		internal static readonly Dictionary<CutoutMode, string> cutoutModeNames = new Dictionary<CutoutMode, string>() {
			{ CutoutMode.Classic, "Classic (Single alpha value as threshold)" },
			{ CutoutMode.RangeRandom, "Random Range (Two alpha values defines range where texture randomly fades)" },
			{ CutoutMode.RangeRandomH01, "Random Range H01 (Same as Random Range, but also cubic Hermite spline smooth)" },
		};

		internal static readonly Dictionary<ShadingMode, string> shadingModeNames = new Dictionary<ShadingMode, string>() {
			{ ShadingMode.CubedParadoxFLT, "CubedParadox Flat Lit Toon" },
			{ ShadingMode.KawashirovFLTSingle, "Kawashirov Flat Lit Toon, Single-Step, Diffuse-based, Simple." },
			{ ShadingMode.KawashirovFLTRamp, "Kawashirov Flat Lit Toon, Ramp-based, In dev yet." },
		};

		internal static readonly Dictionary<ShadingMode, string> shadingModeDesc = new Dictionary<ShadingMode, string>() {
			{ ShadingMode.CubedParadoxFLT, "CubedParadox Flat Lit Toon. Legacy. Not recommended. And I dislike this." },
			{ ShadingMode.KawashirovFLTSingle, "Kawashirov Flat Lit Toon, Single-Step, Diffuse-based, Simple. Like CubedParadox, but better: supports more standard unity lighting features and also fast as fuck compare to other cbd-flt-like shaders." },
			{ ShadingMode.KawashirovFLTRamp, "Kawashirov Flat Lit Toon, Ramp-based, In dev yet, need extra tests in various conditions, but you can use it, It should work well." },
		};

		internal static string[] tags = new string[] {
			ShaderBaking.Commons.GenaratorGUID,
			//
			RenderType,
			F_Debug, F_Instancing,
			F_Geometry, F_Tessellation, F_Partitioning, F_Domain,
			F_Random,
			F_MainTex,
			F_Cutout_Forward, F_Cutout_ShadowCaster, F_Cutout_Classic, F_Cutout_RangeRandom,
			F_Emission, F_EmissionMode,
			F_NormalMap,
			F_Shading,
			F_DistanceFade, F_DistanceFadeMode, 
			F_Matcap, F_MatcapMode,
			F_FPS, F_FPSMode,
			F_Outline, F_OutlineMode,
			F_IWD, F_IWDDirections,
			F_PCW, F_PCWMode
		};

	}


}
