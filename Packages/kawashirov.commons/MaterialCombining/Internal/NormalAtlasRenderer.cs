#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using static Kawashirov.MaterialCombining.MatCmbUtility;

namespace Kawashirov.MaterialCombining {
	public class NormalAtlasRenderer : AbstractAtlasRenderer {
		public const string NAME = "Normal";

		public NormalAtlasRenderer(AbstaractMaterialCombiner combider) : base(combider, NAME) { }

		public override void BlitPrepareReset(Material mat_blit) {
			base.BlitPrepareReset(mat_blit);
		}

		public override Vector2Int GetTexSize(MaterialGroup group) {
			var tex = GetTexture2D(group.matOriginal, "_BumpMode", null);
			return tex != null ? new Vector2Int(tex.width, tex.height) : Vector2Int.zero;
		}

		public override void PrepareRenderBackground(Material mat_blit) {
			BlitTexRGBA(mat_blit, Texture2D.normalTexture);
			mat_blit.SetInteger("_BumpMode", 1);
		}

		public override void PrepareRenderMaterial(MaterialGroup group, Material mat_blit) {
			var mat_orig = group.matOriginal;
			var bumpmap_tex = GetTexture2D(mat_orig, "_BumpMap", Texture2D.normalTexture);
			var bumpmap_scale = GetScalar(mat_orig, "_BumpScale", 1);
			// Карта нормалей в Unity использует RGBA, 
			// т.к. G и A каналы имеют лучшее качество с блочной компрессией
			BlitTexRGBA(mat_blit, bumpmap_tex);
			mat_blit.SetVector("_Scale", Vector4.one * bumpmap_scale);
			mat_blit.SetInteger("_BumpMode", 1);
		}

		public override void AtlasConfigureImporter(TextureImporter importer) {
			base.AtlasConfigureImporter(importer);
			importer.sRGBTexture = true;
			importer.textureType = TextureImporterType.NormalMap;
		}

		public override void AtlasReset(Material mat_atlas) {
			mat_atlas.SetFloat("_BumpScale", 1);
			mat_atlas.DisableKeyword("_NORMALMAP");
		}

		public override void AtlasApply(Material mat_atlas) {
			SetTextureNoST(mat_atlas, "_BumpMap", atlasTexture);
			mat_atlas.EnableKeyword("_NORMALMAP");
		}

	}
}
#endif