using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;

#if UNITY_EDITOR

using UnityEditor;

namespace Kawashirov {
	public class KawaRSInspector : KawaShaderGUI {

		protected enum TexMode { NoRotaton, Single, Two, Four, Eight }
		protected static string[] TexModeNames = Enum.GetNames(typeof(TexMode));

		protected UnityEditor.MaterialEditor materialEditor = null;
		protected MaterialProperty[] materialProperties = null;
		protected bool updateMeta = false;
		protected bool checkValues = false;

		protected static bool MaterialCheckTag(object material, string tag, string value) {
			var m = material as Material;
			var tag_v = m ? m.GetTag(tag, false, "") : null;
			// Debug.Log(String.Format("{0}: {1}={2}", material, tag, tag_v));
			return m && string.Equals(value, tag_v, StringComparison.InvariantCultureIgnoreCase);
		}

		protected static bool MaterialCheckTagContains(object material, string tag, string value) {
			var m = material as Material;
			if (!m)
				return false;
			var tag_v = m.GetTag(tag, false, "");
			return tag_v.Split(',').ToList<string>().Any(v => string.Equals(value, v, StringComparison.InvariantCultureIgnoreCase));
		}

		protected static void SetKeyword(Material m, string keyword, bool state) {
			if (state)
				m.EnableKeyword(keyword);
			else
				m.DisableKeyword(keyword);
		}

		protected static void SetKeywords(MaterialEditor materialEditor, string keyword, bool state) {
			foreach (var obj in materialEditor.targets) {
				var mat = (Material)obj;
				SetKeyword(mat, keyword, state);
			}
		}

		protected virtual void SetupMaterialMeta(Material material) {
			SetupMaterialWithTexMode(material);
		}

		protected static void SetupMaterialWithTexMode(Material material) {
			var texMode = (TexMode)material.GetFloat("__TexMode");
			SetKeyword(material, "SIDES_OFF", texMode == TexMode.NoRotaton);
			SetKeyword(material, "SIDES_ONE", texMode == TexMode.Single);
			SetKeyword(material, "SIDES_TWO", texMode == TexMode.Two);
			SetKeyword(material, "SIDES_FOUR", texMode == TexMode.Four);
			SetKeyword(material, "SIDES_EIGHT", texMode == TexMode.Eight);
		}

		protected static int PopupProperty(string label, MaterialProperty property, string[] displayedOptions, Action<bool> isChanged = null) {
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

		protected void needUpdateMeta(bool need) {
			updateMeta = need || updateMeta;
		}

		protected void texturePropertySingleLine(MaterialProperty texture, string title, string hint) {
			EditorGUI.showMixedValue = texture.hasMixedValue;

			materialEditor.TexturePropertySingleLine(new GUIContent(title, hint), texture);

			EditorGUI.showMixedValue = false;
		}


		protected void OnGUI_Textures() {
			var texFront = FindProperty("_TexFront", materialProperties);
			var texFrontRight = FindProperty("_TexFrontRight", materialProperties);
			var texRight = FindProperty("_TexRight", materialProperties);
			var texBackRight = FindProperty("_TexBackRight", materialProperties);
			var texBack = FindProperty("_TexBack", materialProperties);
			var texBackLeft = FindProperty("_TexBackLeft", materialProperties);
			var texLeft = FindProperty("_TexLeft", materialProperties);
			var texFrontLeft = FindProperty("_TexFrontLeft", materialProperties);

			var texFallback = FindProperty("_MainTex", materialProperties);

			var mode = FindProperty("__TexMode", materialProperties);
			var modeEnum = (TexMode)PopupProperty("Texture sides", mode, TexModeNames, needUpdateMeta);

			EditorGUI.BeginChangeCheck();
			EditorGUI.indentLevel += 1;
			materialEditor.TextureScaleOffsetProperty(texFront);
			texturePropertySingleLine(texFront, "Front", "Front Color Texture (RGBA)");
			if (modeEnum == TexMode.Eight)
				texturePropertySingleLine(texFrontRight, "Front-Right", "Front-Right Color Texture (RGBA)");
			if (modeEnum == TexMode.Four || modeEnum == TexMode.Eight)
				texturePropertySingleLine(texRight, "Right", "Right Color Texture (RGBA)");
			if (modeEnum == TexMode.Eight)
				texturePropertySingleLine(texBackRight, "Back-Right", "Back-Right Color Texture (RGBA)");
			if (modeEnum == TexMode.Two || modeEnum == TexMode.Four || modeEnum == TexMode.Eight)
				texturePropertySingleLine(texBack, "Back", "Back Color Texture (RGBA)");
			if (modeEnum == TexMode.Eight)
				texturePropertySingleLine(texBackLeft, "Back-Left", "Back-Left Color Texture (RGBA)");
			if (modeEnum == TexMode.Four || modeEnum == TexMode.Eight)
				texturePropertySingleLine(texLeft, "Left", "Left Color Texture (RGBA)");
			if (modeEnum == TexMode.Eight)
				texturePropertySingleLine(texFrontLeft, "Front-Left", "Front-Left Color Texture (RGBA)");
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("This texture will be used when shaders blocked.");
			texturePropertySingleLine(texFallback, "Fallback", "Fallback Color Texture (RGBA)");
			materialEditor.TextureScaleOffsetProperty(texFallback);
			EditorGUI.indentLevel -= 1;
			var texChanged = EditorGUI.EndChangeCheck();
			if (texChanged) {
				// It's not used but keep sync
				texFrontRight.textureScaleAndOffset = texFront.textureScaleAndOffset;
				texRight.textureScaleAndOffset = texFront.textureScaleAndOffset;
				texBackRight.textureScaleAndOffset = texFront.textureScaleAndOffset;
				texBack.textureScaleAndOffset = texFront.textureScaleAndOffset;
				texBackLeft.textureScaleAndOffset = texFront.textureScaleAndOffset;
				texLeft.textureScaleAndOffset = texFront.textureScaleAndOffset;
				texFrontLeft.textureScaleAndOffset = texFront.textureScaleAndOffset;
			}

			EditorGUILayout.Space();

			var isCutout = MaterialCheckTag(materialEditor.target, "KawaRS_RenderType", "Cutout");
			if (isCutout) {
				materialEditor.ShaderProperty(FindProperty("_Cutoff", materialProperties), "Cutout");
			}

			materialEditor.ShaderProperty(FindProperty("_Color", materialProperties), "Color");
			materialEditor.ShaderProperty(FindProperty("_Emission", materialProperties), "Emission");
		}

		protected void OnGUI_TileAnim() {
			EditorGUILayout.LabelField("Tiled animation:");
			EditorGUI.indentLevel += 1;
			materialEditor.ShaderProperty(FindProperty("_xtiles", materialProperties), "X Tiles");
			materialEditor.ShaderProperty(FindProperty("_ytiles", materialProperties), "Y Tiles");
			materialEditor.ShaderProperty(FindProperty("_framerate", materialProperties), "Frames Per Second");
			materialEditor.ShaderProperty(FindProperty("_frame", materialProperties), "Manual Frame Number");
			EditorGUI.indentLevel -= 1;
		}

		public override void OnGUI(UnityEditor.MaterialEditor materialEditor, MaterialProperty[] properties) {
			this.materialEditor = materialEditor;
			materialProperties = properties;
			updateMeta = false;
			checkValues = false;

			checkValues = GUILayout.Button("Force fix values and compilation keywords");
			if (checkValues) {
				materialEditor.PropertiesChanged(); // TODO
			}

			EditorGUILayout.Space();
			OnGUI_Textures();

			EditorGUILayout.Space();
			OnGUI_TileAnim();

			KawaGUIUtility.ShaderEditorFooter();

			if (updateMeta || checkValues) {
				foreach (var obj in materialEditor.targets) {
					var mat = (Material)obj;
					SetupMaterialMeta(mat);
				}
			}

		}


	}
}
#endif
