#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using static Kawashirov.MaterialCombining.MatCmbUtility;

namespace Kawashirov.MaterialCombining {
	public class SpecSmoothRenderer : AbstractAtlasRenderer {
		public const string NAME = "SpecSmooth";

		public SpecSmoothRenderer(AbstaractMaterialCombiner combider) : base(combider, NAME) { }

		public override void BlitPrepareReset(Material mat_blit) {
			base.BlitPrepareReset(mat_blit);
			mat_blit.SetInteger("_ColorSpace", 1);
		}

		public override Vector2Int GetTexSize(MaterialGroup group) {
			var mat_orig = group.matOriginal;
			var spec_tex = GetTexture2D(mat_orig, "_SpecGlossMap", null);
			Texture2D main_tex = null;
			if (GetScalar(mat_orig, "_SmoothnessTextureChannel", 0) == 1) {
				// Smoothness хранится в _MainTex alpha
				main_tex = GetTexture2D(mat_orig, "_MainTex", null);
			}
			if (spec_tex != null && main_tex != null) {
				if (main_tex.width * main_tex.height < spec_tex.width * spec_tex.height) {
					return new Vector2Int(spec_tex.width, spec_tex.height);
				} else {
					return new Vector2Int(main_tex.width, main_tex.height);
				}
			} else if (main_tex != null) {
				return new Vector2Int(main_tex.width, main_tex.height);
			} else if (spec_tex != null) {
				return new Vector2Int(spec_tex.width, spec_tex.height);
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

			// У "Specular" множитель не применяется к текстуре. Либо то, либо другое.
			// _SPECGLOSSMAP
			var spec_tex = GetTexture2D(mat_orig, "_SpecGlossMap", null);
			if (spec_tex == null) {
				BlitTexRGB(mat_blit, Texture2D.whiteTexture);
				var spec_color = GetColor(mat_orig, "_SpecColor", Color.black);
				BlitColorRGB(mat_blit, spec_color);
			} else {
				BlitTexRGB(mat_blit, spec_tex);
				BlitColorRGB(mat_blit, Color.white);
			}
			BlitScaleRGB(mat_blit, Vector3.one);

			// "Smoothness" множитель это разные проперти, в зависимости от наличия или отсутсвия _SpecGlossMap
			// См DoSpecularMetallicArea() в StandardShaderGUI.cs reference
			var smooth_scale = GetScalar(mat_orig, spec_tex != null ? "_GlossMapScale" : "_Glossiness", 0);
			if (GetScalar(mat_orig, "_SmoothnessTextureChannel", 0) == 1) {
				// Smoothness хранится в _MainTex alpha
				var main_tex = GetTexture2D(mat_orig, "_MainTex", Texture2D.whiteTexture);
				BlitTexA(mat_blit, main_tex);
			} else {
				// Smoothness хранится в _SpecGlossMap alpha
				BlitTexA(mat_blit, spec_tex != null ? spec_tex : Texture2D.whiteTexture);
			}
			BlitScaleA(mat_blit, smooth_scale);
			BlitColorA(mat_blit, 1);
		}

		public override void AtlasConfigureImporter(TextureImporter importer) {
			base.AtlasConfigureImporter(importer);
			importer.sRGBTexture = true;
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
			SetTextureNoST(mat_atlas, "_SpecGlossMap", atlasTexture);
			mat_atlas.EnableKeyword("_SPECGLOSSMAP");
		}

	}
}
#endif