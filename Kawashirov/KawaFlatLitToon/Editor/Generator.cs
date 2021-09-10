using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

using EU = UnityEditor.EditorUtility;
using KST = Kawashirov.ShaderTag;
using KSBC = Kawashirov.ShaderBaking.Commons;
using KFLTC = Kawashirov.FLT.Commons;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov.FLT {
	[Serializable]
	public class Generator : ShaderBaking.BaseGenerator {
		// GUID of KawaFLT_Struct_Shared.cginc
		public static readonly string MAIN_CGINC_GUID = "19e348e622400bd4d86b4eff1408f1b9";
		// GUID of noise_256x256_R16.asset
		public static readonly string RND_NOISE_GUID = "de64211a543015d4cb7cbee9b684386b";

		private static Texture2D _rndDefaultTexture = null;

		public ShaderComplexity complexity = ShaderComplexity.VF;
		public TessPartitioning tessPartitioning = TessPartitioning.Integer;
		public TessDomain tessDomain = TessDomain.Triangles;
		public BlendTemplate mode = BlendTemplate.Opaque;
		public CullMode cull = CullMode.Back;
		public bool instancing = true;
		public int queueOffset = 0;
		public bool disableBatching = false;
		public bool forceNoShadowCasting = false;
		public bool ignoreProjector = true;
		//public bool zWrite = true;

		public MainTexKeywords mainTex = MainTexKeywords.ColorMask;
		public CutoutMode cutout = CutoutMode.Classic;
		public bool emission = true;
		public EmissionMode emissionMode = EmissionMode.Custom;
		public bool bumpMap = false;

		public bool rndMixTime = false;
		public bool rndMixCords = false;
		public bool rndScreenScale = false;
		[NonSerialized] private bool needRandomVert = false;
		[NonSerialized] private bool needRandomFrag = false;
		public Texture2D rndDefaultTexture = null;

		public ShadingMode shading = ShadingMode.KawashirovFLTSingle;

		public bool matcap = false;
		public MatcapMode matcapMode = MatcapMode.Multiple;

		public bool distanceFade = false;
		public DistanceFadeMode distanceFadeMode = DistanceFadeMode.Range;

		public bool wnoise = false;

		public bool FPS = false;
		public FPSMode FPSMode = FPSMode.ColorTint;

		public bool outline = false;
		public OutlineMode outlineMode = OutlineMode.Tinted;

		public bool iwd = false;
		public IWDDirections iwdDirections = 0;

		public bool pcw = false;
		public PolyColorWaveMode pcwMode = PolyColorWaveMode.Classic;

		[MenuItem("Kawashirov/Flat Lit Toon Shader/Create New Shader Generator Asset")]
		public static void CreateAsset() {
			var save_path = EU.SaveFilePanelInProject(
				"New Flat Lit Toon Shader Generator Asset", "MyShaderGenerator.asset", "asset",
				"Please enter a file name to save the Generator to."
			);
			if (string.IsNullOrWhiteSpace(save_path))
				return;

			var generator = CreateInstance<Generator>();

			generator.shaderName = Path.GetFileNameWithoutExtension(save_path);
			generator.rndDefaultTexture = GetRndDefaultTexture();

			AssetDatabase.CreateAsset(generator, save_path);
		}

		[MenuItem("Kawashirov/Flat Lit Toon Shader/Delete all generated shaders (but not generators)")]
		public static void Delete() {
			var cginc_path = GetMainCGIncPath();
			var shader_base_path = Path.GetDirectoryName(cginc_path);
			DeleteGeneratedAtPath(shader_base_path);
		}

		public static string GetMainCGIncPath() {
			var cginc_path = AssetDatabase.GUIDToAssetPath(MAIN_CGINC_GUID);
			if (string.IsNullOrWhiteSpace(cginc_path)) {
				Debug.LogErrorFormat("[KawaFLT] Can not get path to KawaFLT_Struct_Shared.cginc. Does it exist? Does it's GUID = <b>{0}</b>?", MAIN_CGINC_GUID);
				throw new InvalidOperationException("Can not get path to KawaFLT_Struct_Shared.cginc");
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
				Debug.LogErrorFormat(this, "[KawaFLT] Can not load KawaFLT_Struct_Shared.cginc. GUID = <b>{0}</b>. Path = <i>{1}</i>", MAIN_CGINC_GUID, cginc_path);
				throw new InvalidOperationException("Can not load KawaFLT_Struct_Shared.cginc");
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
				EU.SetDirty(this);
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
				name = name
			};

			shader.tags[KSBC.GenaratorGUID] = this_guid;

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

			ConfigureFeatureOutline(shader);
			ConfigureFeatureInfinityWarDecimation(shader);
			ConfigureFeaturePolyColorWave(shader);

			ConfigureFeaturePRNG(shader);

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
			EU.SetDirty(this); // result modified
			AssetDatabase.SetLabels(result, new string[] { "Kawashirov-Generated-Shader-File", "Kawashirov", "Generated" });
			EU.SetDirty(result); // shader modified
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
				EU.SetDirty(shader_importer);
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

			shader.TagBool(KFLTC.F_Instancing, f_instancing);
			shader.TagBool(KST.DisableBatching, !f_batching);
			shader.TagBool(KST.ForceNoShadowCasting, forceNoShadowCasting);
			shader.TagBool(KST.IgnoreProjector, ignoreProjector);

			switch (complexity) {
				case ShaderComplexity.VHDGF:
					shader.TagBool(KFLTC.F_Geometry, true);
					shader.TagBool(KFLTC.F_Tessellation, true);
					shader.Include("KawaFLT_Struct_VHDGF.cginc");
					shader.Include("KawaFLT_PreFrag_VHDGF.cginc");
					shader.Define("KAWAFLT_PIPELINE_VHDGF 1");
					shader.Define("KAWAFLT_F_GEOMETRY 1");
					shader.Define("KAWAFLT_F_TESSELLATION 1");
					break;
				case ShaderComplexity.VGF:
					shader.TagBool(KFLTC.F_Geometry, true);
					shader.TagBool(KFLTC.F_Tessellation, false);
					shader.Include("KawaFLT_Struct_VGF.cginc");
					shader.Include("KawaFLT_PreFrag_VGF.cginc");
					shader.Define("KAWAFLT_PIPELINE_VGF 1");
					shader.Define("KAWAFLT_F_GEOMETRY 1");
					break;
				default:
					shader.TagBool(KFLTC.F_Geometry, false);
					shader.TagBool(KFLTC.F_Tessellation, false);
					shader.Include("KawaFLT_Struct_VF.cginc");
					shader.Include("KawaFLT_PreFrag_VF.cginc");
					shader.Define("KAWAFLT_PIPELINE_VF 1");
					break;
			}

			//shader.forward.zWrite = this.zWrite;
			shader.forward.cullMode = cull;
			shader.forward.multi_compile_instancing = f_instancing;
			shader.forward.defines.Add("KAWAFLT_PASS_FORWARDBASE 1");
			shader.forward.includes.Add("KawaFLT_Frag_ForwardBase.cginc");
			shader.forward.vertex = "vert";
			shader.forward.hull = complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.forward.domain = complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.forward.geometry = complexity == ShaderComplexity.VHDGF || complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.forward.fragment = "frag_forwardbase";

			shader.forward_add.zWrite = false;
			shader.forward_add.cullMode = cull;
			shader.forward_add.multi_compile_instancing = f_instancing;
			shader.forward_add.defines.Add("KAWAFLT_PASS_FORWARDADD 1");
			shader.forward_add.includes.Add("KawaFLT_Frag_ForwardAdd.cginc");
			shader.forward_add.vertex = "vert";
			shader.forward_add.hull = complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.forward_add.domain = complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.forward_add.geometry = complexity == ShaderComplexity.VHDGF || complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.forward_add.fragment = "frag_forwardadd";

			shader.shadowcaster.active = !forceNoShadowCasting;
			shader.shadowcaster.cullMode = cull;
			shader.shadowcaster.multi_compile_instancing = f_instancing;
			shader.shadowcaster.defines.Add("KAWAFLT_PASS_SHADOWCASTER 1");
			shader.shadowcaster.includes.Add("KawaFLT_Frag_ShadowCaster.cginc");
			shader.shadowcaster.vertex = "vert";
			shader.shadowcaster.hull = complexity == ShaderComplexity.VHDGF ? "hull" : null;
			shader.shadowcaster.domain = complexity == ShaderComplexity.VHDGF ? "domain" : null;
			shader.shadowcaster.geometry = complexity == ShaderComplexity.VHDGF || complexity == ShaderComplexity.VGF ? "geom" : null;
			shader.shadowcaster.fragment = "frag_shadowcaster";
		}


		private void ConfigureTess(ShaderSetup shader) {
			if (complexity != ShaderComplexity.VHDGF)
				return;

			shader.TagEnum(KFLTC.F_Partitioning, tessPartitioning);
			switch (tessPartitioning) {
				case TessPartitioning.Integer:
					shader.Define("TESS_P_INT 1");
					break;
				case TessPartitioning.FractionalEven:
					shader.Define("TESS_P_EVEN 1");
					break;
				case TessPartitioning.FractionalOdd:
					shader.Define("TESS_P_ODD 1");
					break;
				case TessPartitioning.Pow2:
					shader.Define("TESS_P_POW2 1");
					break;
			}

			shader.TagEnum(KFLTC.F_Domain, tessDomain);
			switch (tessDomain) {
				case TessDomain.Triangles:
					shader.Define("TESS_D_TRI 1");
					break;
				case TessDomain.Quads:
					shader.Define("TESS_D_QUAD 1");
					break;
			}

			shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Uni", defualt = 1, range = new Vector2(0.9f, 10), power = 2 });
			shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Nrm", defualt = 1, range = new Vector2(0, 20), power = 2 });
			shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Inside", defualt = 1, range = new Vector2(0.1f, 10), power = 10 });
		}

		private void ConfigureBlending(ShaderSetup shader) {
			string q = null;
			if (mode == BlendTemplate.Opaque) {
				q = "Geometry";
				shader.tags[KST.RenderType] = "Opaque";
				shader.tags[KFLTC.RenderType] = "Opaque";
				shader.forward.srcBlend = BlendMode.One;
				shader.forward.dstBlend = BlendMode.Zero;
				shader.forward.zWrite = true;
				shader.forward_add.srcBlend = BlendMode.One;
				shader.forward_add.dstBlend = BlendMode.One;
				shader.forward_add.zWrite = false;
			} else if (mode == BlendTemplate.Cutout) {
				q = "AlphaTest";
				shader.tags[KST.RenderType] = "TransparentCutout";
				shader.tags[KFLTC.RenderType] = "Cutout";
				shader.Define("_ALPHATEST_ON 1");
				shader.forward.srcBlend = BlendMode.One;
				shader.forward.dstBlend = BlendMode.Zero;
				shader.forward.zWrite = true;
				shader.forward_add.srcBlend = BlendMode.One;
				shader.forward_add.dstBlend = BlendMode.One;
				shader.forward_add.zWrite = false;
			} else if (mode == BlendTemplate.Fade || mode == BlendTemplate.FadeCutout) {
				q = "Transparent";
				shader.tags[KST.RenderType] = "Transparent";
				shader.tags[KFLTC.RenderType] = "Fade";
				shader.Define("_ALPHABLEND_ON 1");
				// Дополнительно CUTOFF_FADE
				shader.forward.srcBlend = BlendMode.SrcAlpha;
				shader.forward.dstBlend = BlendMode.OneMinusSrcAlpha;
				shader.forward.zWrite = false;
				shader.forward_add.srcBlend = BlendMode.SrcAlpha;
				shader.forward_add.dstBlend = BlendMode.One;
				shader.forward_add.zWrite = false;
			}
			shader.tags["Queue"] = string.Format("{0}{1:+#;-#;+0}", q, queueOffset);
			shader.shadowcaster.srcBlend = null;
			shader.shadowcaster.dstBlend = null;
			shader.shadowcaster.zWrite = null;
		}

		private void ConfigureFeatureMainTex(ShaderSetup shader) {
			var mainTex = this.mainTex;
			if (mode == BlendTemplate.Cutout && mainTex == MainTexKeywords.NoMainTex)
				mainTex = MainTexKeywords.NoMask;

			shader.TagEnum(KFLTC.F_MainTex, mainTex);

			switch (this.mainTex) {
				case MainTexKeywords.NoMainTex:
					shader.Define("MAINTEX_OFF 1");
					break;
				case MainTexKeywords.NoMask:
					shader.Define("MAINTEX_NOMASK 1");
					break;
				case MainTexKeywords.ColorMask:
					shader.Define("MAINTEX_COLORMASK 1");
					break;
			}

			if (this.mainTex == MainTexKeywords.ColorMask || this.mainTex == MainTexKeywords.NoMask) {
				shader.properties.Add(new Property2D() { name = "_MainTex" });
				if (this.mainTex == MainTexKeywords.ColorMask) {
					shader.properties.Add(new Property2D() { name = "_ColorMask", defualt = "black" });
				}
			}

			shader.properties.Add(new PropertyColor() { name = "_Color" });
		}

		private void ConfigureFeatureCutoff(ShaderSetup shader) {
			var forward_on = false;
			var forward_mode = CutoutMode.Classic;
			var shadow_on = false;
			var shadow_mode = CutoutMode.Classic;
			var cutoff_fade_flag = false;

			if (mode == BlendTemplate.Cutout) {
				forward_on = true;
				forward_mode = cutout;
				shadow_on = !forceNoShadowCasting;
				shadow_mode = cutout;

			} else if (mode == BlendTemplate.Fade) {
				forward_on = false;
				forward_mode = cutout;
				shadow_on = !forceNoShadowCasting;
				shadow_mode = cutout;

			} else if (mode == BlendTemplate.FadeCutout) {
				// Only classic
				forward_on = true;
				forward_mode = CutoutMode.Classic;
				shadow_on = !forceNoShadowCasting;
				shadow_mode = cutout;
				cutoff_fade_flag = true;
			}

			ConfigureFeatureCutoffPassDefines(shader.forward, forward_on, forward_mode, cutoff_fade_flag);
			ConfigureFeatureCutoffPassDefines(shader.forward_add, forward_on, forward_mode, cutoff_fade_flag);
			ConfigureFeatureCutoffPassDefines(shader.shadowcaster, shadow_on, shadow_mode, cutoff_fade_flag);

			var prop_classic = false;
			var prop_range = false;
			if (forward_on) {
				prop_classic |= forward_mode == CutoutMode.Classic;
				prop_range |= forward_mode == CutoutMode.RangeRandom;
				prop_range |= forward_mode == CutoutMode.RangeRandomH01;
				shader.TagEnum(KFLTC.F_Cutout_Forward, forward_mode);
			}
			if (shadow_on) {
				prop_classic |= shadow_mode == CutoutMode.Classic;
				prop_range |= shadow_mode == CutoutMode.RangeRandom;
				prop_range |= shadow_mode == CutoutMode.RangeRandomH01;
				shader.TagEnum(KFLTC.F_Cutout_ShadowCaster, shadow_mode);
			}

			shader.TagBool(KFLTC.F_Cutout_Classic, prop_classic);
			if (prop_classic) {
				shader.properties.Add(new PropertyFloat() { name = "_Cutoff", defualt = 0.5f, range = new Vector2(0, 1) });
			}
			shader.TagBool(KFLTC.F_Cutout_RangeRandom, prop_range);
			if (prop_range) {
				shader.properties.Add(new PropertyFloat() { name = "_CutoffMin", defualt = 0.4f, range = new Vector2(0, 1) });
				shader.properties.Add(new PropertyFloat() { name = "_CutoffMax", defualt = 0.6f, range = new Vector2(0, 1) });
			}
		}

		private void ConfigureFeatureCutoffPassDefines(PassSetup pass, bool is_on, CutoutMode mode, bool cutoff_fade_flag) {
			if (is_on) {
				pass.defines.Add("CUTOFF_ON 1");

				if (cutoff_fade_flag) {
					pass.defines.Add("CUTOFF_FADE 1");
				}

				if (mode == CutoutMode.Classic && !cutoff_fade_flag) {
					pass.defines.Add("CUTOFF_CLASSIC 1");
					// CUTOFF_FADE итак делает обрезку + коррекцию, вторая не к чему. 
				}
				if (mode == CutoutMode.RangeRandom || mode == CutoutMode.RangeRandomH01) {
					needRandomFrag = true;
					pass.defines.Add("CUTOFF_RANDOM 1");
					if (mode == CutoutMode.RangeRandomH01) {
						pass.defines.Add("CUTOFF_RANDOM_H01 1");
						// CUTOFF_RANDOM_H01 расширяет поведение CUTOFF_RANDOM, а не заменет
					}
				}
			} else {
				pass.defines.Add("CUTOFF_OFF 1");
			}
		}

		private void ConfigureFeatureEmission(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_Emission, emission);
			if (emission) {
				shader.forward.defines.Add("EMISSION_ON 1");
				shader.TagEnum(KFLTC.F_EmissionMode, emissionMode);
				switch (emissionMode) {
					case EmissionMode.AlbedoNoMask:
						shader.forward.defines.Add("EMISSION_ALBEDO_NOMASK 1");
						break;
					case EmissionMode.AlbedoMask:
						shader.forward.defines.Add("EMISSION_ALBEDO_MASK 1");
						break;
					case EmissionMode.Custom:
						shader.forward.defines.Add("EMISSION_CUSTOM 1");
						break;
				}
				if (emissionMode == EmissionMode.AlbedoMask) {
					shader.properties.Add(new Property2D() { name = "_EmissionMask", defualt = "white" });
				}
				if (emissionMode == EmissionMode.Custom) {
					shader.properties.Add(new Property2D() { name = "_EmissionMap", defualt = "white" });
				}
				shader.properties.Add(new PropertyColor() { name = "_EmissionColor", defualt = Color.black });
			} else {
				shader.forward.defines.Add("EMISSION_OFF 1");
			}
		}

		private void ConfigureFeatureNormalMap(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_NormalMap, bumpMap);
			if (bumpMap) {
				shader.forward.defines.Add("_NORMALMAP");
				shader.forward_add.defines.Add("_NORMALMAP");
				shader.properties.Add(new Property2D() { name = "_BumpMap", defualt = "bump", isNormal = true });
				shader.properties.Add(new PropertyFloat() { name = "_BumpScale", defualt = 1.0f });
			}
		}

		private void ConfigureFeatureShading(ShaderSetup shader) {
			shader.TagEnum(KFLTC.F_Shading, shading);
			switch (shading) {
				case ShadingMode.CubedParadoxFLT:
					shader.forward.defines.Add("SHADE_CUBEDPARADOXFLT 1");
					shader.forward_add.defines.Add("SHADE_CUBEDPARADOXFLT 1");
					ConfigureFeatureShadingCubedParadox(shader);
					break;
				case ShadingMode.KawashirovFLTSingle:
					shader.forward.defines.Add("SHADE_KAWAFLT_SINGLE 1");
					shader.forward_add.defines.Add("SHADE_KAWAFLT_SINGLE 1");
					ConfigureFeatureShadingKawashirovFLTSingle(shader);
					break;
				case ShadingMode.KawashirovFLTRamp:
					shader.forward.defines.Add("SHADE_KAWAFLT_RAMP 1");
					shader.forward_add.defines.Add("SHADE_KAWAFLT_RAMP 1");
					ConfigureFeatureShadingKawashirovFLTRamp(shader);
					break;
			}
		}

		private void ConfigureFeatureShadingCubedParadox(ShaderSetup shader) {
			shader.properties.Add(new PropertyFloat() { name = "_Sh_Cbdprdx_Shadow", defualt = 0.4f, range = new Vector2(0, 1) });
		}

		private void ConfigureFeatureShadingKawashirovFLTSingle(ShaderSetup shader) {
			shader.properties.Add(new PropertyFloat() { name = "_Sh_Kwshrv_ShdBlnd", defualt = 0.7f, range = new Vector2(0, 1), power = 2 });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_TngntLo", defualt = 0.7f, range = new Vector2(0, 1), power = 1.5f });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_TngntHi", defualt = 0.8f, range = new Vector2(0, 1), power = 1.5f });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_ShdLo", defualt = 0.4f, range = new Vector2(0, 1) });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_ShdHi", defualt = 0.9f, range = new Vector2(0, 1) });
		}

		private void ConfigureFeatureShadingKawashirovFLTRamp(ShaderSetup shader) {
			shader.properties.Add(new PropertyFloat() { name = "_Sh_Kwshrv_ShdBlnd", defualt = 0.7f, range = new Vector2(0, 1), power = 2 });
			shader.properties.Add(new Property2D() { name = "_Sh_KwshrvRmp_Tex", defualt = "gray" });
			shader.properties.Add(new PropertyColor() { name = "_Sh_KwshrvRmp_NdrctClr", defualt = Color.white });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_TngntLo", defualt = 0.7f, range = new Vector2(0, 1), power = 1.5f });
		}

		private void ConfigureFeatureMatcap(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_Matcap, matcap);
			if (matcap) {
				shader.Define("MATCAP_ON 1");
				needRandomFrag = true;
				shader.TagEnum(KFLTC.F_MatcapMode, matcapMode);
				switch (matcapMode) {
					case MatcapMode.Replace:
						shader.Define("MATCAP_REPLACE 1");
						break;
					case MatcapMode.Multiple:
						shader.Define("MATCAP_MULTIPLE 1");
						break;
					case MatcapMode.Add:
						shader.Define("MATCAP_ADD 1");
						break;
				}
				shader.properties.Add(new Property2D() { name = "_MatCap", defualt = "white" });
				shader.properties.Add(new PropertyFloat() { name = "_MatCap_Scale", defualt = 1f, range = new Vector2(0, 1) });
			} else {
				shader.Define("MATCAP_OFF 1");
			}
		}

		private void ConfigureFeatureDistanceFade(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_DistanceFade, distanceFade);
			if (distanceFade) {
				shader.Define("DSTFD_ON 1");
				needRandomFrag = true;
				shader.TagEnum(KFLTC.F_DistanceFadeMode, distanceFadeMode);
				switch (distanceFadeMode) {
					case DistanceFadeMode.Range:
						shader.Define("DSTFD_RANGE 1");
						break;
					case DistanceFadeMode.Infinity:
						shader.Define("DSTFD_INFINITY 1");
						break;
				}
				shader.properties.Add(new PropertyVector() { name = "_DstFd_Axis", defualt = Vector4.one });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_Near", defualt = 1.0f });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_Far", defualt = 2.0f });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_AdjustPower", defualt = 1.0f, range = new Vector2(0.1f, 10), power = 10 });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_AdjustScale", defualt = 1.0f, range = new Vector2(0.1f, 10) });
			} else {
				shader.Define("DSTFD_OFF 1");
			}
		}

		private void ConfigureFeatureWNoise(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_WNoise, wnoise);
			if (wnoise) {
				needRandomFrag = true;
				shader.Define("WNOISE_ON 1");
				shader.properties.Add(new PropertyFloat() { name = "_WNoise_Albedo", defualt = 1, range = new Vector2(0, 1) });
				shader.properties.Add(new PropertyFloat() { name = "_WNoise_Em", defualt = 1, range = new Vector2(0, 1) });
			} else {
				shader.Define("WNOISE_OFF 1");
			}
		}

		private void ConfigureFeatureFPS(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_FPS, FPS);
			if (FPS) {
				shader.TagEnum(KFLTC.F_FPSMode, FPSMode);
				switch (FPSMode) {
					case FPSMode.ColorTint:
						shader.Define("FPS_COLOR 1");
						break;
					case FPSMode.DigitsTexture:
						shader.Define("FPS_TEX 1");
						break;
					case FPSMode.DigitsMesh:
						shader.Define("FPS_MESH 1");
						break;
				}
				shader.properties.Add(new PropertyColor() { name = "_FPS_TLo", defualt = new Color(1, 0.5f, 0.5f, 1) });
				shader.properties.Add(new PropertyColor() { name = "_FPS_THi", defualt = new Color(0.5f, 1, 0.5f, 1) });
			} else {
				shader.Define("FPS_OFF 1");
			}
		}

		private void ConfigureFeatureOutline(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_Outline, outline);
			if (outline) {
				shader.Define("OUTLINE_ON 1");
				shader.TagEnum(KFLTC.F_OutlineMode, outlineMode);
				if (outlineMode == OutlineMode.Colored) {
					shader.Define("OUTLINE_COLORED 1");
				} else if (outlineMode == OutlineMode.Tinted) {
					shader.Define("OUTLINE_TINTED 1");
				}
				shader.properties.Add(new PropertyFloat() { name = "_outline_width", defualt = 0.2f, range = new Vector2(0, 1) });
				shader.properties.Add(new PropertyColor() { name = "_outline_color", defualt = new Color(0.5f, 0.5f, 0.5f, 1) });
				shader.properties.Add(new PropertyFloat() { name = "_outline_bias", defualt = 0, range = new Vector2(-1, 5) });
			} else {
				shader.Define("OUTLINE_OFF 1");
			}
		}

		private void ConfigureFeatureInfinityWarDecimation(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_IWD, iwd);
			if (iwd && (complexity == ShaderComplexity.VGF || complexity == ShaderComplexity.VHDGF)) {
				needRandomVert = true;
				shader.Define("IWD_ON 1");
				shader.properties.Add(new PropertyVector() { name = "_IWD_Plane", defualt = Vector4.zero });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_PlaneDistRandomness", defualt = 0, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_DirRandomWeight", defualt = 0.5f, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_DirPlaneWeight", defualt = 0.5f, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_DirNormalWeight", defualt = 0.5f, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_DirObjectWeight", defualt = 0.5f, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyVector() { name = "_IWD_DirObjectVector", defualt = Vector4.zero });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_DirWorldWeight", defualt = 0.5f, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyVector() { name = "_IWD_DirWorldVector", defualt = Vector4.zero });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_MoveSpeed", defualt = 1, range = new Vector2(0, 15), power = 5 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_MoveAccel", defualt = 1, range = new Vector2(0, 15), power = 5 });
				shader.properties.Add(new PropertyColor() { name = "_IWD_TintColor", defualt = new Color(0.2f, 0.2f, 0.2f, 0.1f) });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_TintFar", defualt = 0.5f, range = new Vector2(0, 10), power = 5 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_CmprssFar", defualt = 0.5f, range = new Vector2(0, 10), power = 5 });
				if (complexity == ShaderComplexity.VHDGF) {
					shader.properties.Add(new PropertyFloat() { name = "_IWD_Tsltn", defualt = 1, range = new Vector2(0, 10), power = 2 });
				}
			} else {
				shader.Define("IWD_OFF 1");
			}
		}

		private void ConfigureFeaturePolyColorWave(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_PCW, pcw);
			if (pcw) {
				needRandomVert = true;
				shader.Define("PCW_ON 1");
				shader.TagEnum(KFLTC.F_PCWMode, pcwMode);
				if (pcw) {
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmLo", defualt = 4 });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmAs", defualt = 0.25f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmHi", defualt = 0.5f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmDe", defualt = 0.25f });
					shader.properties.Add(new PropertyVector() { name = "_PCW_WvTmUV", defualt = new Vector4(0, 10, 0, 0) });
					shader.properties.Add(new PropertyVector() { name = "_PCW_WvTmVtx", defualt = new Vector4(0, 10, 0, 0) });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmRnd", defualt = 5 });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_Em", defualt = 0.5f, range = new Vector2(0, 1), power = 2 });
					shader.properties.Add(new PropertyColor() { name = "_PCW_Color", defualt = Color.white });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwTm", defualt = 0.5f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwTmRnd", defualt = 0.5f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwStrtn", defualt = 1, range = new Vector2(0, 1) });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwBrghtnss", defualt = 0.5f, range = new Vector2(0, 1) });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_Mix", defualt = 0.5f, range = new Vector2(0, 1) });
				}
			} else {
				shader.Define("PCW_OFF 1");
			}
		}

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
}
