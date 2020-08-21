using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class DistanceMetrics : MonoBehaviour
{

#if UNITY_EDITOR
	[CustomEditor(typeof(DistanceMetrics))]
	[CanEditMultipleObjects]
	public class DistanceMetricsEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			EditorGUILayout.Space();
			if (GUILayout.Button("Update Distance"))
			{
				foreach (var x in targets.Cast<DistanceMetrics>().Where(x => x != null))
					x.UpdateDistance();
			}
			if (GUILayout.Button("Update Setup"))
			{
				foreach (var x in targets.Cast<DistanceMetrics>().Where(x => x != null))
					x.UpdateSetup();
			}
			if (GUILayout.Button("Re-Render Frame")) {
				foreach (var x in targets.Cast<DistanceMetrics>().Where(x => x != null).Select(x => x.gameObject.GetComponent<Camera>()).Where(x => x != null))
					x.Render();
			}

			var target_safe = target as DistanceMetrics;
			if (target_safe != null)
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Distance", target_safe.distance.ToString());
				EditorGUILayout.LabelField("Samples Weights", target_safe.samplesWeights.ToString());

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Debug (read-only):");
				EditorGUILayout.ObjectField(nameof(target_safe.__cam), target_safe.__cam, typeof(UnityEngine.Object), true);
				EditorGUILayout.ObjectField(nameof(target_safe.__cmdShader), target_safe.__cmdShader, typeof(UnityEngine.Object), true);
				EditorGUILayout.ObjectField(nameof(target_safe.__cmdMaterial), target_safe.__cmdMaterial, typeof(UnityEngine.Object), true);
				EditorGUILayout.ObjectField(nameof(target_safe.__distanceRT), target_safe.__distanceRT, typeof(UnityEngine.Object), true);
				EditorGUILayout.ObjectField(nameof(target_safe.__reader), target_safe.__reader, typeof(UnityEngine.Object), true);
			}
		}
	}
#endif // UNITY_EDITOR

	private static void DestroyUni(UnityEngine.Object obj) {
		if (obj != null) {
#if UNITY_EDITOR
			DestroyImmediate(obj);
#else
			Destroy(obj);
#endif
		}
	}

	public bool autoUpdate = false;
	[Range(16, 256)] public int samplerSize = 64;
	public Texture2D samplerMask;
	[Range(0, 1)] public float focusAreaSize = 1f / 3f;
	public Material distanceRTDebug;

	[NonSerialized] public float distance;
	[NonSerialized] public float samplesWeights;

	[NonSerialized] public Camera __cam;
	[NonSerialized] public CommandBuffer __cmd;
	[NonSerialized] public Shader __cmdShader;
	[NonSerialized] public Material __cmdMaterial;
	[NonSerialized] public RenderTexture __distanceRT;
	[NonSerialized] public Texture2D __reader;

	private void Reset()
	{
		DestroyThings();

		__cmdShader = null;

		autoUpdate = false;
		samplerSize = 64;
		samplerMask = null;
		focusAreaSize = 1f / 3f;
		distanceRTDebug = null;

		UpdateSetup();
	}

	private void Start()
	{
		__cam = GetComponent<Camera>();
	}

	private void OnEnable()
	{
		UpdateSetup();
	}

	private void OnDisable()
	{
		if (__cmd != null)
			__cam.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, __cmd);
	}

	private void OnPostRender()
	{
		if (autoUpdate) UpdateDistance();
	}

	private void OnValidate()
	{
		UpdateSetup();
		UpdateDistance();
	}

#if UNITY_EDITOR
	private void OnDrawGizmos()
	{
		if (__cam)
		{
			var fov = __cam.fieldOfView;
			if (__cam.pixelHeight > __cam.pixelWidth)
				fov = fov / __cam.pixelHeight * __cam.pixelWidth;
			fov *= Mathf.Clamp01(focusAreaSize);
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = new Color(0, 0.75f, 0.75f, 1);
			Gizmos.DrawFrustum(Vector3.zero, fov, __cam.farClipPlane, __cam.nearClipPlane, 1);
		}
	}
#endif // UNITY_EDITOR

	private void OnDestroy() {
		DestroyThings();
	}

	private void DestroyThings() {
		if (__cmd != null) {
			if (__cam != null)
				__cam.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, __cmd);
			__cmd.Dispose();
			__cmd = null;
		}


		DestroyUni(__cmdMaterial);
		__cmdMaterial = null;

		DestroyUni(__distanceRT);
		__distanceRT = null;

		DestroyUni(__reader);
		__reader = null;
	}

	public void UpdateSetup()
	{
		__cam = GetComponent<Camera>();

		if (__cmdShader == null)
		{
			__cmdShader = Shader.Find("Kawashirov/CmdBuffer-DistanceMetrics");
			Shader.WarmupAllShaders();
		}

		if (__cmdMaterial == null)
			__cmdMaterial = new Material(__cmdShader);
		__cmdMaterial.name = "__" + GetType().Name + nameof(__cmdMaterial);
		__cmdMaterial.shader = __cmdShader;
		__cmdMaterial.mainTexture = null;
		__cmdMaterial.SetFloat("_FocusSize", focusAreaSize);
		__cmdMaterial.SetTexture("_Mask", samplerMask);

		if (__distanceRT == null || __distanceRT.width != samplerSize || __distanceRT.height != samplerSize)
		{
			DestroyUni(__distanceRT);
			__distanceRT = new RenderTexture(samplerSize, samplerSize, 0, RenderTextureFormat.ARGBFloat);
		}
		__distanceRT.name = "__" + GetType().Name + nameof(__distanceRT);
		__distanceRT.useDynamicScale = true;
		__distanceRT.filterMode = FilterMode.Bilinear;
		__distanceRT.wrapMode = TextureWrapMode.Clamp;
		__distanceRT.vrUsage = VRTextureUsage.None;

		if (__cmd != null) __cam.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, __cmd);
		__cmd = new CommandBuffer();
		__cmd.name = "__" + GetType().Name + nameof(__cmd);
		__cmd.Blit(BuiltinRenderTextureType.Depth, __distanceRT, __cmdMaterial);
		__cam.AddCommandBuffer(CameraEvent.AfterDepthTexture, __cmd);

		if (distanceRTDebug != null)
		{
			distanceRTDebug.shader = Shader.Find("Unlit/Transparent");
			distanceRTDebug.mainTexture = __distanceRT;
		}
	}

	public void UpdateDistance()
	{
		if (__reader == null || __reader.width != __distanceRT.width || __reader.height != __distanceRT.height)
		{
			DestroyUni(__reader);
			__reader = new Texture2D(samplerSize, samplerSize, TextureFormat.RGBAFloat, false);
			__reader.name = "__" + GetType().Name + nameof(__reader);
		}

		var prev_active = RenderTexture.active;
		try
		{
			RenderTexture.active = __distanceRT;
			__reader.ReadPixels(new Rect(0, 0, __distanceRT.width, __distanceRT.height), 0, 0, false);
			__reader.Apply();
		}
		finally
		{
			RenderTexture.active = prev_active;
		}

		samplesWeights = 0;
		double avg = 0;
		foreach (var color in __reader.GetPixels(0, 0, __reader.width, __reader.height))
		{
			samplesWeights += color.a;
			avg += color.r;
		}
		distance = (float) (avg / samplesWeights);

	}

}
