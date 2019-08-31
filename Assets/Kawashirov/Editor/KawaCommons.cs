
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

using EGUI = UnityEditor.EditorGUI;
using EGUIL = UnityEditor.EditorGUILayout;

namespace Kawashirov {
	public static class GeneralCommons {

		public static bool AnyNotNull<T>(params T[] objs)
		{
			foreach (var obj in objs) {
				if (obj != null)
					return true;
			}
			return false;
		}
	}

	public static class UnityMaterialCommons {
		// Облегчает трекинг сыылок и навигацию по коду.
		public static readonly string DisableBatching = "DisableBatching";
		public static readonly string ForceNoShadowCasting = "ForceNoShadowCasting";
		public static readonly string IgnoreProjector = "IgnoreProjector";
		public static readonly string RenderType = "RenderType";

		public static string MaterialTagGet(object material, string tag)
		{
			var m = material as Material;
			var tag_v = m ? m.GetTag(tag, false, "") : null;
			return tag_v;
		}

		public static bool MaterialTagIsSet(object material, string tag)
		{
			return !string.IsNullOrEmpty(MaterialTagGet(material, tag));
		}

		public static bool MaterialTagCheck(object material, string tag, string value)
		{
			var tag_v = MaterialTagGet(material, tag);
			return !string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(tag_v) && string.Equals(value, tag_v, StringComparison.InvariantCultureIgnoreCase);
		}

		public static bool MaterialTagContainsCheck(object material, string tag, string value)
		{
			var tag_v = MaterialTagGet(material, tag);
			return string.IsNullOrEmpty(tag_v)
				? false
				: tag_v.Split(',').ToList<string>().Any(v => string.Equals(value, v, StringComparison.InvariantCultureIgnoreCase));
		}

		public static bool MaterialTagBoolCheck(object material, string tag)
		{
			return MaterialTagCheck(material, tag, "True");
		}

		public static bool MaterialTagEnumCheck<E>(object material, string tag, E value)
		{
			return MaterialTagCheck(material, tag, Enum.GetName(typeof(E), value));
		}

		public static E MaterialTagEnumGet<E>(object material, string tag) where E : struct
		{
			var m = material as Material;
			if (!m)
				throw new ArgumentException(string.Format("No vaild material provided: {0}", material));
			var tag_v = m.GetTag(tag, false, "");
			if (string.IsNullOrEmpty(tag_v))
				throw new ArgumentException(string.Format("No vaild tag set in material: {0}.{1} = {2}", material, tag, tag_v));
			return (E)Enum.Parse(typeof(E), tag_v, true);
		}

		public static E MaterialTagEnumGet<E>(object material, string tag, E defualt) where E : struct
		{
			try {
				return MaterialTagEnumGet<E>(material, tag);
			} catch (Exception exc) {
				return defualt;
			}
		}

		public static E? MaterialTagEnumGetSafe<E>(object material, string tag, E? defualt = null) where E : struct
		{
			try {
				return MaterialTagEnumGet<E>(material, tag);
			} catch (Exception exc) {
				return defualt;
			}
		}

		public static int PopupMaterialProperty(string label, MaterialProperty property, string[] displayedOptions, Action<bool> isChanged = null)
		{
			var value = (int)property.floatValue;
			EGUI.showMixedValue = property.hasMixedValue;
			EGUI.BeginChangeCheck();
			value = EGUIL.Popup(label, value, displayedOptions);
			if (EGUI.EndChangeCheck()) {
				property.floatValue = (float)value;
				if (isChanged != null)
					isChanged(true);
			} else {
				if (isChanged != null)
					isChanged(false);
			}
			EGUI.showMixedValue = false;
			return value;
		}

	}

	public static class KawaCommonsTags {
		public static readonly string GenaratorGUID = "Kawa_GenaratorGUID";
		public static readonly string Feature_Debug = "Kawa_Feature_Debug";
	}

	[CanEditMultipleObjects]
	public class KawaEditor : Editor {

		public static bool PropertyEnumPopupCustomLabels<E>(
			SerializedProperty property, string label, Dictionary<E, string> labels = null,
			GUILayoutOption[] options = null
		) where E : struct, IConvertible, IComparable, IFormattable
		{
			var e_display = property.enumDisplayNames;
			var e_type = typeof(E);

			if (labels != null && e_type.IsEnum && labels.Count > 0) {
				var e_values = Enum.GetValues(e_type);
				var e_names = Enum.GetNames(e_type);
				for (var e_index = 0; e_index < e_names.Length; ++e_index) {
					var e_object = (E)Enum.Parse(typeof(E), e_names[e_index]);
					string custom_label;
					labels.TryGetValue(e_object, out custom_label);
					if (!string.IsNullOrEmpty(custom_label)) {
						e_display[e_index] = custom_label;
					}
				}
			}

			EGUI.BeginChangeCheck();
			var enumValueIndex = EGUIL.Popup(
				label, (!property.hasMultipleDifferentValues) ? property.enumValueIndex : -1, e_display, options
			);
			if (EGUI.EndChangeCheck()) {
				property.enumValueIndex = enumValueIndex;
				return true;
			}
			return false;
		}

		public static int PropertyMaskPopupCustomLabels(
			string label, SerializedProperty property, Type enum_t, Dictionary<int, string> labels = null,
			GUILayoutOption[] options = null
		)
		{
			// TODO

			return 0;
		}

		public void DefaultPrpertyField(SerializedProperty property, string label = null)
		{
			if (string.IsNullOrEmpty(label)) {
				EGUIL.PropertyField(property);
			} else {
				EGUIL.PropertyField(property, new GUIContent(label));
			}
		}

		public void DefaultPrpertyField(string name, string label = null)
		{
			this.DefaultPrpertyField(this.serializedObject.FindProperty(name), label);
		}

	}

}