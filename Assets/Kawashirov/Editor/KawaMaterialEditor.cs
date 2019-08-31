using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Kawashirov;
using System.Linq;

using UMC = Kawashirov.UnityMaterialCommons;

// Èìÿ ôàéëà äëæíî ñîâïàäàòü ñ èìåíåì òèïà.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/
// Òèï íå âêëş÷åí â íåéìñïåéñ Kawashirov, ò.ê. ıäèòîğ óêàçûâàåòñÿ â ôàéëå .shader áåç óêàçàíèÿ íåéìñïåéñà.

public class KawaMaterialEditor : MaterialEditor {

	protected static void HelpBoxRich(string msg)
	{
		var style = GUI.skin.GetStyle("HelpBox");
		var rt = style.richText;
		style.richText = true;
		EditorGUILayout.TextArea(msg, style);
		style.richText = rt;
	}

	protected MaterialProperty FindProperty(string name)
	{
		// GetMaterialProperty èíîãäà âîçâğàùàåò êàêèå-òî ñëîìàííûå îáúåêòû ïî÷åìó-òî
		var mp = GetMaterialProperty(this.targets, name);
		return mp != null && !string.IsNullOrEmpty(mp.name) && mp.targets != null && mp.targets.Length > 0 ? mp : null;
	}

	protected void TexturePropertySmol(GUIContent label, MaterialProperty prop)
	{
		// ¨ÁÀÍÛÅ ÂÛ ÑÓÊÀ ×ÅĞÒÈ ĞÀÇĞÀÁÎÒ×ÈÊÈ ŞÍÈÒÈ ß ÂÀÑ Â ĞÎÒ ÅÁÀË
		// ÊÀÊÎÃÎ ÕÓß ×ÒÎ ÁÛ ÏĞÎÑÒÎ ÏÎÑÒÀÂÈÒÜ ÈÊÎÍÊÓ Ñ ÒÅÊÑÒÓĞÎÉ ÑÏĞÀÂÀ, À ÍÅ ÑËÅÂÀ
		// ß ÄÎËÆÅÍ ÏÈÄÎĞÈÒÜ ÑÎÄÅĞÆÈÌÎÅ ÄËËÎÊ ×ÒÎ ÁÛ ÏÎÍßÒÜ ×¨ ÊÀÊ ĞÀÁÎÒÀÅÒ
		// È ÂÛÇÛÂÀÒÜ ÂÀØ ÏÈÄÎĞÑÊÈÉ ÃÎÂÍÎÊÎÄ ÑÅĞÅÇ ĞÅÔËÅÊØÅÍÛ?
		// ×¨ ÑËÎÆÍÎ ÁÛËÎ protected ÂÌÅÑÒÎ private ÏÎÑÒÀÂÈÒÜ?
		// ÊÀÊ ß ÁËßÒÜ İÊÑÒÅÍÄÈÒÜ İÄÈÒÎĞÛ ÄÎËÆÅÍ, ÑÓÊÀ?
		try {
			var flags = BindingFlags.Instance | BindingFlags.NonPublic;
			var m1 = typeof(MaterialEditor).GetMethod("GetControlRectForSingleLine", flags);
			var m2 = typeof(MaterialEditor).GetMethod("TexturePropertyBody", flags);

			var rect = (Rect)m1.Invoke(this, new object[] { });

			var rect_ctrl = EditorGUI.PrefixLabel(rect, label);

			var min = Math.Min(rect_ctrl.height, rect_ctrl.width);
			rect_ctrl.width = Math.Min(rect_ctrl.width, min);
			rect_ctrl.height = Math.Min(rect_ctrl.height, min);

			m2.Invoke(this, new object[] { rect_ctrl, prop });
		} catch (Exception exc) {
			// ÅÆÅËÈ ×ÒÎ-ÒÎ ÏÎØËÎ ÍÅ ÒÀÊ
			this.DefaultShaderProperty(prop, label.text);
		}
	}

	protected void TexturePropertySmol(GUIContent label, MaterialProperty prop, bool compatibility)
	{
		this.TexturePropertySmol(label, prop);
		if (compatibility) {
			this.TextureCompatibilityWarning(prop);
		}
	}

	protected void TexturePropertySmolDisabled(GUIContent label, MaterialProperty prop, bool compatibility = true)
	{
		using (new EditorGUI.DisabledScope(prop == null)) {
			if (prop != null) {
				this.TexturePropertySmol(label, prop, compatibility);
			} else {
				EditorGUILayout.LabelField(label, new GUIContent("Disabled"));
			}
		}

	}

	protected void ShaderPropertyDisabled(MaterialProperty property, string label = null)
	{
		var gui_label = new GUIContent(label); // null label is ok
		this.ShaderPropertyDisabled(property, gui_label);
	}

	protected void ShaderPropertyDisabled(MaterialProperty property, GUIContent label = null)
	{
		if (property != null) {
			this.ShaderProperty(property, label);
		} else {
			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.LabelField(label, new GUIContent("Disabled"));
			}
		}
	}

	protected static void LabelEnum<E>(string label, E value, Dictionary<E, string> display = null) where E : struct
	{
		string label2 = null;
		if (display != null && display.Count > 0) {
			display.TryGetValue(value, out label2);
		}
		if (string.IsNullOrEmpty(label2)) {
			label2 = Enum.GetName(typeof(E), value);
		}
		EditorGUILayout.LabelField(label, label2);
	}

	protected static void LabelEnumDisabled<E>(string label, E? value, Dictionary<E, string> display = null) where E : struct
	{
		if (value.HasValue) {
			LabelEnum(label, value.Value, display);
		} else if (!string.IsNullOrEmpty(label)) {
			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.LabelField(label, "Disabled");
			}
		}
	}

	protected static void LabelEnumDisabledFromTag<E>(
		string label, object material, string tag, Dictionary<E, string> display = null, E? defualt = null
	) where E : struct
	{
		var e = UMC.MaterialTagEnumGetSafe(material, tag, defualt);
		LabelEnumDisabled(label, e, display);
	}

	protected static void LabelEnumDisabledFromTagMixed<E>(
		string label, IEnumerable<object> materials, string tag, Dictionary<E, string> display = null, E? defualt = null
	) where E : struct
	{
		var values = new HashSet<E>();
		foreach (var material in materials) {
			var value = UMC.MaterialTagEnumGetSafe<E>(material, tag);
			if (value.HasValue) {
				values.Add(value.Value);
			}
		}
		if (values.Count < 1 && defualt.HasValue) {
			values.Add(defualt.Value);
		}
		if (values.Count < 1) {
			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.LabelField(label, "Disabled");
			}
		} else if (values.Count > 1) {
			EditorGUILayout.LabelField(label, "Mixed Values");
		} else {
			LabelEnum(label, values.First(), display);
		}
	}




}