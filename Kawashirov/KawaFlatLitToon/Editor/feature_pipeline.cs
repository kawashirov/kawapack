using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using Kawashirov.FLT;

using GUIL = UnityEngine.GUILayout;
using EGUIL = UnityEditor.EditorGUILayout;
using EU = UnityEditor.EditorUtility;
using KST = Kawashirov.ShaderTag;
using KSBC = Kawashirov.ShaderBaking.Commons;
using KFLTC = Kawashirov.FLT.Commons;
using SC = Kawashirov.StaticCommons;

using static UnityEditor.EditorGUI;

namespace Kawashirov.FLT {
	public enum TessDomain { Triangles, Quads }
	public enum ShaderComplexity { VF, VGF, VHDGF }
	public enum TessPartitioning { Integer, FractionalEven, FractionalOdd, Pow2 }

	internal static partial class Commons {

		internal static readonly string F_Geometry = "KawaFLT_Feature_Geometry";
		internal static readonly string F_Tessellation = "KawaFLT_Feature_Tessellation";
		internal static readonly string F_Partitioning = "KawaFLT_Feature_Partitioning";
		internal static readonly string F_Domain = "KawaFLT_Feature_Domain";

		internal static readonly Dictionary<ShaderComplexity, string> shaderComplexityNames = new Dictionary<ShaderComplexity, string> {
			{ ShaderComplexity.VF, "VF Lightweight (Vertex, Fragment)" },
			{ ShaderComplexity.VGF, "VGF Geometry (Vertex, Geometry, Fragment)" },
			{ ShaderComplexity.VHDGF, "VHDGF Tessellation+Geometry (Vertex, Hull, Domain, Geometry, Fragment)" },
		};
	}

	public partial class Generator {
		public ShaderComplexity complexity = ShaderComplexity.VF;
		public TessPartitioning tessPartitioning = TessPartitioning.Integer;
		public TessDomain tessDomain = TessDomain.Triangles;

		private void ConfigureTess(ShaderSetup shader) {
			if (complexity != ShaderComplexity.VHDGF)
				return;

			shader.TagEnum(KFLTC.F_Partitioning, tessPartitioning);
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

			shader.TagEnum(KFLTC.F_Domain, tessDomain);
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

	public partial class GeneratorEditor {
		private void PipelineGUI() {
			var complexity = serializedObject.FindProperty("complexity");
			PropertyEnumPopupCustomLabels(complexity, "DX11 Pipeline Stages", KFLTC.shaderComplexityNames);

			complexity_VGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VGF;
			complexity_VHDGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VHDGF;

			using (new DisabledScope(!complexity_VHDGF)) {
				using (new IndentLevelScope()) {
					DefaultPrpertyField("tessPartitioning", "Tessellation Partitioning");
					DefaultPrpertyField("tessDomain", "Tessellation Domain (Primitive Topology)");
				}
			}
		}
	}
}

internal partial class KawaFLTShaderGUI {

	protected void OnGUI_Tessellation() {
		var _Tsltn_Uni = FindProperty("_Tsltn_Uni");
		var _Tsltn_Nrm = FindProperty("_Tsltn_Nrm");
		var _Tsltn_Inside = FindProperty("_Tsltn_Inside");
		var tessellation = _Tsltn_Uni != null && _Tsltn_Nrm != null && _Tsltn_Inside != null;
		using (new DisabledScope(!tessellation)) {
			if (tessellation) {
				EGUIL.LabelField("Tessellation", "Enabled");
				using (new IndentLevelScope()) {
					LabelShaderTagEnumValue<TessPartitioning>(KFLTC.F_Partitioning, "Partitioning", "Unknown");
					LabelShaderTagEnumValue<TessDomain>(KFLTC.F_Domain, "Domain", "Unknown");
					materialEditor.ShaderProperty(_Tsltn_Uni, "Uniform factor");
					materialEditor.ShaderProperty(_Tsltn_Nrm, "Factor from curvness");
					materialEditor.ShaderProperty(_Tsltn_Inside, "Inside multiplier");
				}
			} else {
				EGUIL.LabelField("Tessellation", "Disabled");
			}
		}
	}
}
