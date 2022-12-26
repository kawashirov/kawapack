using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov.KawaShade {
	[Serializable]
	public partial class KawaShadeGenerator : ShaderBaking.BaseGenerator {
		// GUID of kawa_struct_shared.cginc
		public static readonly string MAIN_CGINC_GUID = "19e348e622400bd4d86b4eff1408f1b9";

		public CullMode cull = CullMode.Back;
		public bool instancing = true;
		public bool disableBatching = false;
		public bool forceNoShadowCasting = false;
		public bool ignoreProjector = true;
		//public bool zWrite = true;


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

		[MenuItem("Kawashirov/KawaShade/Delete all generated shaders (but not generators)")]
		public static void Delete() {
			var cginc_path = GetMainCGIncPath();
			var shader_base_path = Path.GetDirectoryName(cginc_path);
			DeleteGeneratedAtPath(shader_base_path);
		}

		public static string GetMainCGIncPath() {
			var cginc_path = AssetDatabase.GUIDToAssetPath(MAIN_CGINC_GUID);
			if (string.IsNullOrWhiteSpace(cginc_path)) {
				Debug.LogErrorFormat("[KawaFLT] Can not get path to kawa_struct_shared.cginc. Does it exist? Does it's GUID = <b>{0}</b>?", MAIN_CGINC_GUID);
				throw new InvalidOperationException("Can not get path to kawa_struct_shared.cginc");
			}
			return cginc_path;
		}

		public static Texture2D GetRndDefaultTexture() {
			if (_rndDefaultTexture == null) {
				try {
					Debug.Log("[KawaFLT] Loading RndDefaultTexture...");
					var path = AssetDatabase.GUIDToAssetPath(RND_NOISE_GUID);
					_rndDefaultTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
					if (_rndDefaultTexture == null)
						throw new InvalidOperationException("Texture2D asset is null");
				} catch (Exception exc) {
					Debug.LogWarningFormat(_rndDefaultTexture, "[KawaFLT] Loading RndDefaultTexture failed: <i>{0}</i>\n{1}", exc.Message, exc.StackTrace);
					Debug.LogException(exc, _rndDefaultTexture);
				}
				Debug.LogFormat(_rndDefaultTexture, "[KawaFLT] Loaded RndDefaultTexture: <i>{0}</i>", _rndDefaultTexture);
			}
			return _rndDefaultTexture;
		}

		public void ValidateAssetDatabase(out string shader_path, out string this_guid) {
			var cginc_path = GetMainCGIncPath();

			var cginc_object = AssetDatabase.LoadAssetAtPath<TextAsset>(cginc_path);
			if (cginc_object == null) {
				Debug.LogErrorFormat(this, "[KawaFLT] Can not load kawa_struct_shared.cginc. GUID = <b>{0}</b>. Path = <i>{1}</i>", MAIN_CGINC_GUID, cginc_path);
				throw new InvalidOperationException("Can not load kawa_struct_shared.cginc");
			}

			var this_path = AssetDatabase.GetAssetPath(this);
			if (string.IsNullOrWhiteSpace(this_path)) {
				Debug.LogErrorFormat(this, "[KawaFLT] Can not get path to this generator <b>{0}</b>. Is it persistent?", this);
				throw new InvalidOperationException("Can not get path to this generator.");
			}

			this_guid = AssetDatabase.AssetPathToGUID(this_path);
			if (string.IsNullOrWhiteSpace(this_guid)) {
				Debug.LogErrorFormat(this, "[KawaFLT] Can not get GUID of this generator <b>{0}</b>. Is it persistent?", this);
				throw new InvalidOperationException("Can not get GUID of this generator.");
			}

			var shader_base_path = Path.GetDirectoryName(cginc_path);
			shader_path = string.Format("{0}/_generated_{1}.shader", shader_base_path, this_guid);

			var shader_at_path = AssetDatabase.LoadAssetAtPath<Shader>(shader_path);

			// Существует привязанный шейдер, провяеряем совпадает ли он с шейдером, лежащим по верному пути.
			// Если shader_at_path не существует, а result существует, значит result лежит не там, где должен.
			if (result != null && result != shader_at_path) {
				var s2 = AssetDatabase.GetAssetPath(result);
				var s3 = shader_at_path == null ? "null" : shader_at_path.ToString();
				Debug.LogWarningFormat(
					this, "[KawaFLT] Shader bound to this generator <b>{0}</b>. Does not match shader at correct path."
					+ "\nBound: <b>{1}</b> at <i>{2}</i>\nCorrect: <b>{3}</b> at <i>{4}</i>",
					this, result, s2, s3, shader_base_path
				);
				// Отвязываем.
				Debug.LogWarningFormat(
					this, "[KawaFLT] Unlinking shader bound to this generator <b>{0}</b>: <b>{1}</b> at <i>{2}</i>. Delete it if not need.",
					this, result, s2, s3, shader_base_path
				);
				result = shader_at_path; // может быть null
				EditorUtility.SetDirty(this);
			}

		}

		public override void Refresh() {
			if (string.IsNullOrWhiteSpace(shaderName)) {
				Debug.LogErrorFormat(this, "[KawaFLT] Name of shader is not set. <b>{0}</b>\n@ <i>{1}</i>", this, RefreshablePath());
				return;
			}

			ValidateAssetDatabase(out var shader_path, out var this_guid);

			var name = shaderName.Trim();

			var shader = new ShaderSetup {
				name = "Kawashirov/KawaShade/" + name
			};

			shader.tags[Commons.GenaratorGUID] = this_guid;

			ConfigureGeneral(shader);
			ConfigureTess(shader);
			ConfigureBlending(shader);
			ConfigureFeatureMainTex(shader);
			ConfigureFeatureCutoff(shader);
			ConfigureFeatureEmission(shader);
			ConfigureFeatureNormalMap(shader);

			ConfigureFeatureShading(shader);

			ConfigureFeatureMatcap(shader);
			ConfigureFeatureDistanceFade(shader);
			ConfigureFeatureWNoise(shader);
			ConfigureFeatureFPS(shader);
			ConfigureFeaturePSX(shader);

			ConfigureFeatureOutline(shader);
			ConfigureFeatureInfinityWarDecimation(shader);
			ConfigureFeaturePolyColorWave(shader);

			ConfigureFeaturePRNG(shader);

			shader.custom_editor = "KawaShadeGUI";

			var code = new StringBuilder(1024 * 12);
			code.Append("// DO NOT EDIT THIS FILE\n");
			code.Append("// It's genarated by scripts and used by scripts.\n");
			code.Append("// НЕ ИЗМЕНЯЙТЕ ЭТОТ ФАЙЛ\n");
			code.Append("// Он сгенерирован скриптами и используется скриптами.\n");
			shader.Bake(code);

			Debug.LogFormat(this, "[KawaFLT] Writing generated shader <b>{0}</b> to disk...\n@ <i>{1}</i>", this, RefreshablePath());
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
			needRandomVert = false;
			needRandomFrag = false;
			shader.SkipCommonStaticVariants();
			shader.Debug(debug);

			var f_batching = !disableBatching;
			var f_instancing = instancing;
			if (complexity == ShaderComplexity.VHDGF) {
				f_instancing = false;
				f_batching = false;
			}
			if (iwd) {
				f_instancing = false;
				f_batching = false;
			}
			if (f_batching == false) {
				f_instancing = false;
			}

			shader.TagBool(KawaShadeCommons.F_Instancing, f_instancing);
			shader.TagBool(ShaderTag.DisableBatching, !f_batching);
			shader.TagBool(ShaderTag.ForceNoShadowCasting, forceNoShadowCasting);
			shader.TagBool(ShaderTag.IgnoreProjector, ignoreProjector);

			switch (complexity) {
				case ShaderComplexity.VHDGF:
					shader.TagBool(KawaShadeCommons.F_Geometry, true);
					shader.TagBool(KawaShadeCommons.F_Tessellation, true);
					shader.Include("kawa_struct_vhdgf.cginc");
					shader.Include("kawa_prefrag_vhdgf.cginc");
					shader.Define("KAWAFLT_PIPELINE_VHDGF 1");
					shader.Define("KAWAFLT_F_GEOMETRY 1");
					shader.Define("KAWAFLT_F_TESSELLATION 1");
					break;
				case ShaderComplexity.VGF:
					shader.TagBool(KawaShadeCommons.F_Geometry, true);
					shader.TagBool(KawaShadeCommons.F_Tessellation, false);
					shader.Include("kawa_struct_vgf.cginc");
					shader.Include("kawa_prefrag_vgf.cginc");
					shader.Define("KAWAFLT_PIPELINE_VGF 1");
					shader.Define("KAWAFLT_F_GEOMETRY 1");
					break;
				default:
					shader.TagBool(KawaShadeCommons.F_Geometry, false);
					shader.TagBool(KawaShadeCommons.F_Tessellation, false);
					shader.Include("kawa_struct_vf.cginc");
					shader.Include("kawa_prefrag_vf.cginc");
					shader.Define("KAWAFLT_PIPELINE_VF 1");
					break;
			}

			//shader.forward.zWrite = this.zWrite;
			shader.forward.cullMode = cull;
			shader.forward.multi_compile_instancing = f_instancing;
			shader.forward.defines.Add("KAWAFLT_PASS_FORWARDBASE 1");
			shader.forward.includes.Add("kawa_frag_forward_base.cginc");
			shader.forward.vertex = "vert";
			shader.forward.hull = complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.forward.domain = complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.forward.geometry = complexity == ShaderComplexity.VHDGF || complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.forward.fragment = "frag_forwardbase";

			shader.forward_add.zWrite = false;
			shader.forward_add.cullMode = cull;
			shader.forward_add.multi_compile_instancing = f_instancing;
			shader.forward_add.defines.Add("KAWAFLT_PASS_FORWARDADD 1");
			shader.forward_add.includes.Add("kawa_frag_forward_add.cginc");
			shader.forward_add.vertex = "vert";
			shader.forward_add.hull = complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.forward_add.domain = complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.forward_add.geometry = complexity == ShaderComplexity.VHDGF || complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.forward_add.fragment = "frag_forwardadd";

			shader.shadowcaster.active = !forceNoShadowCasting;
			shader.shadowcaster.cullMode = cull;
			shader.shadowcaster.multi_compile_instancing = f_instancing;
			shader.shadowcaster.defines.Add("KAWAFLT_PASS_SHADOWCASTER 1");
			shader.shadowcaster.includes.Add("kawa_frag_shadow_caster.cginc");
			shader.shadowcaster.vertex = "vert";
			shader.shadowcaster.hull = complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.shadowcaster.domain = complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.shadowcaster.geometry = complexity == ShaderComplexity.VHDGF || complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.shadowcaster.fragment = "frag_shadowcaster";
		}

	}
}
