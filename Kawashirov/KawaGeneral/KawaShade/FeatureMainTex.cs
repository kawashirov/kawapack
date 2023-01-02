using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	public class FeatureMainTex : AbstractFeature {
		internal static readonly string F_MainTex = "KawaShade_Feature_MainTex";

		public enum MainTexKeywords { NoMainTex, NoMask, ColorMask }

		internal static readonly Dictionary<MainTexKeywords, string> mainTexKeywordsNames = new Dictionary<MainTexKeywords, string> {
			{ MainTexKeywords.NoMainTex, "No Main Texture (Color Only)" },
			{ MainTexKeywords.NoMask, "Main Texture without Color Mask" },
			{ MainTexKeywords.ColorMask, "Main Texture with Color Mask" },
		};

		public override int GetOrder() => (int)Order.GENERAL + 500;

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_MainTex);
		}

		public override void ConfigureShader(KawaShadeGenerator gen, ShaderSetup shader) {
			var mainTex = gen.mainTex;

			shader.TagEnum(F_MainTex, mainTex);

			switch (gen.mainTex) {
				case MainTexKeywords.NoMainTex:
					shader.Define("MAINTEX_OFF 1");
					break;
				case MainTexKeywords.NoMask:
					shader.Define("MAINTEX_NOMASK 1");
					break;
				case MainTexKeywords.ColorMask:
					shader.Define("MAINTEX_COLORMASK 1");
					break;
			}

			if (gen.mainTex == MainTexKeywords.ColorMask || gen.mainTex == MainTexKeywords.NoMask) {
				shader.properties.Add(new Property2D() { name = "_MainTex" });
				if (gen.mainTex == MainTexKeywords.ColorMask) {
					shader.properties.Add(new Property2D() { name = "_ColorMask", defualt = "black" });
				}
				if (gen.mainTexSeparateAlpha) {
					shader.Define("MAINTEX_SEPARATE_ALPHA 1");
					shader.properties.Add(new Property2D() { name = "_MainTexAlpha", defualt = "white" });
				}
			}

			shader.properties.Add(new PropertyColor() { name = "_Color" });
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var mainTex = editor.serializedObject.FindProperty("mainTex");
			KawaGUIUtility.PropertyEnumPopupCustomLabels(mainTex, "Albedo (Main) Texture", mainTexKeywordsNames);
			using (new EditorGUI.IndentLevelScope()) {
				KawaGUIUtility.DefaultPrpertyField(editor, "mainTexSeparateAlpha", "Alpha in separate texture");
			}
		}

		internal static readonly GUIContent _MainTex_label =
			new GUIContent("Albedo (Main) Texture", "Albedo Main Color Texture (RGBA)");
		internal static readonly GUIContent _ColorMask_label =
			new GUIContent("Color Mask", "Masks Color Tint (R)");

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _MainTex = editor.FindProperty("_MainTex");
			var _Color = editor.FindProperty("_Color");
			var _ColorMask = editor.FindProperty("_ColorMask");
			var _MainTexAlpha = editor.FindProperty("_MainTexAlpha");

			editor.TexturePropertySingleLineDisabled(_MainTex_label, _MainTex);

			using (new EditorGUI.IndentLevelScope()) {
				var _MainTexAlpha_label = new GUIContent("Alpha (of Main Texture)", "Separate Alpha-channel for Main Color Texture (R)");
				editor.TexturePropertySingleLineDisabled(_MainTexAlpha_label, _MainTexAlpha);

				editor.ShaderPropertyDisabled(_Color, "Color");

				editor.TexturePropertySingleLineDisabled(_ColorMask_label, _ColorMask);
			}
		}
	}

	public partial class KawaShadeGenerator {
		public FeatureMainTex.MainTexKeywords mainTex = FeatureMainTex.MainTexKeywords.ColorMask;
		public bool mainTexSeparateAlpha = false;
	}
}
