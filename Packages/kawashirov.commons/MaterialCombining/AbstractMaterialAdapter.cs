#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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
		protected readonly List<DataChannelDescriptor> descriptors = new List<DataChannelDescriptor>();

		/* libarary methods */

		protected static bool Equals(string a, string b) {
			return string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);
		}

		protected static bool Contains(string where, string what) {
			return !string.IsNullOrWhiteSpace(where) && where.Contains(what, StringComparison.InvariantCultureIgnoreCase);
		}

		protected static int ChCharToIndex(char channel) {
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

		protected bool GetCommon(Material mat, string prop_name,
			out Shader shader, out int prop_index, out ShaderPropertyType prop_type) {
			// Возвращает true если вызывающему нужно отказаться от этой проперти
			shader = mat.shader;
			prop_index = -1;
			prop_type = ShaderPropertyType.Color;

			if (shader == null) {
				LogWarning($"Material {mat} has no valid shader attached.");
				return true;
			}

			prop_index = shader.FindPropertyIndex(prop_name);
			if (prop_index < 0) {
				LogWarning($"Material {mat} shader {shader} has no property \"{prop_name}\"");
				return true;
			}

			prop_type = shader.GetPropertyType(prop_index);
			return false;
		}

		protected Texture2D GetTexture2D(Material mat, string prop_name, Texture2D default_) {
			if (GetCommon(mat, prop_name, out var shader, out var prop_index, out var prop_type))
				return default_;
			if (prop_type != ShaderPropertyType.Texture) {
				LogWarning($"Material {mat} shader {shader} property \"{prop_name}\" type is not Texture: {prop_type}.");
				return default_;
			}
			var dim = shader.GetPropertyTextureDimension(prop_index);
			if (dim != TextureDimension.Tex2D) {
				LogWarning($"Material {mat} shader {shader} property \"{prop_name}\" dim is not Tex2D: {dim}.");
				return default_; // Only Tex2D supported for now
			}
			var bound_tex = mat.GetTexture(prop_name) as Texture2D;
			return bound_tex == null ? default_ : bound_tex;
		}

		protected Color GetColor(Material mat, string prop_name, Color default_) {
			if (GetCommon(mat, prop_name, out var shader, out var prop_index, out var prop_type))
				return default_;
			if (prop_type != ShaderPropertyType.Color)
				return default_;

			return mat.GetColor(prop_name);
		}

		protected Vector4 GetTextureST(Material mat, string prop_name, Vector4 default_) {
			if (GetCommon(mat, prop_name, out var shader, out var prop_index, out var prop_type))
				return default_;
			if (prop_type != ShaderPropertyType.Texture) {
				LogWarning($"Material {mat} shader {shader} property \"{prop_name}\" type is not Texture: {prop_type}.");
				return default_;
			}
			var dim = shader.GetPropertyTextureDimension(prop_index);
			if (dim != TextureDimension.Tex2D) {
				LogWarning($"Material {mat} shader {shader} property \"{prop_name}\" dim is not Tex2D: {dim}.");
				return default_; // Only Tex2D supported for now
			}
			var scale = mat.GetTextureScale(prop_name);
			var offset = mat.GetTextureOffset(prop_name);
			return new Vector4(scale.x, scale.y, offset.x, offset.y);
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

		/* abstract API */

		protected abstract IEnumerable<DataChannelDescriptor> YieldDescriptors();

		public virtual List<DataChannelDescriptor> InitDescriptors() {
			descriptors.Clear();
			descriptors.AddRange(YieldDescriptors());
			return descriptors;
		}

		protected virtual DataChannelDescriptor GetDescriptorByName(string name)
			=> descriptors.FirstOrDefault(d => string.Equals(d.name, name));


		// MaterialCombiner передаёт сюда материал, который ему нужно адаптировать.
		// Если дескрипторы из этого адаптера, то хорошо, на пол беды меньше.
		// Но если из другого, то адаптеру придется подумать как адаптировать этот канал.
		// Или проигнорировать его, тогда там будут данные по-умолчанию.
		// Возвращает true и data, если адаптер понимает данный материал и смог его адаптировать.
		public abstract bool TryAdaptMaterial(Material mat, List<DataChannelDescriptor> descriptors, out DataAdapted data);

		public abstract void DataToMaterial(List<DataChannel> data, Material atlassed);
	}
}
#endif