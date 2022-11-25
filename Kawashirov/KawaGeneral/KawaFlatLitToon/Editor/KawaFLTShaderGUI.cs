using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Kawashirov;
using Kawashirov.FLT;

using MP = UnityEditor.MaterialProperty;
using EGUIL = UnityEditor.EditorGUILayout;
using SC = Kawashirov.KawaUtilities;
using KFLTC = Kawashirov.FLT.Commons;
using static UnityEditor.EditorGUI;
using static Kawashirov.MaterialsCommons;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/
// Тип не включен в неймспейс Kawashirov.FLT, т.к. эдитор указывается в файле .shader без указания неймспейса.

internal partial class KawaFLTShaderGUI : Kawashirov.ShaderBaking.BakedShaderGUI<Generator> {

	public override IEnumerable<string> GetShaderTagsOfIntrest() => KFLTC.tags.Value;

	public override void CustomBakedGUI() {
		EGUIL.Space();
		OnGUI_BlendMode();

		EGUIL.Space();
		OnGUI_Tessellation();

		EGUIL.Space();
		OnGUI_Random();

		EGUIL.Space();
		OnGUI_Textures();

		EGUIL.Space();
		OnGUI_Shading();

		EGUIL.Space();
		OnGUI_Outline();

		EGUIL.Space();
		OnGUI_MatCap();

		EGUIL.Space();
		OnGUI_DistanceFade();

		EGUIL.Space();
		OnGUI_WNoise();

		EGUIL.Space();
		OnGUI_FPS();

		EGUIL.Space();
		OnGUI_PSX();

		EGUIL.Space();
		OnGUI_InfinityWarDecimation();

		EGUIL.Space();
		OnGUI_PolyColorWave();
	}
}
