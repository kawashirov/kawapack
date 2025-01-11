#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using static Kawashirov.MaterialCombining.MatCmbUtility;

namespace Kawashirov.MaterialCombining {
	public class MetalSmoothRenderer : AbstractAtlasRenderer {
		public const string NAME = "MetalSmooth";

		public MetalSmoothRenderer(AbstaractMaterialCombiner combider) : base(combider, NAME) { }

		public override void BlitPrepareReset(Material mat_blit) {
			base.BlitPrepareReset(mat_blit);
		}

		public override Vector2Int GetTexSize(MaterialGroup group) {
			var mat_orig = group.matOriginal;
			var metal_tex = GetTexture2D(mat_orig, "_MetallicGlossMap", null);
			Texture2D main_tex = null;
			if (GetScalar(mat_orig, "_SmoothnessTextureChannel", 0) == 1) {
				// Smoothness хранится в _MainTex alpha
				main_tex = GetTexture2D(mat_orig, "_MainTex", null);
			}
			if (metal_tex != null && main_tex != null) {
				if (main_tex.width * main_tex.height < metal_tex.width * metal_tex.height) {
					return new Vector2Int(metal_tex.width, metal_tex.height);
				} else {
					return new Vector2Int(main_tex.width, main_tex.height);
				}
			} else if (main_tex != null) {
				return new Vector2Int(main_tex.width, main_tex.height);
			} else if (metal_tex != null) {
				return new Vector2Int(metal_tex.width, metal_tex.height);
			} else {
				return Vector2Int.zero;
			}
		}

		public override void PrepareRenderBackground(Material mat_blit) {
			BlitTexRGBA(mat_blit, Texture2D.blackTexture);
			BlitColorRGB(mat_blit, Color.black);
			BlitColorA(mat_blit, 0);
		}

		public override void PrepareRenderMaterial(MaterialGroup group, Material mat_blit) {
			var mat_orig = group.matOriginal;

			// У "Metallic" множитель не применяется к текстуре. Либо то, либо другое.
			var metal_tex = GetTexture2D(mat_orig, "_MetallicGlossMap", null);
			if (metal_tex == null) {
				BlitTexRGB(mat_blit, Texture2D.whiteTexture);
				// _Metallic это [Gamma] по этому как цвет.
				var metal_scale = GetScalar(mat_orig, "_Metallic", 0);
				BlitColorRGB(mat_blit, Color.white * metal_scale);
			} else {
				BlitTexRGB(mat_blit, metal_tex);
				BlitColorRGB(mat_blit, Color.white);
			}
			BlitScaleRGB(mat_blit, Vector3.one);

			// "Smoothness" множитель это разные проперти, в зависимости от наличия или отсутсвия _MetallicGlossMap
			// См DoSpecularMetallicArea() в StandardShaderGUI.cs reference
			var smooth_scale = GetScalar(mat_orig, metal_tex != null ? "_GlossMapScale" : "_Glossiness", 0);
			if (GetScalar(mat_orig, "_SmoothnessTextureChannel", 0) == 1) {
				// Smoothness хранится в _MainTex alpha
				var main_tex = GetTexture2D(mat_orig, "_MainTex", Texture2D.whiteTexture);
				BlitTexA(mat_blit, main_tex);
			} else {
				// Smoothness хранится в _MetallicGlossMap alpha
				BlitTexA(mat_blit, metal_tex != null ? metal_tex : Texture2D.whiteTexture);
			}
			BlitScaleA(mat_blit, smooth_scale);
			BlitColorA(mat_blit, 1);
		}

		public override void AtlasConfigureImporter(TextureImporter importer) {
			base.AtlasConfigureImporter(importer);
		}

		public override void AtlasReset(Material mat_atlas) {
			mat_atlas.SetFloat("_GlossMapScale", 1);
			mat_atlas.SetFloat("_Metallic", 1);
			mat_atlas.DisableKeyword("_METALLICGLOSSMAP");
			mat_atlas.DisableKeyword("_SPECGLOSSMAP");
			// Smooth всегда вместе с metalic/specular 
			mat_atlas.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
		}

		public override void AtlasApply(Material mat_atlas) {
			SetTextureNoST(mat_atlas, "_MetallicGlossMap", atlasTexture);
			mat_atlas.EnableKeyword("_METALLICGLOSSMAP");
		}

	}
}
#endif