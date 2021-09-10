using UdonSharp;
using UnityEngine;

public class AnimatorParametersSave : UdonSharpBehaviour {
	private Animator animator;
	private string[] p_keys;
	private float[] p_values;
	private string _path = "";

	public void Start() {
		_path = GetPath(transform);

		animator = gameObject.GetComponent<Animator>();
		if (animator == null) {
			Debug.LogErrorFormat(gameObject, "[Kawa|AnimatorParametersSave] There is no any Animators! @ {0}", _path);
			gameObject.SetActive(false);
		}
	}

	public void OnDisable() {
		_path = GetPath(transform);

		var parameters = animator.parameters; // getter
		var parameters_l = parameters.Length; // getter

		var new_values = new float[parameters_l];
		var new_keys = new string[parameters_l];

		for (var i = 0; i < parameters_l; ++i) {
			var p = parameters[i];
			var p_name = p.name;
			var p_type = p.type;
			new_keys[i] = p_name;

			if (p_type == AnimatorControllerParameterType.Bool) {
				new_values[i] = animator.GetBool(p_name) ? 0.0f : 1.0f;
			} else if (p_type == AnimatorControllerParameterType.Int) {
				new_values[i] = animator.GetInteger(p_name);
			} else if (p_type == AnimatorControllerParameterType.Float) {
				new_values[i] = animator.GetFloat(p_name);
			}
			Debug.LogFormat(gameObject, "[Kawa|AnimatorParametersSave] Saving {1}={2}... @ {0}", _path, p_name, new_values[i]);
		}

		p_values = new_values;
		p_keys = new_keys;
	}

	public void OnEnable() {
		var parameters = animator.parameters; // getter
		var l_parameters = parameters.Length; // getter

		var l_values = p_values.Length;

		for (var i = 0; i < l_values; ++i) {
			var p_name = p_keys[i];
			var p_value = p_values[i];
			var p_type = AnimatorControllerParameterType.Bool;

			for (var j = 0; j < l_parameters; ++j) {
				var p = parameters[j];
				if (p.name.Equals(p_name)) {
					p_type = p.type;
					break;
				}
			}

			Debug.LogFormat(gameObject, "[Kawa|AnimatorParametersSave] Loading {1}={2}... @ {0}", _path, p_name, p_value);
			if (p_type == AnimatorControllerParameterType.Bool) {
				animator.SetBool(p_name, p_value != 0.0f);
			} else if (p_type == AnimatorControllerParameterType.Int) {
				animator.SetInteger(p_name, (int)p_value);
			} else if (p_type == AnimatorControllerParameterType.Float) {
				animator.SetFloat(p_name, p_value);
			}
		}
	}

	/* Utils */

	private string GetPath(Transform t) {
		var path = t.name;
		while (t.parent != null) {
			t = t.parent;
			path = t.name + "/" + path;
		}
		return path;
	}

}
