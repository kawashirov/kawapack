#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class StandardMaterialAdapter : AbstractMaterialAdapter {
		public const string DATACH_ALBEDO = "Albedo";
		public const string DATACH_METALSMOOTH = "MetallicSmoothness";
		public const string DATACH_NORMAL = "Normal";
		public const string DATACH_EMISSION = "Emission";

		public enum AlphaMode { RGB, RGBA }
		public enum MetallicSmoothnessMode { MetallicAndSmoothness, MetallicOnly, SmoothnessOnly }

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

		[Tooltip("If this adapter used for final altlas, should Albedo (Color, MainTex) support alpha?")]
		public AlphaMode AlbedoAlphaMode = AlphaMode.RGBA;

		public MetallicSmoothnessMode MetalSmoothMode = MetallicSmoothnessMode.MetallicAndSmoothness;

		/**/

		protected DataChannelDescriptor descAlbedo = null;
		protected DataChannelDescriptor descMetalSmooth = null;
		protected DataChannelDescriptor descNormal = null;
		protected DataChannelDescriptor descEmission = null;

		public static string MetalSmoothModeToChannels(MetallicSmoothnessMode mode) {
			if (mode == MetallicSmoothnessMode.MetallicOnly) {
				return "RGB0";
			} else if (mode == MetallicSmoothnessMode.SmoothnessOnly) {
				return "000A";
			} else {
				return "RGBA";
			}
		}

		protected override IEnumerable<DataChannelDescriptor> YieldDescriptors() {
			yield return descAlbedo = new DataChannelDescriptor(this, DATACH_ALBEDO) {
				bgTexture = Texture2D.blackTexture,
				textureChannels = AlbedoAlphaMode == AlphaMode.RGB ? "RGB1" : "RGBA",
				bgColor = Color.black,
				alphaIsTransparency = true, isNormal = false, sRGB = true, HDR = false
			};
			yield return descMetalSmooth = new DataChannelDescriptor(this, DATACH_METALSMOOTH) {
				bgTexture = Texture2D.blackTexture,
				textureChannels = MetalSmoothModeToChannels(MetalSmoothMode),
				bgColor = Color.black, // 0 metall, 0 smooth
				alphaIsTransparency = false, isNormal = false, sRGB = false, HDR = false
			};
			yield return descNormal = new DataChannelDescriptor(this, DATACH_NORMAL) {
				bgTexture = Texture2D.normalTexture,
				// Карта нормалей в Unity использует RGBA, 
				// т.к. G и A каналы имеют лучшее качество в блочной компрессией
				textureChannels = "RGBA",
				bgColor = Color.white,
				alphaIsTransparency = false, isNormal = true, sRGB = false, HDR = false
			};
			yield return descEmission = new DataChannelDescriptor(this, DATACH_EMISSION) {
				bgTexture = Texture2D.blackTexture,
				textureChannels = "RGB1",
				bgColor = Color.black,
				alphaIsTransparency = false, isNormal = false, sRGB = false, HDR = true
			};
		}

		protected virtual bool CanAdaptMaterial(Material mat) {
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

		protected Vector4 MaterialToST(Material mat)
			=> GetTextureST(mat, "_MainTex", new Vector4(1, 1, 0, 0));

		protected virtual DataChannel GetAlbedoDC(Material mat) {
			var main_tex = GetTexture2D(mat, "_MainTex", Texture2D.whiteTexture);
			var main_color = GetColor(mat, "_Color", Color.white);
			return new DataChannel(mat, descAlbedo)
				.SetTextureRGBA(main_tex).SetColor(main_color);
		}

		protected virtual DataChannel GetMetallicDC(Material mat) {
			var metallic_tex_raw = GetTexture2D(mat, "_MetallicGlossMap", null);

			var color = Color.white;

			// У "Metallic" множитель не применяется к текстуре. Либо то, либо другое.
			var metallic_tex = metallic_tex_raw;
			if (metallic_tex_raw == null) {
				metallic_tex = Texture2D.whiteTexture;
				var metallic_scale = GetScalar(mat, "_Metallic", 0);
				color.r = metallic_scale;
				color.g = metallic_scale;
				color.b = metallic_scale;
			}

			// "Smoothness" множитель это разные проперти, в зависимости от наличия или отсутсвия _MetallicGlossMap
			// См DoSpecularMetallicArea() в StandardShaderGUI.cs reference
			var smoothness_scale = GetScalar(mat, metallic_tex_raw != null ? "_GlossMapScale" : "_Glossiness", 0);
			color.a = smoothness_scale;

			var dc = new DataChannel(mat, descMetalSmooth)
				.SetTextureSingleToRGB(metallic_tex, 0);

			if (GetScalar(mat, "_SmoothnessTextureChannel", 0) == 1) {
				// Smoothness хранится в _MainTex alpha
				var main_tex = GetTexture2D(mat, "_MainTex", Texture2D.whiteTexture);
				dc.SetAlpha(main_tex, 3);
			} else {
				// Smoothness хранится в _MetallicGlossMap alpha
				dc.SetAlpha(metallic_tex, 3);
			}

			return dc.SetColor(color);
		}

		protected virtual DataChannel GetNormalMapDC(Material mat) {
			var bumpmap_tex = GetTexture2D(mat, "_BumpMap", Texture2D.normalTexture);
			var bumpmap_scale = GetScalar(mat, "_BumpScale", 1);
			return new DataChannel(mat, descNormal)
				.SetTextureRGBA(bumpmap_tex).SetColor(Color.white * bumpmap_scale);
		}

		protected virtual DataChannel GetEmissionDC(Material mat) {
			// Для "Emission" имеет значение globalIlluminationFlags
			var emission_tex = Texture2D.blackTexture;
			var emission_color = Color.black;
			if ((mat.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive) != 0) {
				emission_tex = GetTexture2D(mat, "_EmissionMap", Texture2D.whiteTexture);
				emission_color = GetColor(mat, "_EmissionColor", Color.black);
				emission_color.a = 1;
			}
			return new DataChannel(mat, descEmission)
				.SetTextureRGB(emission_tex).SetWhiteAlpha().SetColor(emission_color);
		}

		protected virtual DataChannel GetDataChannelByName(Material mat, DataChannelDescriptor desc) {
			// desc должен быть из this.descriptors !!!
			var desc_name = desc.name;
			if (DATACH_ALBEDO.Equals(desc_name)) {
				return GetAlbedoDC(mat);
			} else if (DATACH_METALSMOOTH.Equals(desc_name)) {
				return GetMetallicDC(mat);
			} else if (DATACH_NORMAL.Equals(desc_name)) {
				return GetNormalMapDC(mat);
			} else if (DATACH_EMISSION.Equals(desc_name)) {
				return GetEmissionDC(mat);
			}
			return null;
		}

		protected virtual IEnumerable<DataChannel> YieldDataChannels(Material mat, List<DataChannelDescriptor> descriptors) {
			if (this.descriptors == descriptors) {
				// Если ref на список совпадает, значит это наш список.
				if (descAlbedo != null)
					yield return GetAlbedoDC(mat);
				if (descMetalSmooth != null)
					yield return GetMetallicDC(mat);
				if (descNormal != null)
					yield return GetNormalMapDC(mat);
				if (descEmission != null)
					yield return GetEmissionDC(mat);
			} else {
				// Если ссылка на список НЕ совпадает, значит это НЕ наш список и мы адаптируем под другой.
				// TODO Здесь типа что-то можно сделать для совместимости
				// пока тоже самое, подразумеваем совместимовть.
				foreach (var desc_other in descriptors) {
					var desc_name = desc_other.name;
					if (descAlbedo != null && descAlbedo.name.Equals(desc_name))
						yield return GetAlbedoDC(mat);
					else if (descMetalSmooth != null && descMetalSmooth.name.Equals(desc_name))
						yield return GetMetallicDC(mat);
					else if (descNormal != null && descNormal.name.Equals(desc_name))
						yield return GetNormalMapDC(mat);
					else if (descEmission != null && descEmission.name.Equals(desc_name))
						yield return GetEmissionDC(mat);
					// else несовместимый канал?
				}
			}
		}

		public override bool TryAdaptMaterial(Material mat, List<DataChannelDescriptor> descriptors, out DataAdapted data) {
			if (CanAdaptMaterial(mat)) {
				data = new DataAdapted(this,
					YieldDataChannels(mat, descriptors).ToList(),
					MaterialToST(mat),
					0
				);
				return true;
			} else {
				data = null;
				return false;
			}
		}

		public override void DataToMaterial(List<DataChannel> data, Material atlassed) {

		}
	}
}
#endif