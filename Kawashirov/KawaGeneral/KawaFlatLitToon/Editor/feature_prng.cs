using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using Kawashirov.FLT;

using GUIL = UnityEngine.GUILayout;
using EGUIL = UnityEditor.EditorGUILayout;
using EU = UnityEditor.EditorUtility;
using KST = Kawashirov.ShaderTag;
using KSBC = Kawashirov.ShaderBaking.Commons;
using KFLTC = Kawashirov.FLT.Commons;
using SC = Kawashirov.KawaUtilities;

using static UnityEditor.EditorGUI;

namespace Kawashirov.FLT {

	internal static partial class Commons {
		internal static readonly string F_Random = "KawaFLT_Feature_Random";
	}

	public partial class Generator {
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
				shader.TagBool(KFLTC.F_Random, true);
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
				shader.TagBool(KFLTC.F_Random, false);
			}
		}
	}

	public partial class GeneratorEditor {
		private void RandomGUI() {
			EGUIL.LabelField("PRNG Settings");
			using (new IndentLevelScope()) {
				EGUIL.HelpBox(
					"Some features using Pseudo-Random Number Generator.\n" +
					"These options affects it's behaivor.",
					MessageType.None
				);
				KawaGUIUtilities.DefaultPrpertyField(this, "rndMixTime", "Use Time where possible");
				KawaGUIUtilities.DefaultPrpertyField(this, "rndMixCords", "Use Screen-Space coords where possible");
				KawaGUIUtilities.DefaultPrpertyField(this, "rndScreenScale", "Screen-Space scaling");
				using (new GUIL.HorizontalScope()) {
					var rndDefaultTexture = serializedObject.FindProperty("rndDefaultTexture");
					KawaGUIUtilities.DefaultPrpertyField(rndDefaultTexture, "Default noise texture.");
					if (GUIL.Button("Default")) {
						rndDefaultTexture.objectReferenceValue = Generator.GetRndDefaultTexture();
					}
				}
			}
		}
	}
}

internal partial class KawaFLTShaderGUI {
	protected void OnGUI_Random() {
		EGUIL.LabelField("PRNG Settings");
		using (new IndentLevelScope()) {
			var _Rnd_Seed = FindProperty("_Rnd_Seed");
			var label_tex = new GUIContent("Seed Noise", "R16 texture filled with random values to help generating random numbers.");
			if (_Rnd_Seed != null) {
				using (new GUIL.HorizontalScope()) {
					materialEditor.TexturePropertySingleLine(label_tex, _Rnd_Seed);
					if (GUIL.Button("Default")) {
						_Rnd_Seed.textureValue = Generator.GetRndDefaultTexture();
					}
				}

				var value = _Rnd_Seed.textureValue as Texture2D;
				if (value == null) {
					EGUIL.HelpBox(
						"No seed noise texture is set!\n" +
						"Some of enabled features using Pseudo-Random Number Generator.\n" +
						"This texture is required, and shader will not properly work without this.",
						MessageType.Error
					);
				} else if (value.format != TextureFormat.R16) {
					EGUIL.HelpBox(
						"Seed noise texture is not encoded as R16!\n(Single red channel, 16 bit integer.)\n" +
						"Pseudo-Random Number Generator features is guaranteed to work only with R16 format.",
						MessageType.Warning
					);
				}
			} else {
				using (new DisabledScope(true))
					EGUIL.LabelField(label_tex, new GUIContent("Disabled"));
			}

			ShaderPropertyDisabled(FindProperty("_Rnd_ScreenScale"), "Screen Space Scale");
		}
	}
}
