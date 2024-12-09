
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using Kawashirov;
using Kawashirov.Refreshables;
using Kawashirov.Udon;

#if UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif

public class CommonUSharpBehaviour : UdonSharpBehaviour
#if !COMPILER_UDONSHARP
	, IRefreshable
#endif
{
	protected string logName = "";
	protected string path_ = "";

	public virtual void Start() {
		path_ = _GetPath(gameObject.transform);
	}

	protected void _Info(string msg) {
		Debug.Log($"[{logName}] {msg} @ {path_}", this);
	}

	protected void _Warning(string msg) {
		Debug.LogWarning($"[{logName}] {msg} @ {path_}", this);
	}

	protected void _Error(string msg) {
		Debug.LogError($"[{logName}] {msg} @ {path_}", this);
	}


	protected bool _EnsureValid(object obj, string msgWhenInvalid) {
		return _EnsureValid(obj, false, msgWhenInvalid);
	}

	protected bool _EnsureValid(object obj, bool disableSelf, string msgWhenInvalid) {
		var isValid = Utilities.IsValid(obj);
		if (!isValid) {
			if (disableSelf) {
				enabled = false;
				_Error(msgWhenInvalid);
			} else {
				_Warning(msgWhenInvalid);
			}
		}
		return isValid;
	}

	protected int _CountValid(object[] arr, string arrayName) {
		var count = 0;
		for (var i = 0; i < arr.Length; ++i) {
			var item = arr[i];
			if (Utilities.IsValid(item)) {
				++count;
			} else {
				_Error($"{arrayName}[{i}] is invalid!");
			}
		}
		return count;
	}

	protected int _EnsureCountValid(object[] arr, bool disableSelf, string arrayName) {
		// Ensure array itself valid, 
		// but elements might be invalid with error
		var count = 0;
		if (_EnsureValid(arr, disableSelf, $"{arrayName} is invalid!")) {
			count = _CountValid(arr, arrayName);
			_Info($"Bound to {count} {arrayName}.");
		}
		return count;
	}

	protected bool _EnsureAll(object[] arr, string arrayName) {
		return _EnsureAll(arr, false, 0, arrayName);
	}

	protected bool _EnsureAll(object[] arr, bool disableSelf, int minCount, string arrayName) {
		if (!Utilities.IsValid(arr)) {
			_Error($"Array \"{arrayName}\" is invalid!");
			if (disableSelf)
				enabled = false;
			return false;
		}
		if (arr.Length < minCount) {
			_Error($"Array \"{arrayName}\".Length < {minCount}!");
			if (disableSelf)
				enabled = false;
			return false;
		}

		var is_valid = true;
		for (var i = 0; i < arr.Length; ++i) {
			if (!Utilities.IsValid(arr[i])) {
				_Error($"Array element \"{arrayName}\"[{i}] is invalid!");
				is_valid = false;
			}
		}
		if (disableSelf && !is_valid)
			enabled = false;
		return is_valid;
	}


	protected bool _Ensure(bool cond, string msgWhenInvalid) {
		return _Ensure(cond, false, msgWhenInvalid);
	}

	protected bool _Ensure(bool cond, bool disableSelf, string msgWhenInvalid) {
		if (!cond) {
			_Error(msgWhenInvalid);
			if (disableSelf)
				enabled = false;
		}
		return cond;
	}


	protected string _GetPath(Transform t) {
		var path = t.name;
		while (Utilities.IsValid(t.parent)) {
			t = t.parent;
			path = $"{t.name}/{path}";
		}
		return path;
	}


	protected virtual string _PlayerToString_Tag(VRCPlayerApi player, string tags) {
		return tags;
	}

	protected virtual string _PlayerToString(VRCPlayerApi player) {
		if (!Utilities.IsValid(player))
			return "null";
		var tags = player.isLocal ? "local" : "remote";
		if (player.isMaster)
			tags = "master," + tags;
		tags = _PlayerToString_Tag(player, tags);
		return $"\"{player.displayName}\" ({tags}#{player.playerId})";
	}


#if UNITY_EDITOR && !COMPILER_UDONSHARP
	[CustomEditor(typeof(CommonUSharpBehaviour), true)]
	public class Editor : UnityEditor.Editor {
		public override void OnInspectorGUI() {
			if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
				return;
			DrawDefaultInspector();
			KawaGizmos.DrawEditorGizmosGUI();
			this.EditorRefreshableGUI();
		}
	}

	public virtual void Refresh() { }

	public Object AsUnityObject() => this;

	public string RefreshablePath() => gameObject.KawaGetFullPath();

#endif

}
