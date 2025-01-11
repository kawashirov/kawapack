#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kawashirov.MaterialCombining {
	public abstract class AbstractAtlasRenderer {
		public static Vector4 VECTOR_0011 = new Vector4(0, 0, 1, 1);
		public static Vector4 VECTOR_0123 = new Vector4(0, 1, 2, 3);
		public static Vector3 VECTOR_012 = new Vector3(0, 1, 2);

		public readonly AbstaractMaterialCombiner combider;
		public readonly string name;

		public Texture2D atlasTexture = null;

		public AbstractAtlasRenderer(AbstaractMaterialCombiner combider, string name) {
			this.combider = combider;
			this.name = name;
		}

		/* Orignal materials data get helpers */

		protected bool GetCommon(Material mat, string prop_name,
			out Shader shader, out int prop_index, out ShaderPropertyType prop_type) {
			// Возвращает true если вызывающему нужно отказаться от этой проперти
			shader = mat.shader;
			prop_index = -1;
			prop_type = ShaderPropertyType.Color;

			if (shader == null) {
				combider.LogWarning($"Material {mat} has no valid shader attached.");
				return true;
			}

			prop_index = shader.FindPropertyIndex(prop_name);
			if (prop_index < 0) {
				combider.LogWarning($"Material {mat} shader {shader} has no property \"{prop_name}\"");
				return true;
			}

			prop_type = shader.GetPropertyType(prop_index);
			return false;
		}

		protected Texture2D GetTexture2D(Material mat, string prop_name, Texture2D default_) {
			if (GetCommon(mat, prop_name, out var shader, out var prop_index, out var prop_type))
				return default_;
			if (prop_type != ShaderPropertyType.Texture) {
				combider.LogWarning($"Material {mat} shader {shader} property \"{prop_name}\" type is not Texture: {prop_type}.");
				return default_;
			}
			var dim = shader.GetPropertyTextureDimension(prop_index);
			if (dim != TextureDimension.Tex2D) {
				combider.LogWarning($"Material {mat} shader {shader} property \"{prop_name}\" dim is not Tex2D: {dim}.");
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
				combider.LogWarning($"Material {mat} shader {shader} property \"{prop_name}\" type is not Texture: {prop_type}.");
				return default_;
			}
			var dim = shader.GetPropertyTextureDimension(prop_index);
			if (dim != TextureDimension.Tex2D) {
				combider.LogWarning($"Material {mat} shader {shader} property \"{prop_name}\" dim is not Tex2D: {dim}.");
				return default_; // Only Tex2D supported for now
			}
			var scale = mat.GetTextureScale(prop_name);
			var offset = mat.GetTextureOffset(prop_name);
			return new Vector4(scale.x, scale.y, offset.x, offset.y);
		}

		protected float GetScalar(Material mat, string prop_name, float default_) {
			if (GetCommon(mat, prop_name, out var shader, out var prop_index, out var prop_type))
				return default_;
			if (prop_type == ShaderPropertyType.Float || prop_type == ShaderPropertyType.Range)
				return mat.GetFloat(prop_name);
			if (prop_type == ShaderPropertyType.Int)
				return mat.GetInt(prop_name);
			return default_;
		}

		/* Blit config helpers */

		/*
			в финальную текстуру атласа будет записан:
			- в канал R (№0): (канал № _Channels[0] из _TexR) * _Color.r * _Scale.x
			- в канал G (№1): (канал № _Channels[1] из _TexG) * _Color.g * _Scale.y
			- в канал B (№2): (канал № _Channels[2] из _TexB) * _Color.b * _Scale.z
			- в канал A (№3): (канал № _Channels[3] из _TexA) * _Color.a * _Scale.w
			Такой сложный подход позволяет легко мультиплексить каналы.
			Шейдер может переопределять это поведение, например для карты нормалей.
		*/

		protected void BlitTexRGBA(Material blit, Texture2D tex, Vector4 channels) {
			blit.SetTexture("_TexR", tex);
			blit.SetTexture("_TexG", tex);
			blit.SetTexture("_TexB", tex);
			blit.SetTexture("_TexA", tex);
			blit.SetVector("_Channels", channels);
		}

		protected void BlitTexRGBA(Material blit, Texture2D tex) => BlitTexRGBA(blit, tex, VECTOR_0123);

		protected void BlitTexRGB(Material blit, Texture2D tex, Vector3 channels) {
			blit.SetTexture("_TexR", tex);
			blit.SetTexture("_TexG", tex);
			blit.SetTexture("_TexB", tex);
			blit.SetTexture("_TexA", tex);
			var channels_curr = blit.GetVector("_Channels");
			(channels_curr.x, channels_curr.y, channels_curr.z) = (channels.x, channels.y, channels.z);
			blit.SetVector("_Channels", channels_curr);
		}

		protected void BlitTexRGB(Material blit, Texture2D tex) => BlitTexRGB(blit, tex, VECTOR_0123);

		protected void BlitTexA(Material blit, Texture2D tex, int channel) {
			blit.SetTexture("_TexA", tex);
			var channels = blit.GetVector("_Channels");
			channels.w = channel;
			blit.SetVector("_Channels", channels);
		}

		protected void BlitTexA(Material blit, Texture2D tex) => BlitTexA(blit, tex, 3);

		protected void BlitColorRGB(Material blit, Color color) {
			var curr_color = blit.GetColor("_Color");
			(curr_color.r, curr_color.g, curr_color.b) = (color.r, color.g, color.b);
			blit.SetColor("_Color", curr_color);
		}

		protected void BlitColorA(Material blit, float color) {
			var curr_color = blit.GetColor("_Color");
			curr_color.a = color;
			blit.SetColor("_Color", curr_color);
		}

		protected void BlitScaleRGB(Material blit, Vector3 scale) {
			var curr_scale = blit.GetVector("_Scale");
			(curr_scale.x, curr_scale.y, curr_scale.z) = (scale.x, scale.y, scale.z);
			blit.SetVector("_Scale", curr_scale);
		}

		protected void BlitScaleA(Material blit, float scale) {
			var curr_scale = blit.GetVector("_Scale");
			curr_scale.w = scale;
			blit.SetVector("_Scale", curr_scale);
		}

		/* Abstract workflow */

		public virtual bool BlitDebugCapture() => false;

		public abstract Vector2Int GetTexSize(MaterialGroup group);

		public virtual void BlitPrepareReset(Material mat_blit) {
			BlitTexRGBA(mat_blit, Texture2D.whiteTexture);
			mat_blit.SetColor("_Color", Color.white);
			mat_blit.SetVector("_Scale", Vector4.one);

			mat_blit.SetInteger("_ColorSpace", 0);
			mat_blit.SetInteger("_BumpMode", 0);
			mat_blit.SetInteger("_ParallaxMode", 0);
			mat_blit.SetInteger("_OcclusionMode", 0);
			mat_blit.SetFloat("_ParallaxRef", 0.02f);
		}

		public abstract void PrepareRenderBackground(Material mat_blit);

		public abstract void PrepareRenderMaterial(MaterialGroup group, Material mat_blit);

		public virtual string AtlasSaveFormat() => "png"; // or "exr"

		public virtual Texture2D AtlasRTToTexture2D(RenderTexture atlas) {
			var tex_temp = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBAHalf, true, linear: false);
			var prev_active = RenderTexture.active;
			try {
				RenderTexture.active = atlas;
				tex_temp.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0, true);
				tex_temp.Apply();
				EditorUtility.SetDirty(tex_temp);
			} finally {
				RenderTexture.active = prev_active;
			}
			return tex_temp;
		}

		public virtual void AtlasConfigureImporter(TextureImporter importer) {
			importer.textureType = TextureImporterType.Default;
			importer.sRGBTexture = true;
			importer.alphaIsTransparency = false;

			importer.mipmapEnabled = true;
			importer.streamingMipmaps = true;
			importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
			importer.mipMapsPreserveCoverage = false;
			importer.alphaTestReferenceValue = 0.5f;

			importer.filterMode = FilterMode.Trilinear;
			importer.wrapMode = TextureWrapMode.Clamp;

			// var tex_size = Mathf.NextPowerOfTwo(Mathf.Max(atlasSize.x, atlasSize.y));
			// importer.maxTextureSize = Mathf.Clamp(tex_size, 32, 16 * 1024);
			importer.maxTextureSize = 16 * 1024;

			importer.textureCompression = TextureImporterCompression.CompressedHQ;
			importer.compressionQuality = 100;
			importer.crunchedCompression = false;
		}

		public abstract void AtlasReset(Material mat_atlas);

		public abstract void AtlasApply(Material mat_atlas);

	}
}
#endif