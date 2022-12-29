using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;
using Codice.CM.Common;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov.KawaShade {
	[Serializable]
	public partial class KawaShadeGenerator : BaseGenerator {
		// GUID of kawa_struct_shared.cginc
		public static readonly string MAIN_CGINC_GUID = "19e348e622400bd4d86b4eff1408f1b9";

		public enum IncludeOrders {
			STRUCT_SHARED = 1000,
			STRUCT_SPEC = 1800,
			LIBRARY = 2000,
			FEATURES = 3000,
			SHADOWS = 4000,
			PREFRAG_SHARED = 5000,
			PREFRAG_SPEC = 5800,
			FRAG_SHARED = 6000,
			FRAG_SPEC = 6800,
		}


		[MenuItem("Kawashirov/KawaShade/Create New Shader KawaShadeGenerator Asset")]
		public static void CreateAsset() {
			var save_path = EditorUtility.SaveFilePanelInProject(
				"New KawaShade Generator Asset", "MyShaderGenerator.asset", "asset",
				"Please enter a file name to save the KawaShadeGenerator to."
			);
			if (string.IsNullOrWhiteSpace(save_path))
				return;

			var generator = CreateInstance<KawaShadeGenerator>();

			generator.shaderName = Path.GetFileNameWithoutExtension(save_path);
			generator.rndDefaultTexture = GetRndDefaultTexture();

			AssetDatabase.CreateAsset(generator, save_path);
		}

		//[MenuItem("Kawashirov/KawaShade/Delete all generated shaders (but not generators)")]
		//public static void Delete() {
		//	var cginc_path = GetMainCGIncPath();
		//	var shader_base_path = Path.GetDirectoryName(cginc_path);
		//	DeleteGeneratedAtPath(shader_base_path);
		//}

		public static string GetMainCGIncPath() {
			var cginc_path = AssetDatabase.GUIDToAssetPath(MAIN_CGINC_GUID);
			if (string.IsNullOrWhiteSpace(cginc_path)) {
				Debug.LogErrorFormat("[KawaShade] Can not get path to kawa_struct_shared.cginc. Does it exist? Does it's GUID = <b>{0}</b>?", MAIN_CGINC_GUID);
				throw new InvalidOperationException("Can not get path to kawa_struct_shared.cginc");
			}
			return cginc_path;
		}

		public static string GetCGIncPath(string filename) {
			var cginc_path = GetMainCGIncPath();
			var shader_base_path = Path.GetDirectoryName(cginc_path);
			return Path.Combine(shader_base_path, filename);
		}

		public static Texture2D GetRndDefaultTexture() {
			if (_rndDefaultTexture == null) {
				try {
					Debug.Log("[KawaShade] Loading RndDefaultTexture...");
					var path = AssetDatabase.GUIDToAssetPath(RND_NOISE_GUID);
					_rndDefaultTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
					if (_rndDefaultTexture == null)
						throw new InvalidOperationException("Texture2D asset is null");
				} catch (Exception exc) {
					Debug.LogWarningFormat(_rndDefaultTexture, "[KawaShade] Loading RndDefaultTexture failed: <i>{0}</i>\n{1}", exc.Message, exc.StackTrace);
					Debug.LogException(exc, _rndDefaultTexture);
				}
				Debug.LogFormat(_rndDefaultTexture, "[KawaShade] Loaded RndDefaultTexture: <i>{0}</i>", _rndDefaultTexture);
			}
			return _rndDefaultTexture;
		}

		private bool ShouldEscapeChar(char c) {
			return char.IsWhiteSpace(c) || Path.GetInvalidFileNameChars().Contains(c) || Path.GetInvalidPathChars().Contains(c);
		}

		private string EscapeName(string n) {
			var sb = new StringBuilder(n.Length);
			foreach (var ch in n) {
				if (ShouldEscapeChar(ch)) {
					if (sb.Length > 0 && sb[sb.Length - 1] != '-')
						sb.Append('-');
				} else {
					sb.Append(ch);
				}
			}
			if (sb.Length > 0 && sb[sb.Length - 1] == '-')
				sb.Remove(sb.Length - 1, 1);
			if (sb.Length > 0 && sb[0] == '-')
				sb.Remove(0, 1);
			return sb.ToString();
		}


		public void ValidateAssetDatabase(out string shader_path, out string this_guid) {
			var cginc_path = GetMainCGIncPath();

			var cginc_object = AssetDatabase.LoadAssetAtPath<TextAsset>(cginc_path);
			if (cginc_object == null) {
				Debug.LogErrorFormat(this, "[KawaShade] Can not load kawa_struct_shared.cginc. GUID = <b>{0}</b>. Path = <i>{1}</i>", MAIN_CGINC_GUID, cginc_path);
				throw new InvalidOperationException("Can not load kawa_struct_shared.cginc");
			}

			var this_path = AssetDatabase.GetAssetPath(this);
			if (string.IsNullOrWhiteSpace(this_path)) {
				Debug.LogErrorFormat(this, "[KawaShade] Can not get path to this generator <b>{0}</b>. Is it persistent?", this);
				throw new InvalidOperationException("Can not get path to this generator.");
			}

			this_guid = AssetDatabase.AssetPathToGUID(this_path);
			if (string.IsNullOrWhiteSpace(this_guid)) {
				Debug.LogErrorFormat(this, "[KawaShade] Can not get GUID of this generator <b>{0}</b>. Is it persistent?", this);
				throw new InvalidOperationException("Can not get GUID of this generator.");
			}

			var shader_base_path = Path.GetDirectoryName(this_path);
			var name_escaped = EscapeName(name);
			shader_path = string.Format("{0}/{1}.shader", shader_base_path, name_escaped);

			var shader_at_path = AssetDatabase.LoadAssetAtPath<Shader>(shader_path);

			// Существует привязанный шейдер, провяеряем совпадает ли он с шейдером, лежащим по верному пути.
			// Если shader_at_path не существует, а result существует, значит result лежит не там, где должен.
			if (result != null && result != shader_at_path) {
				var s2 = AssetDatabase.GetAssetPath(result);
				var s3 = shader_at_path == null ? "null" : shader_at_path.ToString();
				Debug.LogWarningFormat(
					this, "[KawaShade] Shader bound to this generator <b>{0}</b>. Does not match shader at correct path."
					+ "\nBound: <b>{1}</b> at <i>{2}</i>\nCorrect: <b>{3}</b> at <i>{4}</i>",
					this, result, s2, s3, shader_base_path
				);
				// Отвязываем.
				Debug.LogWarningFormat(
					this, "[KawaShade] Unlinking shader bound to this generator <b>{0}</b>: <b>{1}</b> at <i>{2}</i>. Delete it if not need.",
					this, result, s2, s3, shader_base_path
				);
				result = shader_at_path; // может быть null
				EditorUtility.SetDirty(this);
			}

		}

		public override void Refresh() {
			if (string.IsNullOrWhiteSpace(shaderName)) {
				Debug.LogErrorFormat(this, "[KawaShade] Name of shader is not set. <b>{0}</b>\n@ <i>{1}</i>", this, RefreshablePath());
				return;
			}

			ValidateAssetDatabase(out var shader_path, out var this_guid);

			var name = shaderName.Trim();

			var shader = new ShaderSetup {
				name = "Kawashirov/KawaShade/" + name
			};

			shader.tags[Commons.GenaratorGUID] = this_guid;

			ConfigureGeneral(shader);

			foreach (var feature in AbstractFeature.Features.Value)
				feature.ConfigureShaderEarly(this, shader);

			foreach (var feature in AbstractFeature.Features.Value)
				feature.ConfigureShader(this, shader);

			foreach (var feature in AbstractFeature.Features.Value)
				feature.ConfigureShaderLate(this, shader);
			
			shader.custom_editor = "KawaShadeGUI";

			var code = new StringBuilder(1024 * 12);
			code.Append("// DO NOT EDIT THIS FILE\n");
			code.Append("// It's genarated by scripts and used by scripts.\n");
			code.Append("// НЕ ИЗМЕНЯЙТЕ ЭТОТ ФАЙЛ\n");
			code.Append("// Он сгенерирован скриптами и используется скриптами.\n");
			code.Append("//\n");
			shader.Bake(code);

			Debug.LogFormat(this, "[KawaShade] Writing generated shader <b>{0}</b> to disk...\n@ <i>{1}</i>", this, RefreshablePath());
			using (var writer = new StreamWriter(shader_path, false, Encoding.UTF8)) {
				writer.Write(code.ToString());
				writer.Flush();
			}
			// Нам нужно заимпортировать шейдер, для дальнейших манипуляций, даже если это придется делать еще раз.
			AssetDatabase.ImportAsset(shader_path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
			result = AssetDatabase.LoadAssetAtPath<Shader>(shader_path);
			EditorUtility.SetDirty(this); // result modified
			AssetDatabase.SetLabels(result, new string[] { "Kawashirov-Generated-Shader-File", "Kawashirov", "Generated" });
			EditorUtility.SetDirty(result); // shader modified
			ShaderUtil.RegisterShader(result);

			var shader_importer = AssetImporter.GetAtPath(shader_path) as ShaderImporter;
			if (shader_importer == null) {
				Debug.LogWarningFormat(this, "Can not get ShaderImporter for shader <b>{0}</b>!\n@ <i>{1}</i>", this, RefreshablePath());
			} else {
				shader_importer.SetDefaultTextures(new string[] {
					"_Rnd_Seed"
				}, new Texture[] {
					rndDefaultTexture
				});
				EditorUtility.SetDirty(shader_importer);
				shader_importer.SaveAndReimport();
			}

			Shader.WarmupAllShaders();
		}

		private void ConfigureGeneral(ShaderSetup shader) {
			var cginc_path = Path.GetDirectoryName(GetMainCGIncPath());

			needRandomVert = false;
			needRandomFrag = false;
			shader.SkipCommonStaticVariants();

			string struct_module, prefrag_module;

			switch (complexity) {
				case ShaderComplexity.VHDGF:
					shader.TagBool(FeaturePipeline.F_Geometry, true);
					shader.TagBool(FeaturePipeline.F_Tessellation, true);
					struct_module = "kawa_struct_vhdgf.cginc";
					prefrag_module = "kawa_prefrag_vhdgf.cginc";
					shader.Define("KAWAFLT_PIPELINE_VHDGF 1");
					shader.Define("KAWAFLT_F_GEOMETRY 1");
					shader.Define("KAWAFLT_F_TESSELLATION 1");
					break;
				case ShaderComplexity.VGF:
					shader.TagBool(FeaturePipeline.F_Geometry, true);
					shader.TagBool(FeaturePipeline.F_Tessellation, false);
					struct_module = "kawa_struct_vgf.cginc";
					prefrag_module = "kawa_prefrag_vgf.cginc";
					shader.Define("KAWAFLT_PIPELINE_VGF 1");
					shader.Define("KAWAFLT_F_GEOMETRY 1");
					break;
				default:
					shader.TagBool(FeaturePipeline.F_Geometry, false);
					shader.TagBool(FeaturePipeline.F_Tessellation, false);
					struct_module = "kawa_struct_vf.cginc";
					prefrag_module = "kawa_prefrag_vf.cginc";
					shader.Define("KAWAFLT_PIPELINE_VF 1");
					break;
			}

			shader.Include(ShaderInclude.Direct((int)IncludeOrders.STRUCT_SHARED, Path.Combine(cginc_path, "kawa_struct_shared.cginc")));
			shader.Include(ShaderInclude.Direct((int)IncludeOrders.STRUCT_SPEC, Path.Combine(cginc_path, struct_module)));

			// "libraries"
			shader.Include(ShaderInclude.Direct((int)IncludeOrders.LIBRARY + 1, Path.Combine(cginc_path, "kawa_rnd.cginc")));

			// shadows
			shader.Include(ShaderInclude.Direct((int)IncludeOrders.SHADOWS + 10, Path.Combine(cginc_path, "kawa_shading_cubedparadox.cginc")));
			shader.Include(ShaderInclude.Direct((int)IncludeOrders.SHADOWS + 20, Path.Combine(cginc_path, "kawa_shading_kawaflt_common.cginc")));
			shader.Include(ShaderInclude.Direct((int)IncludeOrders.SHADOWS + 21, Path.Combine(cginc_path, "kawa_shading_kawaflt_log.cginc")));
			shader.Include(ShaderInclude.Direct((int)IncludeOrders.SHADOWS + 22, Path.Combine(cginc_path, "kawa_shading_kawaflt_ramp.cginc")));
			shader.Include(ShaderInclude.Direct((int)IncludeOrders.SHADOWS + 23, Path.Combine(cginc_path, "kawa_shading_kawaflt_single.cginc")));

			// pre-fragment stages
			shader.Include(ShaderInclude.Direct((int)IncludeOrders.PREFRAG_SHARED, Path.Combine(cginc_path, "kawa_prefrag_shared.cginc")));
			shader.Include(ShaderInclude.Direct((int)IncludeOrders.PREFRAG_SHARED + 10, Path.Combine(cginc_path, "kawa_shading_kawaflt_fragment_in.cginc")));
			shader.Include(ShaderInclude.Direct((int)IncludeOrders.PREFRAG_SPEC, Path.Combine(cginc_path, prefrag_module)));

			// fragment stages
			shader.Include(ShaderInclude.Direct((int)IncludeOrders.FRAG_SHARED, Path.Combine(cginc_path, "kawa_frag_shared.cginc")));

			shader.forward.cullMode = cull;
			shader.forward.defines.Add("KAWAFLT_PASS_FORWARDBASE 1");
			var forward_base_frag = ShaderInclude.Direct((int)IncludeOrders.FRAG_SPEC, Path.Combine(cginc_path, "kawa_frag_forward_base.cginc"));
			shader.forward.includes.Add(forward_base_frag);
			shader.forward.vertex = "vert";
			shader.forward.hull = complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.forward.domain = complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.forward.geometry = complexity == ShaderComplexity.VHDGF || complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.forward.fragment = "frag_forwardbase";

			shader.forward_add.defines.Add("KAWAFLT_PASS_FORWARDADD 1");
			var forward_add_frag = ShaderInclude.Direct((int)IncludeOrders.FRAG_SPEC, Path.Combine(cginc_path, "kawa_frag_forward_add.cginc"));
			shader.forward_add.includes.Add(forward_add_frag);
			shader.forward_add.vertex = "vert";
			shader.forward_add.hull = complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.forward_add.domain = complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.forward_add.geometry = complexity == ShaderComplexity.VHDGF || complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.forward_add.fragment = "frag_forwardadd";

			shader.shadowcaster.defines.Add("KAWAFLT_PASS_SHADOWCASTER 1");
			var shadowcaster_frag = ShaderInclude.Direct((int)IncludeOrders.FRAG_SPEC, Path.Combine(cginc_path, "kawa_frag_shadow_caster.cginc"));
			shader.shadowcaster.includes.Add(shadowcaster_frag);
			shader.shadowcaster.vertex = "vert";
			shader.shadowcaster.hull = complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.shadowcaster.domain = complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.shadowcaster.geometry = complexity == ShaderComplexity.VHDGF || complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.shadowcaster.fragment = "frag_shadowcaster";
		}

	}
}
