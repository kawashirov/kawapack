#if UNITY_EDITOR
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Security.Cryptography;

internal class CreateNoizeTex : MonoBehaviour
{
	[MenuItem("GameObject/Create Noize Tex")]
	static void DoIt()
	{
		var rnd = new RNGCryptoServiceProvider();
			NoizeTex(8, rnd);
			NoizeTex(16, rnd);
			NoizeTex(32, rnd);
			NoizeTex(64, rnd);
			NoizeTex(128, rnd);
			NoizeTex(256, rnd);
		
	}
	
	static void NoizeTex(int size, RNGCryptoServiceProvider rnd)
	{
		NoizeTex(size, TextureFormat.R8, rnd, "R8");
		NoizeTex(size, TextureFormat.R16, rnd, "R16");
		NoizeTex(size, TextureFormat.BC4, rnd, "BC4");
		NoizeTex(size, TextureFormat.RHalf, rnd, "RHalf");
		NoizeTex(size, TextureFormat.RFloat, rnd, "RFloat");
	}
	
	
	static void NoizeTex(int size, TextureFormat format, RNGCryptoServiceProvider rnd, string name)
	{
		var tex = new Texture2D(size, size, format, false, true);
		Debug.Log(name + " support: " + SystemInfo.SupportsTextureFormat(format));
		
		var data = tex.GetRawTextureData();
		rnd.GetBytes(data);
		tex.LoadRawTextureData(data);
		
		string path = "Assets/Kawashirov/Additional/noise_" + size + "x" + size + "_" + name + ".asset";
		AssetDatabase.CreateAsset(tex, path);
		AssetDatabase.SetLabels(tex, new string[] {"Kawashirov", "Noise", size + "x" + size, name}); 
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