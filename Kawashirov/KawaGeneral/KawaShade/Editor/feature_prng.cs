using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {

	internal static partial class KawaShadeCommons {
		internal static readonly string F_Random = "KawaShade_Feature_Random";
	}

	public partial class KawaShadeGenerator {
		// GUID of noise_256x256_R16.asset
		public static readonly string RND_NOISE_GUID = "de64211a543015d4cb7cbee9b684386b";
		private static Texture2D _rndDefaultTexture = null;

		[NonSerialized] private bool needRandomVert = false;
		[NonSerialized] private bool needRandomFrag = false;
		public bool rndMixTime = false;
		public bool rndMixCords = false;
		public bool rndScreenScale = false;
		public Texture2D rndDefaultTexture = null;

		private void ConfigureFeaturePRNG(ShaderSetup shader) {
			if (needRandomVert) {
				shader.Define("RANDOM_VERT 1");
			}
			if (needRandomFrag) {
				shader.Define("RANDOM_FRAG 1");
			}
			if (needRandomVert || needRandomFrag) {
				shader.TagBool(KawaShadeCommons.F_Random, true);
				shader.Define("RANDOM_SEED_TEX 1");
				shader.properties.Add(new Property2D() { name = "_Rnd_Seed", defualt = "gray" });
				if (rndMixTime) {
					shader.Define("RANDOM_MIX_TIME 1");
				}
				if (rndMixCords) {
					shader.Define("RANDOM_MIX_COORD 1");
				}
				if (rndScreenScale) {
					shader.Define("RANDOM_SCREEN_SCALE 1");
					shader.properties.Add(new PropertyVector() { name = "_Rnd_ScreenScale", defualt = new Vector4(1, 1, 0, 0) });
				}
			} else {
				shader.TagBool(KawaShadeCommons.F_Random, false);
			}
		}
	}

	public partial class KawaShadeGeneratorEditor {
		private void RandomGUI() {
			EditorGUILayout.LabelField("PRNG Settings");
			using (new EditorGUI.IndentLevelScope()) {
				EditorGUILayout.HelpBox(
					"Some features using Pseudo-Random Number KawaShadeGenerator.\n" +
					"These options affects it's behaivor.",
					MessageType.None
				);
				KawaGUIUtility.DefaultPrpertyField(this, "rndMixTime", "Use Time where possible");
				KawaGUIUtility.DefaultPrpertyField(this, "rndMixCords", "Use Screen-Space coords where possible");
				KawaGUIUtility.DefaultPrpertyField(this, "rndScreenScale", "Screen-Space scaling");
				using (new EditorGUILayout.HorizontalScope()) {
					var rndDefaultTexture = serializedObject.FindProperty("rndDefaultTexture");
					KawaGUIUtility.DefaultPrpertyField(rndDefaultTexture, "Default noise texture.");
					if (GUILayout.Button("Default")) {
						rndDefaultTexture.objectReferenceValue = KawaShadeGenerator.GetRndDefaultTexture();
					}
				}
			}
		}
	}

	internal partial class KawaShadeGUI {
		protected void OnGUI_Random() {
			EditorGUILayout.LabelField("PRNG Settings");
			using (new EditorGUI.IndentLevelScope()) {
				var _Rnd_Seed = FindProperty("_Rnd_Seed");
				var label_tex = new GUIContent("Seed Noise", "R16 texture filled with random values to help generating random numbers.");
				if (_Rnd_Seed != null) {
					using (new EditorGUILayout.HorizontalScope()) {
						materialEditor.TexturePropertySingleLine(label_tex, _Rnd_Seed);
						if (GUILayout.Button("Default")) {
							_Rnd_Seed.textureValue = KawaShadeGenerator.GetRndDefaultTexture();
						}
					}

					var value = _Rnd_Seed.textureValue as Texture2D;
					if (value == null) {
						EditorGUILayout.HelpBox(
							"No seed noise texture is set!\n" +
							"Some of enabled features using Pseudo-Random Number KawaShadeGenerator.\n" +
							"This texture is required, and shader will not properly work without this.",
							MessageType.Error
						);
					} else if (value.format != TextureFormat.R16) {
						EditorGUILayout.HelpBox(
							"Seed noise texture is not encoded as R16!\n(Single red channel, 16 bit integer.)\n" +
							"Pseudo-Random Number KawaShadeGenerator features is guaranteed to work only with R16 format.",
							MessageType.Warning
						);
					}
				} else {
					using (new EditorGUI.DisabledScope(true))
						EditorGUILayout.LabelField(label_tex, new GUIContent("Disabled"));
				}

				ShaderPropertyDisabled(FindProperty("_Rnd_ScreenScale"), "Screen Space Scale");
			}
		}
	}
}
