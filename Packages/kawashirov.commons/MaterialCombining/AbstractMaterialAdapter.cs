#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kawashirov.MaterialCombining {
	/**
		Этот класс преобразует настройки материала в универсальные DataChannelы.
		Комбайнер Материалов комбинирует именно DataChannelы.

		Получается Так:
		"Material 1" -(Adapter A)-> List<DataChannel> - \
		"Material 2" -(Adapter B)-> List<DataChannel> - - -> MaterialCombiner -> List<DataChannel> -> "Atlassed Material"
		"Material 3" -(Adapter C)-> List<DataChannel> - /

	*/
	public abstract class AbstractMaterialAdapter : KawaEditorBehaviour {
		// Должен вернуть null, если 

		protected static bool Equals(string a, string b) {
			return string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);
		}

		protected static bool Contains(string where, string what) {
			return !string.IsNullOrWhiteSpace(where) && where.Contains(what, StringComparison.InvariantCultureIgnoreCase);
		}

		protected bool GetCommon(Material mat, string prop_name,
			out Shader shader, out int prop_index, out ShaderPropertyType prop_type) {
			// Возвращает true если вызывающему нужно отказаться от этой проперти
			shader = mat.shader;
			prop_index = -1;
			prop_type = ShaderPropertyType.Color;

			if (shader == null)
				return true;

			prop_index = shader.FindPropertyIndex(name);
			if (prop_index < 0)
				return true;

			prop_type = shader.GetPropertyType(prop_index);
			return false;
		}

		protected Texture2D GetTexture2D(Material mat, string prop_name, Texture2D default_) {
			if (GetCommon(mat, prop_name, out var shader, out var prop_index, out var prop_type))
				return default_;
			if (prop_type != ShaderPropertyType.Texture)
				return default_;
			if (shader.GetPropertyTextureDimension(prop_index) != TextureDimension.Tex2D)
				return default_; // Only Tex2D supported for now

			return mat.GetTexture(prop_name) as Texture2D;
		}

		protected Color GetColor(Material mat, string prop_name, Color default_) {
			if (GetCommon(mat, prop_name, out var shader, out var prop_index, out var prop_type))
				return default_;
			if (prop_type != ShaderPropertyType.Color)
				return default_;

			return mat.GetColor(name);
		}

		protected virtual float GetScalar(Material mat, string prop_name, float default_) {
			if (GetCommon(mat, prop_name, out var shader, out var prop_index, out var prop_type))
				return default_;
			if (prop_type == ShaderPropertyType.Float)
				return mat.GetFloat(prop_name);
			if (prop_type == ShaderPropertyType.Int)
				return mat.GetInteger(prop_name);
			return default_;
		}

		// Возвращает названия DataChannelов которые понимает этот адаптер
		public abstract ICollection<string> SupportedFeatures();

		// Возвращает true, если адаптер понимает данный материал и может привести его к DataChannelам.
		public abstract bool CanAdaptMaterial(Material mat);

		public abstract List<DataChannel> MaterialToData(Material mat);

		public abstract void DataToMaterial(List<DataChannel> data, Material atlassed);
	}
}
#endif