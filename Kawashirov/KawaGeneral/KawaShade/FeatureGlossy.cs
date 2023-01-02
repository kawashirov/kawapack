using Kawashirov.ShaderBaking;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kawashirov.KawaShade {
	public class FeatureGlossy : AbstractFeature {
		internal static readonly string ShaderTag_Glossy = "KawaShade_Feature_Glossy";
		internal static readonly string ShaderTag_Mode = "KawaShade_Feature_GlossyMode";
		internal static readonly string ShaderTag_GlossinessType = "KawaShade_Feature_GlossinessType";
		internal static readonly string ShaderTag_GlossinessSource = "KawaShade_Feature_GlossinessSource";

		private static readonly GUIContent gui_feature_glossy = new GUIContent("Glossy Reflections Feature");

		public enum Mode { Metallic, Specular }
		public enum GlossinessType { Smoothness, PerceptualRoughness, Roughness }
		public enum GlossinessSource { None, AlbedoAlpha, MapAlpha, Separate }

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(ShaderTag_Glossy);
			tags.Add(ShaderTag_GlossinessType);
			tags.Add(ShaderTag_Mode);
		}

		public override void ConfigureShader(KawaShadeGenerator gen, ShaderSetup shader) {
			IncludeFeatureDirect(shader, "FeatureGlossy.hlsl");

			shader.TagBool(ShaderTag_Glossy, gen.glossy);
			if (gen.glossy) {
				shader.Define("GLOSSY_ON 1");

				shader.TagEnum(ShaderTag_GlossinessType, gen.glossinessType);
				float glossinessDefault = 0;
				if (gen.glossinessType == GlossinessType.Smoothness) {
					shader.Define("GLOSSY_SMOOTHNESS 1");
					glossinessDefault = 1;
				} else if (gen.glossinessType == GlossinessType.PerceptualRoughness) {
					shader.Define("GLOSSY_PERCEPTUAL_ROUGHNESS 1");
					glossinessDefault = 0;
				} else if (gen.glossinessType == GlossinessType.Roughness) {
					shader.Define("GLOSSY_ROUGHNESS 1");
					glossinessDefault = 0;
				}

				shader.properties.Add(new PropertyFloat() { name = "_Glossiness", defualt = glossinessDefault, range = Vector2.up });
				shader.TagEnum(ShaderTag_GlossinessSource, gen.glossinessSource);
				if (gen.glossinessSource == GlossinessSource.None) {
					shader.Define("GLOSSY_FROM_NONE");
				} else if (gen.glossinessSource == GlossinessSource.AlbedoAlpha) {
					shader.Define("GLOSSY_FROM_ALBEDO");
				} else if (gen.glossinessSource == GlossinessSource.MapAlpha) {
					shader.Define("GLOSSY_FROM_MAP"); // same as none if no map 
				} else if (gen.glossinessSource == GlossinessSource.Separate) {
					shader.Define("GLOSSY_FROM_SEPARATE");
					shader.properties.Add(new Property2D() { name = "_GlossinessMap", });
				}

				shader.TagEnum(ShaderTag_Mode, gen.glossyMode);
				if (gen.glossyMode == Mode.Metallic) {
					shader.Define("GLOSSY_METALLIC 1");
					if (gen.glossyMap) {
						shader.Define("GLOSSY_MAP 1");
						shader.properties.Add(new Property2D() { name = "_MetallicGlossMap", });
					}
					shader.properties.Add(new PropertyFloat() { name = "_Metallic", defualt = 1, range = Vector2.up });
				} else if (gen.glossyMode == Mode.Specular) {
					shader.Define("GLOSSY_SPECULAR 1");
					if (gen.glossyMap) {
						shader.Define("GLOSSY_MAP 1");
						shader.properties.Add(new Property2D() { name = "_SpecGlossMap", });
					}
					shader.properties.Add(new PropertyColor() { name = "_SpecColor" });
				}

			} else {
				shader.Define("GLOSSY_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var glossy = editor.serializedObject.FindProperty("glossy");
			KawaGUIUtility.ToggleLeft(glossy, gui_feature_glossy);
			using (new EditorGUI.DisabledScope(!glossy.hasMultipleDifferentValues && !glossy.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(editor, "glossyMode", "Setup");
					KawaGUIUtility.DefaultPrpertyField(editor, "glossyMap", "Use Map (Mask, Texture)");
					KawaGUIUtility.DefaultPrpertyField(editor, "glossinessType", "Type");
					KawaGUIUtility.DefaultPrpertyField(editor, "glossinessSource", "Source");
				}
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _Metallic = editor.FindProperty("_Metallic");
			var _MetallicGlossMap = editor.FindProperty("_MetallicGlossMap");

			var _SpecColor = editor.FindProperty("_SpecColor");
			var _SpecGlossMap = editor.FindProperty("_SpecGlossMap");

			var _Glossiness = editor.FindProperty("_Glossiness");
			var _GlossinessMap = editor.FindProperty("_GlossinessMap");

			var f_glossyMetallic = KawaUtilities.AnyNotNull(_Metallic, _MetallicGlossMap);
			var f_glossySpec = KawaUtilities.AnyNotNull(_SpecColor, _SpecGlossMap);
			var f_glossiness = KawaUtilities.AnyNotNull(_Glossiness, _GlossinessMap);
			var f_glossy = f_glossyMetallic || f_glossySpec || f_glossiness;

			using (new EditorGUI.DisabledScope(!f_glossy)) {
				EditorGUILayout.LabelField("Glossy Reflections Feature", f_glossy ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_glossyMetallic) {
						editor.ShaderPropertyDisabled(_Metallic, "Metallic");
						editor.TexturePropertySingleLineDisabled(new GUIContent("Metallic Map"), _MetallicGlossMap);
					}
					if (f_glossySpec) {
						editor.ShaderPropertyDisabled(_SpecColor, "Specular");
						editor.TexturePropertySingleLineDisabled(new GUIContent("Specular Map"), _SpecGlossMap);
					}
					if (f_glossiness) {
						string typeTagLabel = null;
						editor.shaderTags.TryGetValue(ShaderTag_GlossinessType, out var typeTag);
						if (typeTag != null) {
							typeTagLabel = typeTag.GetMultipleValues().SingleOrDefault();
						}
						if (string.IsNullOrWhiteSpace(typeTagLabel)) {
							typeTagLabel = "Glossiness";
						}
						editor.ShaderPropertyDisabled(_Glossiness, typeTagLabel);
						editor.TexturePropertySingleLineDisabled(new GUIContent($"{typeTagLabel} Map"), _GlossinessMap);
					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool glossy = false;
		public FeatureGlossy.Mode glossyMode = FeatureGlossy.Mode.Metallic;
		public bool glossyMap = false;
		public FeatureGlossy.GlossinessType glossinessType = FeatureGlossy.GlossinessType.Smoothness;
		public FeatureGlossy.GlossinessSource glossinessSource = FeatureGlossy.GlossinessSource.MapAlpha;
	}
}
