#if UNITY_EDITOR
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class CreateNoizeTex : MonoBehaviour
{
	[MenuItem("GameObject/Create Noize Tex")]
	static void DoIt()
	{
		NoizeTex(16);
		NoizeTex(32);
		NoizeTex(64);
		NoizeTex(128);
		NoizeTex(256);
		
		/*
		BayerTex(2);
		BayerTex(4);
		BayerTex(8);
		BayerTex(16);
		BayerTex(32);
		BayerTex(64);
		BayerTex(128);
		*/
	}
	
	static void NoizeTex(int size)
	{
		var rnd = new System.Random();
		var tex_DXT5 = new Texture2D(size, size, TextureFormat.ARGB32, false, true);
		var tex_ARGB32 = new Texture2D(size, size, TextureFormat.ARGB32, false, true);
		var tex_R8 = new Texture2D(size, size, TextureFormat.R8, false, true);
		tex_DXT5.filterMode = FilterMode.Point;
		tex_ARGB32.filterMode = FilterMode.Point;
		tex_R8.filterMode = FilterMode.Point;
		
		var colors = new Color32[size * size];
		for(var i = 0; i < size*size; ++i) {
			var r = (byte) rnd.Next(256);
			var g = (byte) rnd.Next(256);
			var b = (byte) rnd.Next(256);
			var a = (byte) rnd.Next(256);
			colors[i] = new Color32(r,g,b,a);
		}
		tex_DXT5.SetPixels32(colors);
		tex_ARGB32.SetPixels32(colors);
		
		var data_R8 = tex_R8.GetRawTextureData();
		rnd.NextBytes(data_R8);
		tex_R8.LoadRawTextureData(data_R8);
		
		EditorUtility.CompressTexture(tex_DXT5, TextureFormat.DXT5, TextureCompressionQuality.Best);
		EditorUtility.CompressTexture(tex_ARGB32, TextureFormat.ARGB32, TextureCompressionQuality.Best);
		//EditorUtility.CompressTexture(tex_R8, TextureFormat.R8, TextureCompressionQuality.Best);
		
		tex_DXT5.Apply(true, true);
		tex_ARGB32.Apply(true, true);
		tex_R8.Apply(true, true);
		
		string path_DXT5 = "Assets/noise" + size + "x" + size + "DXT5.asset";
		string path_ARGB32 = "Assets/noise" + size + "x" + size + "ARGB32.asset";
		string path_R8 = "Assets/noise" + size + "x" + size + "R8.asset";
		
		AssetDatabase.CreateAsset(tex_DXT5, path_DXT5);
		AssetDatabase.CreateAsset(tex_ARGB32, path_ARGB32);
		AssetDatabase.CreateAsset(tex_R8, path_R8);
	}
	
	
	static uint[,] BayerMatrix(int size) {
		var base_m = new uint[,] {{ 0, 2 }, { 3, 1 }};
		if (size == 2)
			return base_m;
		var ps = size/2;
		var pre = BayerMatrix(ps);
		
		var m = new uint[size,size];
		for(var px = 0; px < ps; ++px) {
			for(var py = 0; py < ps; ++py) {
				var p4 = pre[px, py] * 4;
				m[px, py] = p4 + base_m[0,0];
				m[px+ps, py] = p4 + base_m[1,0];
				m[px, py+ps] = p4 + base_m[0,1];
				m[px+ps, py+ps] = p4 + base_m[1,1];
			}
		}
		/*
		var sb = new StringBuilder();
		for(var x = 0; x < size; ++x) {
			for(var y = 0; y < size; ++y) {
				sb.Append(m[x,y] + " ");
			}
			sb.Append("\n");
		}
		Debug.Log(sb.ToString());
		*/
		return m;
	}
	
	static void BayerTex(int size)
	{
		var rnd = new System.Random();
		var tex = new Texture2D(size, size, TextureFormat.RFloat, false, true);
		tex.filterMode = FilterMode.Point;
		var bayer = BayerMatrix(size);
		for(var i = 0; i < size; ++i) {
			for(var j = 0; j < size; ++j) {
				var c = (float) (1.0 * bayer[i,j] / (size * size));
				tex.SetPixel(i, j, new Color(c,c,c,c));
			}
		}
		//EditorUtility.CompressTexture(tex, TextureFormat.RFloat, TextureCompressionQuality.Best);
		tex.Apply(true, true);
		string path = "Assets/bayer" + size + "x" + size + ".asset";
		AssetDatabase.CreateAsset(tex, path); 
		EditorGUIUtility.PingObject(tex);
	}
}
#endif