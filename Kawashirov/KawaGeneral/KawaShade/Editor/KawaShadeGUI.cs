using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	internal partial class KawaShadeGUI : BakedShaderGUI<KawaShadeGenerator> {

		public override IEnumerable<string> GetShaderTagsOfIntrest() => KawaShadeCommons.tags.Value;

		public override void CustomBakedGUI() {
			EditorGUILayout.Space();
			OnGUI_BlendMode();

			EditorGUILayout.Space();
			OnGUI_Tessellation();

			EditorGUILayout.Space();
			OnGUI_Random();

			EditorGUILayout.Space();
			OnGUI_Textures();

			EditorGUILayout.Space();
			OnGUI_Shading();

			EditorGUILayout.Space();
			OnGUI_Outline();

			EditorGUILayout.Space();
			OnGUI_MatCap();

			EditorGUILayout.Space();
			OnGUI_DistanceFade();

			EditorGUILayout.Space();
			OnGUI_WNoise();

			EditorGUILayout.Space();
			OnGUI_Glitter();

			EditorGUILayout.Space();
			OnGUI_PenetrationSystem();

			EditorGUILayout.Space();
			OnGUI_FPS();

			EditorGUILayout.Space();
			OnGUI_PSX();

			EditorGUILayout.Space();
			OnGUI_InfinityWarDecimation();

			EditorGUILayout.Space();
			OnGUI_PolyColorWave();
		}
	}
}


// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/
// Тип не включен в неймспейс Kawashirov.KawaShade, т.к. эдитор указывается в файле .shader без указания неймспейса.

class KawaShadeGUI : Kawashirov.KawaShade.KawaShadeGUI { }
