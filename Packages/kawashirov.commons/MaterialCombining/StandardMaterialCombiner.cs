#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

using static Kawashirov.MaterialCombining.MatCmbUtility;

namespace Kawashirov.MaterialCombining {
	public class StandardMaterialCombiner : AbstaractMaterialCombiner {
		public const string SHADER_NAME_METALLIC = "Standard";
		public const string SHADER_NAME_SPECULAR = "Standard (Specular)";
		public const string SHADER_NAME_DIELECTRIC = "Standard (Dielectric)";

		public const string DATACH_PARALLAX = "Parallax";
		public const string DATACH_OCCLUSION = "Occlusion";
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

		public WorkflowMode Workflow = WorkflowMode.Metallic;
		public GlossMode Gloss = GlossMode.GlossAndSmoothness;
		public BlendFlags Blend = BlendFlags.All;
		public bool InstancingMatters = false;
		public bool GIFlagsMatters = false;

		public Shader OverrideShader;

		public bool AlbedoEnable = true;
		public float AlbedoScale = 1;

		public bool GlossEnable = true;
		public float GlossScale = 1;

		public bool NormalEnable = true;
		public float NormalScale = 1;

		public bool ParallaxEnable = true;
		public float ParallaxScale = 1;
		public float ParallaxReference = ParallaxAtlasRenderer.PARALLAX_SCALE_MAX;

		public bool OcclusionEnable = true;
		public float OcclusionScale = 1;

		public bool EmissionEnable = true;
		public float EmissionScale = 1;

		public bool AssumeCompatible = false;

		public List<string> ExcludeNameWords = new List<string> { "Legacy" };
		public List<Shader> ExcludeShaders = new List<Shader>();
		public List<string> IncludeNameWords = new List<string> { "VRChat/Mobile/Standard Lite" };
		public List<Shader> IncludeShaders = new List<Shader>();

		/**/

		protected AlbedoAtlasRenderer descAlbedo = null;
		protected AbstractAtlasRenderer descSpecSmooth = null;
		protected AbstractAtlasRenderer descMetalSmooth = null;
		protected AbstractAtlasRenderer descNormal = null;
		protected AbstractAtlasRenderer descParallax = null;
		protected AbstractAtlasRenderer descOcclusion = null;
		protected AbstractAtlasRenderer descEmission = null;

		// // // // //

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

		protected override IEnumerable<AbstractAtlasRenderer> YieldAtlasRenderers() {
			if (AlbedoEnable) {
				yield return descAlbedo = new AlbedoAtlasRenderer(this);
			}

			if (GlossEnable && Workflow == WorkflowMode.Specular) {
				yield return descSpecSmooth = new SpecSmoothRenderer(this);

			} else if (GlossEnable && Workflow == WorkflowMode.Metallic) {
				yield return descMetalSmooth = new MetalSmoothRenderer(this);
			}

			if (NormalEnable) {
				yield return descNormal = new NormalAtlasRenderer(this);
			}

			if (ParallaxEnable) {
				yield return descParallax = new ParallaxAtlasRenderer(this, ParallaxReference);
			}

			if (OcclusionEnable) {
				yield return descOcclusion = new OcclusionAtlasRenderer(this);
			}

			if (EmissionEnable) {
				yield return descEmission = new EmissionAtlasRenderer(this);
			}
		}

		protected override bool CanAdaptMaterial(Material mat) {
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

		protected override Vector2Int CalcMatSize(MaterialGroup mat_group) {
			var size = Vector2Int.zero;
			foreach (var atlas_renderer in atlasRenderers) {
				var size_renderer = atlas_renderer.GetTexSize(mat_group);
				if (size_renderer.x * size_renderer.y > size.x * size.y)
					size = size_renderer;
			}
			return size;
		}

		protected override Vector4 CalcMatST(MaterialGroup mat_group) {
			var mat_orig = mat_group.matOriginal;
			var scale = mat_orig.GetTextureScale("_MainTex");
			var offset = mat_orig.GetTextureOffset("_MainTex");
			return new Vector4(scale.x, scale.y, offset.x, offset.y);
		}

		public void CheckSpecularHighlights(Material mat_atlas) {
			if (GetScalar(mat_atlas, "_SpecularHighlights", 1) == 0) {
				mat_atlas.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
			} else {
				mat_atlas.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
			}
		}

		public void CheckGlossyReflections(Material mat_atlas) {
			if (GetScalar(mat_atlas, "_GlossyReflections", 1) == 0) {
				mat_atlas.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
			} else {
				mat_atlas.DisableKeyword("_GLOSSYREFLECTIONS_OFF");
			}
		}

		public override void ConfigureAtlasMaterial(Material mat_atlas) {
			mat_atlas.shader = EnsureAtlasShader();
			if (!InstancingMatters)
				mat_atlas.enableInstancing = true;
			mat_atlas.globalIlluminationFlags = MaterialEditor.FixupEmissiveFlag(Color.white, mat_atlas.globalIlluminationFlags);

			mat_atlas.RemoveAllTextures();

			foreach (var atlas_renderer in atlasRenderers)
				atlas_renderer.AtlasReset(mat_atlas);

			// Reset unsupported
			SetTextureNoST(mat_atlas, "_DetailMask", null);
			mat_atlas.DisableKeyword("_DETAIL_MULX2");
			SetTextureNoST(mat_atlas, "_DetailAlbedoMap", null);
			mat_atlas.SetFloat("_DetailNormalMapScale", 1);
			SetTextureNoST(mat_atlas, "_DetailNormalMap", null);
			mat_atlas.SetFloat("_UVSec", 0);

			foreach (var atlas_renderer in atlasRenderers)
				atlas_renderer.AtlasApply(mat_atlas);

			CheckSpecularHighlights(mat_atlas);
			CheckGlossyReflections(mat_atlas);
		}
	}
}
#endif