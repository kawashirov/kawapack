using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;

public class KawaRSInspector : ShaderGUI {

	protected enum TexMode { NoRotaton, Single, Two, Four, Eight }
	protected static string[] TexModeNames = Enum.GetNames(typeof(TexMode));

	protected MaterialEditor materialEditor = null;
	protected MaterialProperty[] materialProperties = null;
	protected bool updateMeta = false;
	protected bool checkValues = false;

	protected static bool MaterialCheckTag(object material, string tag, string value) {
		Material m = material as Material;
		string tag_v = m ? m.GetTag(tag, false, "") : null;
		// Debug.Log(String.Format("{0}: {1}={2}", material, tag, tag_v));
		return m && string.Equals(value, tag_v, StringComparison.InvariantCultureIgnoreCase);
	}

	protected static bool MaterialCheckTagContains(object material, string tag, string value) {
		Material m = material as Material;
		if (!m) return false;
		string tag_v = m.GetTag(tag, false, "");
		return tag_v.Split(',').ToList<string>().Any(v => string.Equals(value, v, StringComparison.InvariantCultureIgnoreCase));
	}

	protected static void SetKeyword(Material m, string keyword, bool state) {
		if (state)
			m.EnableKeyword(keyword);
		else
			m.DisableKeyword(keyword);
	}

	protected static void SetKeywords(MaterialEditor materialEditor, string keyword, bool state) {
		foreach(var obj in materialEditor.targets) {
			Material mat = (Material) obj;
			SetKeyword(mat, keyword, state);
		}
	}

	protected virtual void SetupMaterialMeta(Material material) {
		SetupMaterialWithTexMode(material);
	}

	protected static void SetupMaterialWithTexMode(Material material) {
		TexMode texMode = (TexMode) material.GetFloat("__TexMode");
		SetKeyword(material, "SIDES_OFF", texMode == TexMode.NoRotaton);
		SetKeyword(material, "SIDES_ONE", texMode == TexMode.Single);
		SetKeyword(material, "SIDES_TWO", texMode == TexMode.Two);
		SetKeyword(material, "SIDES_FOUR", texMode == TexMode.Four);
		SetKeyword(material, "SIDES_EIGHT", texMode == TexMode.Eight);
	}

	protected static int PopupProperty(string label, MaterialProperty property, string[] displayedOptions, Action<bool> isChanged = null) {
		int value = (int) property.floatValue;
		EditorGUI.showMixedValue = property.hasMixedValue;
		EditorGUI.BeginChangeCheck();
		value = EditorGUILayout.Popup(label, value, displayedOptions);
		if (EditorGUI.EndChangeCheck() ) {
			property.floatValue = (float) value;
			if (isChanged != null) isChanged(true);
		} else {
			if (isChanged != null) isChanged(false);
		}
		EditorGUI.showMixedValue = false;
		return value;
	}

	protected void needUpdateMeta(bool need) {
		this.updateMeta = need || this.updateMeta;
	}

	protected void texturePropertySingleLine(MaterialProperty texture, string title, string hint) {
		EditorGUI.showMixedValue = texture.hasMixedValue;
		
		materialEditor.TexturePropertySingleLine(new GUIContent(title, hint), texture);
		
		EditorGUI.showMixedValue = false;
	}


	protected void OnGUI_Textures() {
		MaterialProperty texFront = FindProperty("_TexFront", this.materialProperties);
		MaterialProperty texFrontRight = FindProperty("_TexFrontRight", this.materialProperties);
		MaterialProperty texRight = FindProperty("_TexRight", this.materialProperties);
		MaterialProperty texBackRight = FindProperty("_TexBackRight", this.materialProperties);
		MaterialProperty texBack = FindProperty("_TexBack", this.materialProperties);
		MaterialProperty texBackLeft = FindProperty("_TexBackLeft", this.materialProperties);
		MaterialProperty texLeft = FindProperty("_TexLeft", this.materialProperties);
		MaterialProperty texFrontLeft = FindProperty("_TexFrontLeft", this.materialProperties);

		MaterialProperty texFallback = FindProperty("_MainTex", this.materialProperties);

		MaterialProperty mode = FindProperty("__TexMode", this.materialProperties);
		var modeEnum = (TexMode) PopupProperty("Texture sides", mode, TexModeNames, this.needUpdateMeta);

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
		bool texChanged = EditorGUI.EndChangeCheck();
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

		bool isCutout = MaterialCheckTag(this.materialEditor.target, "KawaRS_RenderType", "Cutout");
		if (isCutout) {
			materialEditor.ShaderProperty(FindProperty("_Cutoff", this.materialProperties), "Cutout");
		}

		materialEditor.ShaderProperty(FindProperty("_Color", this.materialProperties), "Color");
		materialEditor.ShaderProperty(FindProperty("_Emission", this.materialProperties), "Emission");
	}

	protected void OnGUI_TileAnim() {
		EditorGUILayout.LabelField("Tiled animation:");
		EditorGUI.indentLevel += 1;
			materialEditor.ShaderProperty(FindProperty("_xtiles", this.materialProperties), "X Tiles");
			materialEditor.ShaderProperty(FindProperty("_ytiles", this.materialProperties), "Y Tiles");
			materialEditor.ShaderProperty(FindProperty("_framerate", this.materialProperties), "Frames Per Second");
			materialEditor.ShaderProperty(FindProperty("_frame", this.materialProperties), "Manual Frame Number");
		EditorGUI.indentLevel -= 1;
	}


	public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] materialProperties) {
		this.materialEditor = materialEditor;
		this.materialProperties = materialProperties;
		this.updateMeta = false;
		this.checkValues = false;
		
		this.checkValues = GUILayout.Button("Force fix values and compilation keywords");
		if (this.checkValues) {
			materialEditor.PropertiesChanged(); // TODO
		}

		EditorGUILayout.Space();
		OnGUI_Textures();

		EditorGUILayout.Space();
		OnGUI_TileAnim();

		KawaEditorUtil.ShaderEditorFooter();

		if (this.updateMeta || this.checkValues) {
			foreach (var obj in materialEditor.targets) {
				Material mat = (Material) obj;
				SetupMaterialMeta(mat);
			}
		}

	}


}
