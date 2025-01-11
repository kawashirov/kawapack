#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kawashirov.MaterialCombining {
	public static class MatCmbUtility {

		public static bool Equals(string a, string b) {
			return string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);
		}

		public static bool Contains(string where, string what) {
			return !string.IsNullOrWhiteSpace(where) && where.Contains(what, StringComparison.InvariantCultureIgnoreCase);
		}

		public static int ChCharToIndex(char channel) {
			if (channel == 'R')
				return 0;
			if (channel == 'G')
				return 1;
			if (channel == 'B')
				return 2;
			if (channel == 'A')
				return 3;
			return -1;
		}

		public static bool GetCommon(Material mat, string prop_name,
			out Shader shader, out int prop_index, out ShaderPropertyType prop_type) {
			// Возвращает true если вызывающему нужно отказаться от этой проперти
			shader = mat.shader;
			prop_index = -1;
			prop_type = ShaderPropertyType.Color;

			if (shader == null) {
				Debug.LogWarning($"Material {mat} has no valid shader attached.", mat);
				return true;
			}

			prop_index = shader.FindPropertyIndex(prop_name);
			if (prop_index < 0) {
				Debug.LogWarning($"Material {mat} shader {shader} has no property \"{prop_name}\"", mat);
				return true;
			}

			prop_type = shader.GetPropertyType(prop_index);
			return false;
		}

		public static float GetScalar(Material mat, string prop_name, float default_) {
			if (GetCommon(mat, prop_name, out var shader, out var prop_index, out var prop_type))
				return default_;
			if (prop_type == ShaderPropertyType.Float || prop_type == ShaderPropertyType.Range)
				return mat.GetFloat(prop_name);
			if (prop_type == ShaderPropertyType.Int)
				return mat.GetInt(prop_name);
			return default_;
		}

		public static void RemoveAllTextures(this Material mat) {
			// Можно использовать в MakeNewAtlasMaterial
			foreach (var tex_name in mat.GetTexturePropertyNames())
				mat.SetTexture(tex_name, null);
		}

		public static void SetTextureNoST(Material mat, string prop_name, Texture2D tex) {
			mat.SetTexture(prop_name, tex);
			mat.SetTextureScale(prop_name, Vector2.one);
			mat.SetTextureOffset(prop_name, Vector2.zero);
		}

		public static MaterialGlobalIlluminationFlags DefaultMaterialGlobalIlluminationFlags() {
			// See MaterialEditor.EmissionEnabledProperty()
			var lm = Lightmapping.lightingSettingsDefaults;
			try {
				lm = Lightmapping.lightingSettings;
			} catch (Exception) {
				// suppress "Please assign it to an existing asset or a new instance"
			}
			return lm.realtimeGI ? MaterialGlobalIlluminationFlags.RealtimeEmissive : (lm.bakedGI ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.None);
		}
	}
}
#endif