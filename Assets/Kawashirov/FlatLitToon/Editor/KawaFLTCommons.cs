using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace Kawashirov.FLT {

	internal enum TessDomain { Triangles, Quads }
	internal enum ShaderComplexity { VF, VGF, VHDGF }
	internal enum TessPartitioning { Integer, FractionalEven, FractionalOdd, Pow2 }
	internal enum BlendTemplate { Opaque, Cutout, Fade, FadeCutout, Custom = 256 }
	internal enum MainTexKeywords { NoMainTex, NoMask, ColorMask }
	internal enum CutoutMode { Classic, RangeRandom, RangeRandomH01 }
	internal enum EmissionMode { AlbedoNoMask, AlbedoMask, Custom }
	internal enum ShadingMode { CubedParadoxFLT, KawashirovFLTSingle, KawashirovFLTRamp }
	internal enum MatcapMode { Replace, Multiple, Add }

	internal enum DistanceFadeMode { Range, Infinity }
	//public enum DistanceFadeRandom { PerPixel, ScreenPattern }

	internal enum FPSMode { ColorTint, DigitsTexture, DigitsMesh }

	internal enum OutlineMode { Tinted, Colored }

	[Flags]
	internal enum IWDDirections {
		Plane = 1,
		Random = 2,
		Normal = 4,
		ObjectVector = 8,
		WorldVector = 16,
	}

	internal enum PolyColorWaveMode { Classic, KawaColorfulWaves }

	internal static class Commons {
		internal static readonly string RenderType = "KawaFLT_RenderType";
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

	}


}