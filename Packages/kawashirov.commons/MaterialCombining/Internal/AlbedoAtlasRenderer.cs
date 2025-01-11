#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using static Kawashirov.MaterialCombining.MatCmbUtility;

namespace Kawashirov.MaterialCombining {
	public class AlbedoAtlasRenderer : AbstractAtlasRenderer {
		public const string NAME = "Albedo";

		public AlbedoAtlasRenderer(AbstaractMaterialCombiner combider) : base(combider, NAME) { }

		public override void BlitPrepareReset(Material mat_blit) {
			base.BlitPrepareReset(mat_blit);
			mat_blit.SetInteger("_ColorSpace", 1);
		}

		public override Vector2Int GetTexSize(MaterialGroup group) {
			var tex = GetTexture2D(group.matOriginal, "_MainTex", null);
			return tex != null ? new Vector2Int(tex.width, tex.height) : Vector2Int.zero;
		}

		public override void PrepareRenderBackground(Material mat_blit) {
			BlitTexRGBA(mat_blit, Texture2D.whiteTexture);
			mat_blit.SetColor("_Color", Color.gray);
		}

		public override void PrepareRenderMaterial(MaterialGroup group, Material mat_blit) {
			var mat_orig = group.matOriginal;

			var main_tex = GetTexture2D(mat_orig, "_MainTex", Texture2D.whiteTexture);
			var main_color = GetColor(mat_orig, "_Color", Color.white);

			BlitTexRGBA(mat_blit, main_tex);
			mat_blit.SetColor("_Color", main_color);
		}

		public override void AtlasConfigureImporter(TextureImporter importer) {
			base.AtlasConfigureImporter(importer);
			importer.sRGBTexture = true;
			importer.alphaIsTransparency = true;
			importer.mipMapsPreserveCoverage = true;
		}

		public override void AtlasReset(Material mat_atlas) {
			mat_atlas.SetColor("_Color", Color.white);
		}

		public override void AtlasApply(Material mat_atlas) {
			SetTextureNoST(mat_atlas, "_MainTex", atlasTexture);
		}

	}
}
#endif