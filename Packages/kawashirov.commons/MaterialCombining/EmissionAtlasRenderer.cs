#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using static Kawashirov.MaterialCombining.MatCmbUtility;

namespace Kawashirov.MaterialCombining {
	public class EmissionAtlasRenderer : AbstractAtlasRenderer {
		public const string NAME = "Emission";

		public EmissionAtlasRenderer(AbstaractMaterialCombiner combider) : base(combider, NAME) { }

		public override void BlitPrepareReset(Material mat_blit) {
			base.BlitPrepareReset(mat_blit);
		}

		public override Vector2Int GetTexSize(MaterialGroup group) {
			var mat_orig = group.matOriginal;
			var flags = mat_orig.globalIlluminationFlags;
			var cant_emit = (flags & MaterialGlobalIlluminationFlags.AnyEmissive) == 0;
			var is_black = (flags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) != 0;
			var em_tex = GetTexture2D(mat_orig, "_EmissionMap", null);
			return cant_emit || is_black || em_tex == null ? Vector2Int.zero : new Vector2Int(em_tex.width, em_tex.height);
		}

		public override void PrepareRenderBackground(Material mat_blit) {
			BlitTexRGBA(mat_blit, Texture2D.blackTexture);
			mat_blit.SetColor("_Color", Color.black);
		}

		public override void PrepareRenderMaterial(MaterialGroup group, Material mat_blit) {
			var mat_orig = group.matOriginal;
			var flags = mat_orig.globalIlluminationFlags;
			var can_emit = (flags & MaterialGlobalIlluminationFlags.AnyEmissive) != 0;
			var isnt_black = (flags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0;
			if (can_emit && isnt_black) {
				var em_tex = GetTexture2D(mat_orig, "_EmissionMap", Texture2D.whiteTexture);
				var em_color = GetColor(mat_orig, "_EmissionColor", Color.white);
				BlitTexRGB(mat_blit, em_tex);
				BlitColorRGB(mat_blit, em_color);
			} else {
				BlitTexRGB(mat_blit, Texture2D.blackTexture);
				BlitColorRGB(mat_blit, Color.black);
			}
		}

		public override void AtlasConfigureImporter(TextureImporter importer) {
			base.AtlasConfigureImporter(importer);
			importer.sRGBTexture = false;
		}

		public override void AtlasReset(Material mat_atlas) {
			mat_atlas.SetColor("_EmissionColor", Color.black);
			mat_atlas.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
			mat_atlas.DisableKeyword("_EMISSION");
		}

		public override void AtlasApply(Material mat_atlas) {
			SetTextureNoST(mat_atlas, "_EmissionMap", atlasTexture);
			mat_atlas.SetColor("_EmissionColor", Color.white);
			mat_atlas.globalIlluminationFlags = DefaultMaterialGlobalIlluminationFlags();
			mat_atlas.EnableKeyword("_EMISSION");
		}
	}
}
#endif