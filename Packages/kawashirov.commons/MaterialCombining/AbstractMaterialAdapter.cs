#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kawashirov.MaterialCombining {
	public abstract class AbstractMaterialAdapter : KawaEditorBehaviour {
		[NonSerialized] public MaterialCombiner combiner;
		protected readonly List<DataTexDesc> descriptors = new List<DataTexDesc>();

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

		protected static void SetTextureDesc(Material mat, string prop_name, DataTexDesc desc) {
			mat.SetTexture(prop_name, (desc != null && desc.atlasTexture != null) ? desc.atlasTexture : null);
			mat.SetTextureScale(prop_name, Vector2.one);
			mat.SetTextureOffset(prop_name, Vector2.zero);
		}

		protected static void SetTextureNoST(Material mat, string prop_name, Texture2D tex) {
			mat.SetTexture(prop_name, tex);
			mat.SetTextureScale(prop_name, Vector2.one);
			mat.SetTextureOffset(prop_name, Vector2.zero);
		}

		/* abstract API */

		protected abstract Shader GetDefaultAtlasShader();

		public abstract Shader EnsureAtlasShader();

		protected abstract IEnumerable<DataTexDesc> YieldDescriptors();

		// Первичная настройка адаптера. Вызывается только для "главного" адаптера, 
		// т.е. для того, которому предстоит собрать атласный материал.
		// Предикат определяет, нужно ли атлассировать эту текстуру, т.е. некоторые можно игнорировать.
		public virtual List<DataTexDesc> InitDescriptors() {
			descriptors.Clear();
			descriptors.AddRange(YieldDescriptors());
			return descriptors;
		}

		// MaterialCombiner передаёт сюда материал, который ему нужно адаптировать.
		// Если дескрипторы из этого адаптера, то хорошо, на пол беды меньше.
		// Но если из другого, то адаптеру придется подумать как адаптировать этот канал.
		// Или проигнорировать его, тогда там будут данные по-умолчанию.
		// Возвращает true и data, если адаптер понимает данный материал и смог его адаптировать.
		public abstract bool TryAdaptMaterial(Material mat, List<DataTexDesc> descriptors, out DataAdapted data);

		public virtual bool DiffFloat(Material left, Material right, string name) {
			if (!left.HasFloat(name) && !right.HasFloat(name))
				return false;
			if (!(left.HasFloat(name) && right.HasFloat(name)))
				return true;
			var left_v = left.GetFloat(name);
			var right_v = right.GetFloat(name);
			return !Mathf.Approximately(left_v, right_v);
		}

		// Должен сравнить два материала на совместимость, согласно настройкам этого адаптера.
		public abstract bool IsCompatible(Material left, Material right);

		// Должен создать новый материал (и настроить его),
		// на основе данного оригинала (не изменяя его)
		public abstract Material MakeNewAtlasMaterial(Material original);

	}
}
#endif