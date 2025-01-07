#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class StandardMaterialAdapter : AbstractMaterialAdapter {
		public const string SHADER_NAME_METALLIC = "Standard";
		public const string SHADER_NAME_SPECULAR = "Standard (Specular)";
		public const string SHADER_NAME_DIELECTRIC = "Standard (Dielectric)";

		public const string DATACH_ALBEDO = "Albedo";
		public const string DATACH_SPECMOOTH = "SpecularSmoothness";
		public const string DATACH_METALSMOOTH = "MetallicSmoothness";
		public const string DATACH_NORMAL = "Normal";
		public const string DATACH_EMISSION = "Emission";

		public enum WorkflowMode { Specular, Metallic } // Dielectric
		public enum GlossMode { GlossAndSmoothness, GlossOnly, SmoothnessOnly }
		[Flags]
		public enum BlendFlags {
			None = 0,
			Opaque = 1 << 0, Cutout = 1 << 1, Fade = 1 << 2, Transparent = 1 << 3,
			UsingAlpha = Cutout | Fade | Transparent,
			All = ~0
		}

		/* Serializables */

		[Tooltip("Standard (Specular) vs Standard")] //  vs Standard (Dielectric)
		public WorkflowMode Workflow = WorkflowMode.Metallic;
		public GlossMode Gloss = GlossMode.GlossAndSmoothness;
		public BlendFlags Blend = BlendFlags.All;
		public bool InstancingMatters = false;
		public bool GIFlagsMatters = false;

		[Tooltip("Optional: Replace the shader of altas materials to this one. Must be compatible with this Adapter.")]
		public Shader OverrideShader;

		[Header("Data Textures Options")]
		public bool AlbedoEnable = true;
		public float AlbedoScale = 1;

		public bool GlossEnable = true;
		public float GlossScale = 1;

		public bool NormalEnable = true;
		public float NormalScale = 1;

		public bool EmissionEnable = true;
		public float EmissionScale = 1;

		[Header("Compatibility Options")]
		[Tooltip("If checked, all Shaders will be assumed supported with respect to ExcludeKeywords and IncludeKeywords")]
		public bool AssumeCompatible = false;

		[Tooltip("Shaders that contains any of those words in their names will be assumed as NOT standard-compatible")]
		public List<string> ExcludeNameWords = new List<string> { "Legacy" };

		[Tooltip("Same as ExcludeKeywords but Shader Objects")]
		public List<Shader> ExcludeShaders = new List<Shader>();

		[Tooltip("Shaders that contains any of those words in their names will be assumed as standard-compatible")]
		public List<string> IncludeNameWords = new List<string> { "VRChat/Mobile/Standard Lite" };

		[Tooltip("Same as IncludeKeywords but Shader Objects")]
		public List<Shader> IncludeShaders = new List<Shader>();

		/**/

		protected DataTexDesc descAlbedo = null;
		protected DataTexDesc descSpecSmooth = null;
		protected DataTexDesc descMetalSmooth = null;
		protected DataTexDesc descNormal = null;
		protected DataTexDesc descEmission = null;

		public static string GlossModeToChannels(GlossMode mode) {
			if (mode == GlossMode.GlossOnly) {
				return "RGB0";
			} else if (mode == GlossMode.SmoothnessOnly) {
				return "000A";
			} else {
				return "RGBA";
			}
		}

		public static BlendFlags GetBlendFlags(Material mat) {
			return mat.HasFloat("_Mode") ? mat.GetFloat("_Mode") switch {
				0 => BlendFlags.Opaque,
				1 => BlendFlags.Cutout,
				2 => BlendFlags.Fade,
				3 => BlendFlags.Transparent,
				_ => BlendFlags.None,
			} : BlendFlags.None;
		}

		protected override Shader GetDefaultAtlasShader() {
			return Workflow switch {
				WorkflowMode.Specular => Shader.Find(SHADER_NAME_SPECULAR),
				WorkflowMode.Metallic => Shader.Find(SHADER_NAME_METALLIC),
				// WorkflowMode.Dielectric => Shader.Find(SHADER_NAME_DIELECTRIC),
				_ => null
			};
		}

		public override Shader EnsureAtlasShader() {
			if (OverrideShader != null) {
				return OverrideShader;
			}

			var atlas_shader = GetDefaultAtlasShader();
			if (atlas_shader != null) {
				return atlas_shader;
			}

			throw new Exception();
		}

		protected override IEnumerable<DataTexDesc> YieldDescriptors() {
			if (AlbedoEnable) {
				yield return descAlbedo = new DataTexDesc(this, DATACH_ALBEDO) {
					bgTexture = Texture2D.blackTexture,
					textureChannels = (Blend & BlendFlags.UsingAlpha) == 0 ? "RGB1" : "RGBA",
					bgColor = Color.black,
					scaleFactor = AlbedoScale,
					sRGB = true, alphaIsTransparency = true,
				};
			}

			if (GlossEnable && Workflow == WorkflowMode.Specular) {
				yield return descSpecSmooth = new DataTexDesc(this, DATACH_SPECMOOTH) {
					bgTexture = Texture2D.blackTexture,
					textureChannels = GlossModeToChannels(Gloss),
					bgColor = Color.black, // 0 metall, 0 smooth
					scaleFactor = GlossScale,
					sRGB = false,
				};

			} else if (GlossEnable && Workflow == WorkflowMode.Metallic) {
				yield return descMetalSmooth = new DataTexDesc(this, DATACH_METALSMOOTH) {
					bgTexture = Texture2D.blackTexture,
					textureChannels = GlossModeToChannels(Gloss),
					bgColor = Color.black, // 0 metall, 0 smooth
					scaleFactor = GlossScale,
					sRGB = false,
				};
			}

			if (NormalEnable) {
				yield return descNormal = new DataTexDesc(this, DATACH_NORMAL) {
					bgTexture = Texture2D.normalTexture,
					// Карта нормалей в Unity использует RGBA, 
					// т.к. G и A каналы имеют лучшее качество с блочной компрессией
					textureChannels = "RGBA",
					bgColor = Color.white,
					scaleFactor = NormalScale,
					sRGB = false, isNormal = true,
				};
			}

			if (EmissionEnable) {
				yield return descEmission = new DataTexDesc(this, DATACH_EMISSION) {
					bgTexture = Texture2D.blackTexture,
					textureChannels = "RGB1",
					bgColor = Color.black,
					scaleFactor = EmissionScale,
					sRGB = false, EXR = true,
				};
			}
		}

		protected virtual bool CanAdaptMaterial(Material mat) {
			var shader = mat.shader;
			if (shader == null)
				return false;
			var shader_name = shader.name;

			var blend_mode = GetBlendFlags(mat);
			if ((blend_mode & Blend) == 0)
				return false;

			if (IncludeShaders.Contains(shader))
				return true;
			if (IncludeNameWords.Any(kw => Contains(shader_name, kw)))
				return true;

			if (ExcludeShaders.Contains(shader))
				return false;
			if (ExcludeNameWords.Any(kw => Contains(shader_name, kw)))
				return false;

			if (Workflow == WorkflowMode.Specular) {
				if (Equals(shader_name, "Standard (Specular setup)"))
					return true;
			} else if (Workflow == WorkflowMode.Metallic) {
				if (Equals(shader_name, "Standard"))
					return true;
			}
			// else if (Workflow == WorkflowMode.Dielectric) {
			// 	if (Equals(shader_name, "Standard (Dielectric setup)"))
			// 		return true;
			// }

			return AssumeCompatible;
		}

		protected Vector4 MaterialToST(Material mat)
			=> GetTextureST(mat, "_MainTex", new Vector4(1, 1, 0, 0));

		protected virtual DataTex GetAlbedoDC(Material mat) {
			var main_tex = GetTexture2D(mat, "_MainTex", Texture2D.whiteTexture);
			var main_color = GetColor(mat, "_Color", Color.white);
			return new DataTex(mat, descAlbedo)
				.SetTexRGBA(main_tex).SetColor(main_color);
		}

		protected virtual void ConfigureSmooth(Material mat, DataTex dc, Texture2D gloss_tex_raw, Texture2D gloss_tex) {
			// "Smoothness" множитель это разные проперти, в зависимости от наличия или отсутсвия _MetallicGlossMap
			// См DoSpecularMetallicArea() в StandardShaderGUI.cs reference
			var smoothness_scale = GetScalar(mat, gloss_tex_raw != null ? "_GlossMapScale" : "_Glossiness", 0);
			dc.SetColorAlpha(smoothness_scale);

			if (GetScalar(mat, "_SmoothnessTextureChannel", 0) == 1) {
				// Smoothness хранится в _MainTex alpha
				var main_tex = GetTexture2D(mat, "_MainTex", Texture2D.whiteTexture);
				dc.SetTexAlpha(main_tex, 3);
			} else {
				// Smoothness хранится в _MetallicGlossMap alpha
				dc.SetTexAlpha(gloss_tex, 3);
			}

		}

		protected virtual DataTex GetSpecularDC(Material mat) {
			var dc = new DataTex(mat, descSpecSmooth);

			// У "Specular" множитель не применяется к текстуре. Либо то, либо другое.
			var specular_tex_raw = GetTexture2D(mat, "_SpecGlossMap", null);
			var specular_tex = specular_tex_raw;
			if (specular_tex_raw == null) {
				specular_tex = Texture2D.whiteTexture;
				var spec_scale = GetColor(mat, "_SpecColor", Color.black);
				dc.SetColorRGB(spec_scale);
			}
			dc.SetTexRGB(specular_tex);

			ConfigureSmooth(mat, dc, specular_tex_raw, specular_tex);

			return dc;
		}

		protected virtual DataTex GetMetallicDC(Material mat) {
			var dc = new DataTex(mat, descMetalSmooth);

			// У "Metallic" множитель не применяется к текстуре. Либо то, либо другое.
			var metallic_tex_raw = GetTexture2D(mat, "_MetallicGlossMap", null);
			var metallic_tex = metallic_tex_raw;
			if (metallic_tex_raw == null) {
				metallic_tex = Texture2D.whiteTexture;
				var metallic_scale = GetScalar(mat, "_Metallic", 0);
				dc.SetColorRGB(metallic_scale);
			}
			dc.SetTexSingleToRGB(metallic_tex, 0);

			ConfigureSmooth(mat, dc, metallic_tex_raw, metallic_tex);

			return dc;
		}

		protected virtual DataTex GetNormalMapDC(Material mat) {
			var bumpmap_tex = GetTexture2D(mat, "_BumpMap", Texture2D.normalTexture);
			var bumpmap_scale = GetScalar(mat, "_BumpScale", 1);
			return new DataTex(mat, descNormal)
				.SetTexRGBA(bumpmap_tex).SetColor(Color.white * bumpmap_scale);
		}

		protected virtual DataTex GetEmissionDC(Material mat) {
			// Для "Emission" имеет значение globalIlluminationFlags
			var emission_tex = Texture2D.blackTexture;
			var emission_color = Color.black;
			if ((mat.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive) != 0) {
				emission_tex = GetTexture2D(mat, "_EmissionMap", Texture2D.whiteTexture);
				emission_color = GetColor(mat, "_EmissionColor", Color.black);
				emission_color.a = 1;
			}
			return new DataTex(mat, descEmission)
				.SetTexRGB(emission_tex).SetWhiteAlpha().SetColor(emission_color);
		}

		protected virtual IEnumerable<DataTex> YieldDataTexs(Material mat, List<DataTexDesc> descriptors) {
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

		public override bool TryAdaptMaterial(Material mat, List<DataTexDesc> descriptors, out DataAdapted data) {
			if (CanAdaptMaterial(mat)) {
				data = new DataAdapted(this, YieldDataTexs(mat, descriptors).ToList(), MaterialToST(mat), 0);
				return true;
			} else {
				data = null;
				return false;
			}
		}

		protected virtual void ApplyAtlasAlbedo(Material mat) {
			SetTextureDesc(mat, "_MainTex", descAlbedo);
			mat.SetColor("_Color", Color.white);
		}

		protected virtual void ApplyAtlasWhateverGloss(Material mat) {
			if (descMetalSmooth != null && descMetalSmooth.atlasTexture != null) {
				SetTextureNoST(mat, "_MetallicGlossMap", descMetalSmooth.atlasTexture);
				SetTextureNoST(mat, "_SpecGlossMap", null);
				mat.DisableKeyword("_SPECGLOSSMAP");
				mat.EnableKeyword("_METALLICGLOSSMAP");
			} else if (descSpecSmooth != null && descSpecSmooth.atlasTexture != null) {
				SetTextureNoST(mat, "_SpecGlossMap", descSpecSmooth.atlasTexture);
				SetTextureNoST(mat, "_MetallicGlossMap", null);
				mat.DisableKeyword("_METALLICGLOSSMAP");
				mat.EnableKeyword("_SPECGLOSSMAP");
			} else {
				SetTextureNoST(mat, "_SpecGlossMap", null);
				SetTextureNoST(mat, "_MetallicGlossMap", null);
				mat.DisableKeyword("_SPECGLOSSMAP");
				mat.DisableKeyword("_METALLICGLOSSMAP");
			}
			mat.SetFloat("_GlossMapScale", 1);
			mat.SetFloat("_Metallic", 1);
			SetTextureNoST(mat, "_SpecGlossMap", null);
			// Smooth всегда вместе с metalic/specular 
			mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
		}


		protected virtual void ApplyAtlasNormal(Material mat) {
			if (descNormal != null && descNormal.atlasTexture != null) {
				SetTextureNoST(mat, "_BumpMap", descNormal.atlasTexture);
				mat.EnableKeyword("_NORMALMAP");
			} else {
				SetTextureNoST(mat, "_EmissionMap", null);
				mat.DisableKeyword("_NORMALMAP");
			}
			mat.SetFloat("_BumpScale", 1);
		}

		protected virtual void ApplyAtlasEmission(Material mat) {
			if (descEmission != null && descEmission.atlasTexture != null) {
				SetTextureNoST(mat, "_EmissionMap", descEmission.atlasTexture);
				mat.SetColor("_EmissionColor", Color.white);
				mat.globalIlluminationFlags = DefaultMaterialGlobalIlluminationFlags();
				mat.EnableKeyword("_EMISSION");
			} else {
				SetTextureNoST(mat, "_EmissionMap", null);
				mat.SetColor("_EmissionColor", Color.black);
				mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
				mat.DisableKeyword("_EMISSION");
			}
		}

		public override bool IsCompatible(Material left, Material right) {
			if (DiffCommons(left, right, GIFlagsMatters, InstancingMatters))
				return false;

			// See Standard.shader, StandardSpecular.shader
			if (DiffFloat(left, right, "_Mode"))
				return false;
			var mode = left.GetFloat("_Mode"); // same as right
			if (mode == 1) { // 1: Cutout
				if (DiffFloat(left, right, "_Cutoff"))
					return false;
			}

			// _Color, _MainTex
			// _Glossiness, _GlossMapScale, _SmoothnessTextureChannel
			// _SpecColor, _SpecGlossMap
			// _Metallic, _MetallicGlossMap

			if (DiffFloat(left, right, "_SpecularHighlights"))
				return false;
			if (DiffFloat(left, right, "_GlossyReflections"))
				return false;

			// _BumpScale, _BumpMap
			// _Parallax, _ParallaxMap TODO
			// _OcclusionStrength, _OcclusionMap TODO
			// _EmissionColor, _EmissionMap TODO

			// _DetailMask, _DetailAlbedoMap, _DetailNormalMapScale, _DetailNormalMap, _UVSec - not supported, ignored
			// _SrcBlend, _DstBlend, _ZWrite - managed by StandardShaderGUI from _Mode

			// Assume keywords are OK, managed by StandardShaderGUI

			return true;
		}

		public override void ConfigureAtlasMaterial(Material atlas) {
			atlas.shader = EnsureAtlasShader();
			RemoveAllTextures(atlas);
			if (!InstancingMatters)
				atlas.enableInstancing = true;
			atlas.globalIlluminationFlags = MaterialEditor.FixupEmissiveFlag(Color.white, atlas.globalIlluminationFlags);

			ApplyAtlasAlbedo(atlas);

			// _Cutoff matters

			ApplyAtlasWhateverGloss(atlas);

			// _SpecularHighlights, _GlossyReflections matters

			ApplyAtlasNormal(atlas);

			// TODO _Parallax, _ParallaxMap

			// TODO _OcclusionStrength, _OcclusionMap

			ApplyAtlasEmission(atlas);

			// Reset unsupported
			SetTextureNoST(atlas, "_DetailMask", null);
			SetTextureNoST(atlas, "_DetailAlbedoMap", null);
			atlas.SetFloat("_DetailNormalMapScale", 1);
			SetTextureNoST(atlas, "_DetailNormalMap", null);
			atlas.SetFloat("_UVSec", 0);
		}
	}
}
#endif