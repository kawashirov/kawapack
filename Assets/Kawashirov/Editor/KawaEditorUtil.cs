using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;


public class KawaEditorUtil {
	public static void ShaderEditorFooter() {
		GUIStyle style = new GUIStyle ();
		style.richText = true;
		
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("This shit made by <b>kawashirov ACT2</b>; My Contacts:", style);
		EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Discord server:");
			if (GUILayout.Button("pEugvST")) {
				Application.OpenURL("https://discord.gg/pEugvST");
			}
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.LabelField("Discord tag: kawashirov#8363");
		if (GUILayout.Button("Pls gimme money")) {
			Application.OpenURL("https://www.patreon.com/kawashirov");
		}
	}
}