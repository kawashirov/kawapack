#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using static Kawashirov.MaterialCombining.MatCmbUtility;

namespace Kawashirov.MaterialCombining {
	public class EmissionAtlasRenderer : AbstractAtlasRenderer {
		public const string NAME = "Emission";

		public EmissionAtlasRenderer(AbstaractMaterialCombiner combider) : base(combider, NAME) { }

		public static bool EmissiveIsBlack(Material mat) => 
			mat.globalIlluminationFlags.HasFlag(MaterialGlobalIlluminationFlags.EmissiveIsBlack);

		public override void BlitPrepareReset(Material mat_blit) {
			base.BlitPrepareReset(mat_blit);
		}

		public override Vector2Int GetTexSize(MaterialGroup group) {
			var mat_orig = group.matOriginal;
			var is_black = EmissiveIsBlack(mat_orig);
			var em_tex = GetTexture2D(mat_orig, "_EmissionMap", null);
			return is_black || em_tex == null ? Vector2Int.zero : new Vector2Int(em_tex.width, em_tex.height);
		}

		public override void PrepareRenderBackground(Material mat_blit) {
			BlitTexRGBA(mat_blit, Texture2D.blackTexture);
			mat_blit.SetColor("_Color", Color.black);
		}

		public override void PrepareRenderMaterial(MaterialGroup group, Material mat_blit) {
			var mat_orig = group.matOriginal;
			if (EmissiveIsBlack(mat_orig)) {
				BlitTexRGB(mat_blit, Texture2D.blackTexture);
				BlitColorRGB(mat_blit, Color.black);
			} else {
				var em_tex = GetTexture2D(mat_orig, "_EmissionMap", Texture2D.whiteTexture);
				var em_color = GetColor(mat_orig, "_EmissionColor", Color.white);
				BlitTexRGB(mat_blit, em_tex);
				BlitColorRGB(mat_blit, em_color);
			}
		}

		public override string AtlasSaveFormat() => "exr";

		public override void AtlasConfigureImporter(TextureImporter importer) {
			base.AtlasConfigureImporter(importer);
			importer.sRGBTexture = false;
		}

		public override void AtlasReset(Material mat_atlas) {
			mat_atlas.SetColor("_EmissionColor", Color.black);
			mat_atlas.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;
			mat_atlas.DisableKeyword("_EMISSION");
		}

		public override void AtlasApply(Material mat_atlas) {
			SetTextureNoST(mat_atlas, "_EmissionMap", atlasTexture);
			mat_atlas.SetColor("_EmissionColor", Color.white);
			mat_atlas.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
			mat_atlas.EnableKeyword("_EMISSION");
		}
	}
}
#endif