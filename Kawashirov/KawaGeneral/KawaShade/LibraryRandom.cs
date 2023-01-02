using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;

namespace Kawashirov.KawaShade {
	public class LibraryRandom : AbstractFeature {
		internal static readonly string F_Random = "KawaShade_Feature_Random";

		public override int GetOrder() => (int)Order.LIBRARY;

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_Random);
		}

		public override void ConfigureShaderLate(KawaShadeGenerator gen, ShaderSetup shader) {
			var path = KawaShadeGenerator.GetCGIncPath("LibraryRandom.hlsl");
			shader.Include(ShaderInclude.Direct((int)KawaShadeGenerator.IncludeOrders.LIBRARY, path));

			if (gen.needRandomVert) {
				shader.Define("RANDOM_VERT 1");
			}
			if (gen.needRandomFrag) {
				shader.Define("RANDOM_FRAG 1");
			}
			if (gen.needRandomVert || gen.needRandomFrag) {
				shader.TagBool(F_Random, true);
				shader.Define("RANDOM_SEED_TEX 1");
				shader.properties.Add(new Property2D() { name = "_Rnd_Seed", defualt = "gray" });
				if (gen.rndMixCords) {
					shader.Define("RANDOM_MIX_COORD 1");
				}
				if (gen.rndScreenScale) {
					shader.Define("RANDOM_SCREEN_SCALE 1");
					shader.properties.Add(new PropertyVector() { name = "_Rnd_ScreenScale", defualt = new Vector4(1, 1, 0, 0) });
				}
			} else {
				shader.TagBool(F_Random, false);
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			EditorGUILayout.LabelField("PRNG Settings", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope()) {
				EditorGUILayout.HelpBox(
					"Some Features using Pseudo-Random Number KawaShadeGenerator.\n" +
					"These options affects it's behaivor.",
					MessageType.None
				);
				KawaGUIUtility.DefaultPrpertyField(editor, "rndMixTime", "Use Time where possible");
				KawaGUIUtility.DefaultPrpertyField(editor, "rndMixCords", "Use Screen-Space coords where possible");
				KawaGUIUtility.DefaultPrpertyField(editor, "rndScreenScale", "Screen-Space scaling");
				using (new EditorGUILayout.HorizontalScope()) {
					var rndDefaultTexture = editor.serializedObject.FindProperty("rndDefaultTexture");
					KawaGUIUtility.DefaultPrpertyField(rndDefaultTexture, "Default noise texture.");
					if (GUILayout.Button("Default")) {
						rndDefaultTexture.objectReferenceValue = KawaShadeGenerator.GetRndDefaultTexture();
					}
				}
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			EditorGUILayout.LabelField("PRNG Settings", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope()) {

				var _Rnd_Seed = editor.FindProperty("_Rnd_Seed");
				var label_tex = new GUIContent("Seed Noise", "R16 texture filled with random values to help generating random numbers.");
				using (new EditorGUILayout.HorizontalScope()) {
					editor.TexturePropertySingleLineDisabled(label_tex, _Rnd_Seed);
					if (_Rnd_Seed != null && GUILayout.Button("Default")) {
						_Rnd_Seed.textureValue = KawaShadeGenerator.GetRndDefaultTexture();
					}
				}
				if (_Rnd_Seed != null) {
					var value = _Rnd_Seed.textureValue as Texture2D;
					if (value == null) {
						EditorGUILayout.HelpBox(
							"No seed noise texture is set!\n" +
							"Some of enabled Features using Pseudo-Random Number.\n" +
							"This texture is required, and shader will not properly work without this.",
							MessageType.Error
						);
					} else {
						if (value.format != TextureFormat.R16) {
							EditorGUILayout.HelpBox(
								"Seed noise texture is not encoded as R16!\n(Single red channel, 16 bit integer.)\n" +
								"Pseudo-Random Number Features is guaranteed to work only with R16 format.",
								MessageType.Warning
							);
						}
						if (value.filterMode != FilterMode.Point) {
							EditorGUILayout.HelpBox(
								"Seed noise texture is point-filtred!\n(Single red channel, 16 bit integer.)\n" +
								"Pseudo-Random Number Features is guaranteed to work only with point-filtred noise textures.",
								MessageType.Warning
							);
						}
					}
				}
				editor.ShaderPropertyDisabled(editor.FindProperty("_Rnd_ScreenScale"), "Screen Space Scale");
			}
		}
	}

	public partial class KawaShadeGenerator {
		// GUID of noise_256x256_R16.asset
		public static readonly string RND_NOISE_GUID = "de64211a543015d4cb7cbee9b684386b";
		private static Texture2D _rndDefaultTexture = null;

		[NonSerialized] internal bool needRandomVert = false;
		[NonSerialized] internal bool needRandomFrag = false;
		public bool rndMixTime = false;
		public bool rndMixCords = false;
		public bool rndScreenScale = false;
		public Texture2D rndDefaultTexture = null;
	}
}
