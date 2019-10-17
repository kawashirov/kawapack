using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public static class KawaVRCSetup {

	[MenuItem("Kawashirov/KawaShade/Setup Project Graphics Settings/For VRChat (Disable Deferred)")]
	static void BasicSetup() {
		GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredReflections, BuiltinShaderMode.Disabled);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredShading, BuiltinShaderMode.Disabled);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.LegacyDeferredLighting, BuiltinShaderMode.Disabled);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.DepthNormals, BuiltinShaderMode.Disabled);
	}

	[MenuItem("Kawashirov/KawaShade/Setup Project Graphics Settings/For Fast Shader Compilation (Disable Everything Agressivly)")]
	static void AgressiveSetup() {
		BasicSetup();
		GraphicsSettings.SetShaderMode(BuiltinShaderType.LensFlare, BuiltinShaderMode.Disabled);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.LightHalo, BuiltinShaderMode.Disabled);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.MotionVectors, BuiltinShaderMode.Disabled);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.ScreenSpaceShadows, BuiltinShaderMode.Disabled);
	}

	[MenuItem("Kawashirov/KawaShade/Setup Project Graphics Settings/Reset Unity Defaults (Built-in)")]
	static void UseBuiltinSetup() {
		GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredReflections, BuiltinShaderMode.UseBuiltin);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredShading, BuiltinShaderMode.UseBuiltin);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.LegacyDeferredLighting, BuiltinShaderMode.UseBuiltin);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.DepthNormals, BuiltinShaderMode.UseBuiltin);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.LensFlare, BuiltinShaderMode.UseBuiltin);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.LightHalo, BuiltinShaderMode.UseBuiltin);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.MotionVectors, BuiltinShaderMode.UseBuiltin);
		GraphicsSettings.SetShaderMode(BuiltinShaderType.ScreenSpaceShadows, BuiltinShaderMode.UseBuiltin);
	}

}
