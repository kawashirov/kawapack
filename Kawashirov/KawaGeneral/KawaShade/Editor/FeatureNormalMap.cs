using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	public class FeatureNormalMap : AbstractFeature {
		internal static readonly string F_NormalMap = "KawaShade_Feature_NormalMap";

		public override int GetOrder() => (int)Order.GENERAL + 520;

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_NormalMap);
		}

		public override void ConfigureShader(KawaShadeGenerator gen, ShaderSetup shader) {
			shader.TagBool(F_NormalMap, gen.bumpMap);
			if (gen.bumpMap) {
				shader.forward.defines.Add("_NORMALMAP");
				shader.forward_add.defines.Add("_NORMALMAP");
				shader.properties.Add(new Property2D() { name = "_BumpMap", defualt = "bump", isNormal = true });
				shader.properties.Add(new PropertyFloat() { name = "_BumpScale", defualt = 1.0f });
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			KawaGUIUtility.DefaultPrpertyField(editor, "bumpMap");
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _BumpMap = editor.FindProperty("_BumpMap");
			var label = new GUIContent("Normal Map", "Normal (Bump) Map Texture (RGB)");
			editor.TexturePropertySingleLineDisabled(label, _BumpMap);
			if (_BumpMap != null && _BumpMap.textureValue == null) {
				EditorGUILayout.HelpBox(
					"Normal map texture is not set! Disable normal feature in shader generator, if you don't need this.",
					MessageType.Warning
				);
			}
			using (new EditorGUI.IndentLevelScope()) {
				var _BumpScale = editor.FindProperty("_BumpScale");
				editor.ShaderPropertyDisabled(_BumpScale, "Normal Map Scale");
				if (_BumpScale != null && _BumpScale.floatValue < 0.05) {
					EditorGUILayout.HelpBox(
						"Normal map scale value is close to zero! In this situation, may be it's better to disable normal feature in shader generator, if you don't need this?",
						MessageType.Warning
					);
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool bumpMap = false;
	}
}
