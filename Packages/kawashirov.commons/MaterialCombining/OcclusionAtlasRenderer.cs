#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using static Kawashirov.MaterialCombining.MatCmbUtility;

namespace Kawashirov.MaterialCombining {
	public class OcclusionAtlasRenderer : AbstractAtlasRenderer {
		public const string NAME = "Occlusion";

		public OcclusionAtlasRenderer(AbstaractMaterialCombiner combider) : base(combider, NAME) { }

		public override void BlitPrepareReset(Material mat_blit) {
			base.BlitPrepareReset(mat_blit);
		}

		public override Vector2Int GetTexSize(MaterialGroup group) {
			var tex = GetTexture2D(group.matOriginal, "_OcclusionMap", null);
			return tex != null ? new Vector2Int(tex.width, tex.height) : Vector2Int.zero;
		}

		public override void PrepareRenderBackground(Material mat_blit) {
			BlitTexRGBA(mat_blit, Texture2D.whiteTexture);
			mat_blit.SetInteger("_OcclusionMode", 1);
		}

		public override void PrepareRenderMaterial(MaterialGroup group, Material mat_blit) {
			var mat_orig = group.matOriginal;
			var occ_tex = GetTexture2D(mat_orig, "_OcclusionMap", Texture2D.whiteTexture);
			var occ_str = GetScalar(mat_orig, "_OcclusionStrength", 1);
			BlitTexRGB(mat_blit, occ_tex);
			mat_blit.SetVector("_Scale", Vector4.one * occ_str);
		}

		public override void AtlasConfigureImporter(TextureImporter importer) {
			base.AtlasConfigureImporter(importer);
		}

		public override void AtlasReset(Material mat_atlas) {
			mat_atlas.SetFloat("_OcclusionStrength", 1);
			// No keywords for Occlusion
		}

		public override void AtlasApply(Material mat_atlas) {
			SetTextureNoST(mat_atlas, "_OcclusionMap", atlasTexture);
			// No keywords for Occlusion
		}

	}
}
#endif