#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using static Kawashirov.MaterialCombining.MatCmbUtility;

namespace Kawashirov.MaterialCombining {
	public class ParallaxAtlasRenderer : AbstractAtlasRenderer {
		public const string NAME = "Parallax";

		public const float PARALLAX_SCALE_DEFAULT = 0.02f;
		public const float PARALLAX_SCALE_MAX = 0.08f;

		protected float parallaxRef = PARALLAX_SCALE_MAX;

		public ParallaxAtlasRenderer(AbstaractMaterialCombiner combider, float parallaxRef) : base(combider, NAME) {
			this.parallaxRef = parallaxRef;
		}

		public override void BlitPrepareReset(Material mat_blit) {
			base.BlitPrepareReset(mat_blit);
			mat_blit.SetFloat("_ParallaxRef", PARALLAX_SCALE_DEFAULT);
			mat_blit.SetInteger("_ParallaxMode", 0);
		}

		public override Vector2Int GetTexSize(MaterialGroup group) {
			var tex = GetTexture2D(group.matOriginal, "_ParallaxMap", null);
			return tex != null ? new Vector2Int(tex.width, tex.height) : Vector2Int.zero;
		}

		public override void PrepareRenderBackground(Material mat_blit) {
			BlitTexRGBA(mat_blit, Texture2D.whiteTexture);
			mat_blit.SetVector("_Scale", Vector4.zero);
			mat_blit.SetFloat("_ParallaxRef", parallaxRef);
			mat_blit.SetInteger("_ParallaxMode", 1);
		}

		public override void PrepareRenderMaterial(MaterialGroup group, Material mat_blit) {
			var mat_orig = group.matOriginal;
			var parallax_tex = GetTexture2D(mat_orig, "_ParallaxMap", null);
			if (parallax_tex != null) {
				BlitTexRGB(mat_blit, parallax_tex);
				var parallax_scale = GetScalar(mat_orig, "_Parallax", PARALLAX_SCALE_DEFAULT);
				BlitScaleRGB(mat_blit, Vector3.one * parallax_scale);
			} else {
				BlitTexRGB(mat_blit, Texture2D.whiteTexture);
				BlitScaleRGB(mat_blit, Vector3.zero);
			}
			mat_blit.SetFloat("_ParallaxRef", parallaxRef);
			mat_blit.SetInteger("_ParallaxMode", 1);
		}

		public override void AtlasConfigureImporter(TextureImporter importer) {
			base.AtlasConfigureImporter(importer);
			importer.sRGBTexture = false;
		}

		public override void AtlasReset(Material mat_atlas) {
			mat_atlas.SetFloat("_Parallax", parallaxRef);
			mat_atlas.DisableKeyword("_PARALLAXMAP");
		}

		public override void AtlasApply(Material mat_atlas) {
			SetTextureNoST(mat_atlas, "_ParallaxMap", atlasTexture);
			mat_atlas.EnableKeyword("_PARALLAXMAP");
		}

	}
}
#endif