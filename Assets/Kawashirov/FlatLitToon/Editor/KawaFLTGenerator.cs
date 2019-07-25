using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Rendering;

namespace Kawashirov.FLT  
{
	[CreateAssetMenu(menuName = "Kawashirov Flat Lit Toon Shader Generator")]
	public class Generator : ScriptableObject {
		private static readonly string NAME_PATTERN = @"[a-zA-Z0-9_-]+";

		public string shaderName = "";
		public ShaderComplexity complexity = ShaderComplexity.VF;
		public BlendTemplate mode = BlendTemplate.Opaque;
		public CullMode cull = CullMode.Back;
		public bool instancing = true;
		public int queueOffset = 0;
		public bool disableBatching = false;
		public bool forceNoShadowCasting = false;
		public bool ignoreProjector = true;
		public bool zWrite = true;
		public bool debug = false;

		public MainTexKeywords mainTex = MainTexKeywords.ColorMask;
		public EmissionMode emission = EmissionMode.Custom;
		public CutoutMode cutout = CutoutMode.Classic;

		public ShadingMode shading = ShadingMode.KawashirovFLTSingle;

		public DistanceFadeMode distanceFadeMode = DistanceFadeMode.None;
		public DistanceFadeRandom distanceFadeRandom = DistanceFadeRandom.PerPixel;

		public FPSMode FPS = FPSMode.None;

		public OutlineMode outline = OutlineMode.None;

		public DisintegrationMode disintegrationMode = DisintegrationMode.None;

		public PolyColorWaveMode polyColorWaveMode = PolyColorWaveMode.None;

		public Shader result = null;

		[System.NonSerialized] private Dictionary<string, string> _tags_subshader = new Dictionary<string, string>();
		[System.NonSerialized] private Dictionary<string, string> _tags_forward = new Dictionary<string, string>();
		[System.NonSerialized] private Dictionary<string, string> _tags_forward_add = new Dictionary<string, string>();
		[System.NonSerialized] private Dictionary<string, string> _tags_shadowcaster = new Dictionary<string, string>();

		[System.NonSerialized] private PassSetup _setup_forward = new PassSetup();
		[System.NonSerialized] private PassSetup _setup_forward_add = new PassSetup();
		[System.NonSerialized] private PassSetup _setup_shadowcaster = new PassSetup();
		[System.NonSerialized] private StringBuilder _code = new StringBuilder();

		[System.NonSerialized] private CGProgram _cg_forward = new CGProgram();
		[System.NonSerialized] private CGProgram _cg_forward_add = new CGProgram();
		[System.NonSerialized] private CGProgram _cg_shadowcaster = new CGProgram();

		private void ResetInternals() {
			this._tags_subshader.Clear();
			this._tags_forward.Clear();
			this._tags_forward_add.Clear();
			this._tags_shadowcaster.Clear();
			this._setup_forward.Clear();
			this._setup_forward_add.Clear();
			this._setup_shadowcaster.Clear();
		}

		public void Generate()
		{
			var assets = AssetDatabase.FindAssets("KawaFLT.shader");
			if (assets.Length != 1) {
				EditorUtility.DisplayDialog(
					"Asset not found",
					"Can not find KawaFLT.shader.txt: \n" + string.Join(",\n", assets),
					"OK"
				);
				return;
			}
			var shader_template_guid = assets[0];
			var shader_template_path = AssetDatabase.GUIDToAssetPath(shader_template_guid);
			//EditorUtility.DisplayDialog("shader_template_path", shader_template_path, "OK");
			var shader_template = AssetDatabase.LoadAssetAtPath(shader_template_path, typeof(TextAsset)) as TextAsset;
			// EditorUtility.DisplayDialog("TextAsset", shader_template.text, "OK");
			var shader_cginc_path = System.IO.Path.GetDirectoryName(shader_template_path);
			//EditorUtility.DisplayDialog("shader_cginc_path", shader_cginc_path, "OK");


			this.shaderName = this.shaderName.Trim();
			if (string.IsNullOrEmpty(this.shaderName)) {
				EditorUtility.DisplayDialog("Invalid Shader Name", "Shader Name is Empty!", "OK");
				return;
			}
			if (!Regex.Match(this.shaderName, NAME_PATTERN).Success) {
				EditorUtility.DisplayDialog("Invalid Shader Name", "Shader Name should match " + NAME_PATTERN, "OK");
				return;
			}

			var shader_generated_path = string.Format("{0}/generated_{1}.shader", shader_cginc_path, this.shaderName);

			this._code = new StringBuilder(shader_template.text);

			this.ResetInternals();
			this.ConfigureCode();
			this.BakeCode();

			using (var writer = new StreamWriter(shader_generated_path)) {
				writer.Write(this._code.ToString());
				writer.Flush();
			}
			AssetDatabase.ImportAsset(shader_generated_path);
			this.result = AssetDatabase.LoadAssetAtPath<Shader>(shader_generated_path);

			this.ResetInternals();
		}

		private void ConfigureTagsSubshaders()
		{
			switch (this.complexity) {
				case ShaderComplexity.VHDGF:
					this._tags_subshader["KawaFLT_Features"] = "Geometry,Tessellation";
					this._tags_subshader["KawaFLT_Feature_Geometry"] = "True";
					this._tags_subshader["KawaFLT_Feature_Tessellation"] = "True";
					break;
				case ShaderComplexity.VGF:
					this._tags_subshader["KawaFLT_Features"] = "Geometry";
					this._tags_subshader["KawaFLT_Feature_Geometry"] = "True";
					this._tags_subshader["KawaFLT_Feature_Tessellation"] = "False";
					break;
				default:
					this._tags_subshader["KawaFLT_Features"] = "";
					this._tags_subshader["KawaFLT_Feature_Geometry"] = "False";
					this._tags_subshader["KawaFLT_Feature_Tessellation"] = "False";
					break;
			}

			this._tags_subshader["KawaFLT_Feature_Maintex"] = Enum.GetName(typeof(MainTexKeywords), this.mainTex);
			this._tags_subshader["KawaFLT_Feature_Emission"] = Enum.GetName(typeof(EmissionMode), this.emission);
			this._tags_subshader["KawaFLT_Feature_Cutout"] = Enum.GetName(typeof(CutoutMode), this.cutout);
		}

		private void ConfigureCode()
		{
			switch (this.complexity) {
				case ShaderComplexity.VHDGF:
					this._cg_forward.includes.Add("KawaFLT_Struct_VHDGF.cginc");
					this._cg_forward.includes.Add("KawaFLT_PreFrag_VHDGF.cginc");
					this._cg_forward_add.includes.Add("KawaFLT_Struct_VHDGF.cginc");
					this._cg_forward_add.includes.Add("KawaFLT_PreFrag_VHDGF.cginc");
					this._cg_shadowcaster.includes.Add("KawaFLT_Struct_VHDGF.cginc");
					this._cg_shadowcaster.includes.Add("KawaFLT_PreFrag_VHDGF.cginc");
					break;
				case ShaderComplexity.VGF:
					this._cg_forward.includes.Add("KawaFLT_Struct_VGF.cginc");
					this._cg_forward.includes.Add("KawaFLT_PreFrag_VGF.cginc");
					this._cg_forward_add.includes.Add("KawaFLT_Struct_VGF.cginc");
					this._cg_forward_add.includes.Add("KawaFLT_PreFrag_VGF.cginc");
					this._cg_shadowcaster.includes.Add("KawaFLT_Struct_VGF.cginc");
					this._cg_shadowcaster.includes.Add("KawaFLT_PreFrag_VGF.cginc");
					break;
				default:
					this._cg_forward.includes.Add("KawaFLT_Struct_VF.cginc");
					this._cg_forward.includes.Add("KawaFLT_PreFrag_VF.cginc");
					this._cg_forward_add.includes.Add("KawaFLT_Struct_VF.cginc");
					this._cg_forward_add.includes.Add("KawaFLT_PreFrag_VF.cginc");
					this._cg_shadowcaster.includes.Add("KawaFLT_Struct_VF.cginc");
					this._cg_shadowcaster.includes.Add("KawaFLT_PreFrag_VF.cginc");
					break;
			}

			this._tags_subshader["IgnoreProjector"] = this.ignoreProjector ? "True" : "False";
			this._tags_subshader["ForceNoShadowCasting"] = this.forceNoShadowCasting ? "True" : "False";
			this._tags_subshader["DisableBatching"] = this.disableBatching ? "True" : "False";

			this._setup_forward.zWrite = this.zWrite;
			this._setup_forward_add.zWrite = false;

			this._setup_forward.cullMode = this.cull;
			this._cg_forward.SkipCommonStaticVariants();
			this._cg_forward.defines.Add("KAWAFLT_PASS_FORWARDBASE 1");
			this._cg_forward.includes.Add("KawaFLT_Frag_ForwardBase.cginc");
			this._cg_forward.multi_compile_instancing = this.complexity != ShaderComplexity.VHDGF && this.instancing;
			this._cg_forward.multi_compile_fwdbase = true;
			this._cg_forward.multi_compile_fwdadd_fullshadows = false;
			this._cg_forward.multi_compile_shadowcaster = false;
			this._cg_forward.enable_d3d11_debug_symbols = this.debug;

			this._setup_forward_add.cullMode = this.cull;
			this._cg_forward_add.SkipCommonStaticVariants();
			this._cg_forward_add.defines.Add("KAWAFLT_PASS_FORWARDADD 1");
			this._cg_forward_add.includes.Add("KawaFLT_Frag_ForwardAdd.cginc");
			this._cg_forward_add.multi_compile_instancing = this.complexity != ShaderComplexity.VHDGF && this.instancing;
			this._cg_forward_add.multi_compile_fwdbase = false;
			this._cg_forward_add.multi_compile_fwdadd_fullshadows = true;
			this._cg_forward_add.multi_compile_shadowcaster = false;
			this._cg_forward_add.enable_d3d11_debug_symbols = this.debug;

			this._setup_shadowcaster.cullMode = this.cull;
			this._cg_shadowcaster.SkipCommonStaticVariants();
			this._cg_shadowcaster.defines.Add("KAWAFLT_PASS_SHADOWCASTER 1");
			this._cg_shadowcaster.includes.Add("KawaFLT_Frag_ShadowCaster.cginc");
			this._cg_shadowcaster.multi_compile_instancing = this.complexity != ShaderComplexity.VHDGF && this.instancing;
			this._cg_shadowcaster.multi_compile_fwdbase = false;
			this._cg_shadowcaster.multi_compile_fwdadd_fullshadows = false;
			this._cg_shadowcaster.multi_compile_shadowcaster = true;
			this._cg_shadowcaster.enable_d3d11_debug_symbols = this.debug;

			string q = null;
			switch (this.mode) {
				case BlendTemplate.Opaque:
					q = "Geometry";
					this._tags_subshader["RenderType"] = "Opaque";
					this._tags_subshader["KawaFLT_RenderType"] = "Opaque";
					this._setup_forward.srcBlend = BlendMode.One;
					this._setup_forward.srcBlend = BlendMode.Zero;
					this._setup_forward_add.srcBlend = BlendMode.One;
					this._setup_forward_add.srcBlend = BlendMode.One;
					break;
				case BlendTemplate.Cutout:
					q = "AlphaTest";
					this._tags_subshader["RenderType"] = "TransparentCutout";
					this._tags_subshader["KawaFLT_RenderType"] = "Cutout";
					this._cg_forward.defines.Add("_ALPHATEST_ON 1");
					this._cg_forward_add.defines.Add("_ALPHATEST_ON 1");
					this._cg_shadowcaster.defines.Add("_ALPHATEST_ON 1");
					this._setup_forward.srcBlend = BlendMode.One;
					this._setup_forward.srcBlend = BlendMode.Zero;
					this._setup_forward_add.srcBlend = BlendMode.One;
					this._setup_forward_add.srcBlend = BlendMode.One;
					break;
				case BlendTemplate.Fade:
					q = "Transparent";
					this._tags_subshader["RenderType"] = "Transparent";
					this._tags_subshader["KawaFLT_RenderType"] = "Fade";
					this._cg_forward.defines.Add("_ALPHABLEND_ON 1");
					this._cg_forward_add.defines.Add("_ALPHABLEND_ON 1");
					this._cg_shadowcaster.defines.Add("_ALPHABLEND_ON 1");
					this._setup_forward.srcBlend = BlendMode.SrcAlpha;
					this._setup_forward.srcBlend = BlendMode.OneMinusSrcAlpha;
					this._setup_forward_add.srcBlend = BlendMode.SrcAlpha;
					this._setup_forward_add.srcBlend = BlendMode.One;
					break;
			}
			this._tags_subshader["Queue"] = string.Format("{0}{1:+#;-#;+0}", q, this.queueOffset);
			this._setup_shadowcaster.srcBlend = null;
			this._setup_shadowcaster.dstBlend = null;
		}

		private void BakeCode()
		{
			var shader_name = string.Format("Kawashirov/Flat Lit Toon/{0}", this.shaderName);
			this._code.Replace("{{SHADER_NAME}}", shader_name);

			this._code.Replace("{{SUBSHADER_TAGS}}", this._tags_subshader.BakeTags());

			this._code.Replace("{{PASS_TAGS_ForwardBase}}", this._tags_forward.BakeTags());
			this._code.Replace("{{PASS_TAGS_ForwardAdd}}", this._tags_forward_add.BakeTags());
			this._code.Replace("{{PASS_TAGS_ShadowCaster}}", this._tags_shadowcaster.BakeTags());

			this._code.Replace("{{PASS_SETUP_ForwardBase}}", this._setup_forward.Bake());
			this._code.Replace("{{PASS_SETUP_ForwardAdd}}", this._setup_forward_add.Bake());
			this._code.Replace("{{PASS_SETUP_ShadowCaster}}", this._setup_shadowcaster.Bake());

			this._code.Replace("{{CGPROGRAM_ForwardBase}}", this._cg_forward.Bake());
			this._code.Replace("{{CGPROGRAM_ForwardAdd}}", this._cg_forward_add.Bake());
			this._code.Replace("{{CGPROGRAM_ShadowCaster}}", this._cg_shadowcaster.Bake());
		}
	}
}