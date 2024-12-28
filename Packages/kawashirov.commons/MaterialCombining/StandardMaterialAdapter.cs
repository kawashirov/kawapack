#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class StandardMaterialAdapter : AbstractMaterialAdapter {
		protected static readonly ISet<string> SUPPORTED_FEATURES = new HashSet<string>() {
			"Albedo", "Metallic", "Smoothness", "NormalMap", "Emission"
		};

		[Tooltip("Standard vs Standard (Specular)")]
		public bool SpecularMode = false;

		[Tooltip("If checked, all Shaders will be assumed supported with respect to ExcludeKeywords and IncludeKeywords")]
		public bool AssumeCompatible = false;

		[Tooltip("Shaders that contains any of those words in their names will be assumed as NOT standard-compatible")]
		public string[] ExcludeNameWords = new string[] { "Legacy" };

		[Tooltip("Same as ExcludeKeywords but Shader Objects")]
		public Shader[] ExcludeShaders;

		[Tooltip("Shaders that contains any of those words in their names will be assumed as standard-compatible")]
		public string[] IncludeNameWords = new string[] { "VRChat/Mobile/Standard Lite" };

		[Tooltip("Same as IncludeKeywords but Shader Objects")]
		public Shader[] IncludeShaders;

		protected virtual DataChannel GetAlbedoDC(Material mat) {
			var main_tex = GetTexture2D(mat, "_MainTex", Texture2D.whiteTexture);
			var main_color = GetColor(mat, "_Color", Color.white);
			return new DataChannel() {
				parent = mat, name = "Albedo",
				texture = main_tex, textureChannel = "RGB0",
				scaleColor = main_color, scale = 1
			};
		}

		protected virtual DataChannel GetMetallicDC(Material mat) {
			// У "Metallic" множитель не применяется к текстуре. Либо то, либо другое.
			var metallic_tex_raw = GetTexture2D(mat, "_MetallicGlossMap", null);
			var metallic_tex = metallic_tex_raw == null ? Texture2D.whiteTexture : metallic_tex_raw;
			var metallic_scale = metallic_tex_raw == null ? GetScalar(mat, "_Metallic", 0) : 1;
			return new DataChannel() {
				parent = mat, name = "Metallic",
				texture = metallic_tex, textureChannel = "RGB0",
				scaleColor = Color.white, scale = metallic_scale
			};
		}

		protected virtual DataChannel GetSmoothnessDC(Material mat) {
			// "Smoothness" множитель это разные проперти, в зависимости от наличия или отсутсвия _MetallicGlossMap
			// См DoSpecularMetallicArea() в StandardShaderGUI.cs reference
			var metallic_tex = GetTexture2D(mat, "_MetallicGlossMap", Texture2D.whiteTexture);
			var smoothness_scale = GetScalar(mat, metallic_tex != null ? "_GlossMapScale" : "_Glossiness", 0);
			var smoothness_tex = GetScalar(mat, "_SmoothnessTextureChannel", 0) == 1
				? GetTexture2D(mat, "_MainTex", Texture2D.whiteTexture)
				: metallic_tex;
			return new DataChannel() {
				parent = mat, name = "Metallic",
				texture = smoothness_tex, textureChannel = "000A",
				scaleColor = Color.white, scale = smoothness_scale
			};
		}

		protected virtual DataChannel GetNormalMapDC(Material mat) {
			var bumpmap_tex = GetTexture2D(mat, "_BumpMap", Texture2D.normalTexture);
			var bumpmap_scale = GetScalar(mat, "_BumpScale", 1);
			return new DataChannel() {
				parent = mat, name = "NormalMap",
				texture = bumpmap_tex, textureChannel = "RGB0", scaleColor = Color.white, scale = bumpmap_scale
			};
		}

		protected virtual DataChannel GetEmissionDC(Material mat) {
			// Для "Emission" имеет значение globalIlluminationFlags
			var emission_tex = Texture2D.blackTexture;
			var emission_color = Color.black;
			var scale = 0f;
			if ((mat.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive) != 0) {
				emission_tex = GetTexture2D(mat, "_EmissionMap", Texture2D.whiteTexture);
				emission_color = GetColor(mat, "_EmissionColor", Color.black);
				scale = 1f;
			}
			return new DataChannel() {
				parent = mat, name = "Emission",
				texture = emission_tex, textureChannel = "RGB0", scaleColor = emission_color, scale = scale
			};
		}

		protected virtual IEnumerable<DataChannel> YieldDataChannels(Material mat) {
			yield return GetAlbedoDC(mat);
			yield return GetMetallicDC(mat);
			yield return GetSmoothnessDC(mat);
			yield return GetEmissionDC(mat);
		}

		public override bool CanAdaptMaterial(Material mat) {
			var shader = mat.shader;
			if (shader == null)
				return false;
			var shader_name = shader.name;

			if (IncludeShaders.Contains(shader))
				return true;
			if (IncludeNameWords.Any(kw => Contains(shader_name, kw)))
				return true;

			if (ExcludeShaders.Contains(shader))
				return false;
			if (ExcludeNameWords.Any(kw => Contains(shader_name, kw)))
				return false;

			if (SpecularMode) {
				if (Equals(shader_name, "Standard (Specular setup)"))
					return true;
			} else {
				if (Equals(shader_name, "Standard"))
					return true;
			}

			return AssumeCompatible;
		}

		protected virtual void DataToBlitAlbedo(DataChannel data, Material mat_blit) {
			var (tex, ch_str, color, scale)
				= (data.texture, data.textureChannel, data.scaleColor, data.scale);

			if (scale != 1) {
				color = new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
				scale = 1;
			}

			mat_blit.SetTexture("_MainTex", tex);
			mat_blit.SetColor("_Color", color);
			mat_blit.SetFloat("_Scale", scale);

			var channel_map = new Vector4(
				ChCharToIndex(ch_str[0]),
				ChCharToIndex(ch_str[1]),
				ChCharToIndex(ch_str[2]),
				ChCharToIndex(ch_str[3])
			);
			mat_blit.SetVector("_ChannelMap", channel_map);
		}

		public override void DataToBlit(DataChannel data, Material mat_blit) {
			if (data.name == "Albedo") {
				DataToBlitAlbedo(data, mat_blit);
			}
		}

		public override ICollection<string> SupportedFeatures() => SUPPORTED_FEATURES;
		public override List<DataChannel> MaterialToData(Material mat) => YieldDataChannels(mat).ToList();

		public override void DataToMaterial(List<DataChannel> data, Material atlassed) {

		}
	}
}
#endif