using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using Kawashirov.KawaShade;

namespace Kawashirov.KawaShade {
	public enum TessDomain { Triangles, Quads }
	public enum ShaderComplexity { VF, VGF, VHDGF }
	public enum TessPartitioning { Integer, FractionalEven, FractionalOdd, Pow2 }

	internal static partial class KawaShadeCommons {

		internal static readonly string F_Geometry = "KawaShade_Feature_Geometry";
		internal static readonly string F_Tessellation = "KawaShade_Feature_Tessellation";
		internal static readonly string F_Partitioning = "KawaShade_Feature_Partitioning";
		internal static readonly string F_Domain = "KawaShade_Feature_Domain";

		internal static readonly Dictionary<ShaderComplexity, string> shaderComplexityNames = new Dictionary<ShaderComplexity, string> {
			{ ShaderComplexity.VF, "VF Lightweight (Vertex, Fragment)" },
			{ ShaderComplexity.VGF, "VGF Geometry (Vertex, Geometry, Fragment)" },
			{ ShaderComplexity.VHDGF, "VHDGF Tessellation+Geometry (Vertex, Hull, Domain, Geometry, Fragment)" },
		};
	}

	public partial class KawaShadeGenerator {
		public ShaderComplexity complexity = ShaderComplexity.VF;
		public TessPartitioning tessPartitioning = TessPartitioning.Integer;
		public TessDomain tessDomain = TessDomain.Triangles;

		private void ConfigureTess(ShaderSetup shader) {
			if (complexity != ShaderComplexity.VHDGF)
				return;

			shader.TagEnum(KawaShadeCommons.F_Partitioning, tessPartitioning);
			switch (tessPartitioning) {
				case TessPartitioning.Integer:
					shader.Define("TESS_P_INT 1");
					break;
				case TessPartitioning.FractionalEven:
					shader.Define("TESS_P_EVEN 1");
					break;
				case TessPartitioning.FractionalOdd:
					shader.Define("TESS_P_ODD 1");
					break;
				case TessPartitioning.Pow2:
					shader.Define("TESS_P_POW2 1");
					break;
			}

			shader.TagEnum(KawaShadeCommons.F_Domain, tessDomain);
			switch (tessDomain) {
				case TessDomain.Triangles:
					shader.Define("TESS_D_TRI 1");
					break;
				case TessDomain.Quads:
					shader.Define("TESS_D_QUAD 1");
					break;
			}

			shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Uni", defualt = 1, range = new Vector2(0.9f, 10), power = 2 });
			shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Nrm", defualt = 1, range = new Vector2(0, 20), power = 2 });
			shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Inside", defualt = 1, range = new Vector2(0.1f, 10), power = 10 });
		}
	}

	public partial class KawaShadeGeneratorEditor {
		private void PipelineGUI() {
			var complexity = serializedObject.FindProperty("complexity");
			KawaGUIUtility.PropertyEnumPopupCustomLabels(complexity, "DX11 Pipeline Stages", KawaShadeCommons.shaderComplexityNames);

			complexity_VGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VGF;
			complexity_VHDGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VHDGF;

			using (new EditorGUI.DisabledScope(!complexity_VHDGF)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(this, "tessPartitioning", "Tessellation Partitioning");
					KawaGUIUtility.DefaultPrpertyField(this, "tessDomain", "Tessellation Domain (Primitive Topology)");
				}
			}
		}
	}

	internal partial class KawaShadeGUI {

		protected void OnGUI_Tessellation() {
			var _Tsltn_Uni = FindProperty("_Tsltn_Uni");
			var _Tsltn_Nrm = FindProperty("_Tsltn_Nrm");
			var _Tsltn_Inside = FindProperty("_Tsltn_Inside");
			var tessellation = _Tsltn_Uni != null && _Tsltn_Nrm != null && _Tsltn_Inside != null;
			using (new EditorGUI.DisabledScope(!tessellation)) {
				if (tessellation) {
					EditorGUILayout.LabelField("Tessellation", "Enabled");
					using (new EditorGUI.IndentLevelScope()) {
						LabelShaderTagEnumValue<TessPartitioning>(KawaShadeCommons.F_Partitioning, "Partitioning", "Unknown");
						LabelShaderTagEnumValue<TessDomain>(KawaShadeCommons.F_Domain, "Domain", "Unknown");
						materialEditor.ShaderProperty(_Tsltn_Uni, "Uniform factor");
						materialEditor.ShaderProperty(_Tsltn_Nrm, "Factor from curvness");
						materialEditor.ShaderProperty(_Tsltn_Inside, "Inside multiplier");
					}
				} else {
					EditorGUILayout.LabelField("Tessellation", "Disabled");
				}
			}
		}
	}
}
