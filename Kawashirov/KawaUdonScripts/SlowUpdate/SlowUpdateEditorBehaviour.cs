using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Udon;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.Udon
{
    public class SlowUpdateEditorBehaviour : CommonUdonEditorBehaviour, KawaGizmos.IKawaGizmos
    {

        public static readonly string EventReceivers = "EventReceivers";
        public static readonly string SlowUpdateEventName = "SlowUpdateEventName";

        public AbstractUdonProgramSource[] AutoBindPrograms;

#if UNITY_EDITOR

        private Vector3[] _gizmos_recievers_ = new Vector3[0];
        private double _gizmos_next_update_ = -1.0f;

        [CustomEditor(typeof(SlowUpdateEditorBehaviour))]
        public new class Editor : CommonUdonEditorBehaviour.Editor
        {
            SerializedProperty AutoBindPrograms;

            public void OnEnable()
            {
                AutoBindPrograms = serializedObject.FindProperty("AutoBindPrograms");
            }

            public override void OnInspectorGUI()
            {
                EditorGUILayout.HelpBox(
                    "Binds all UdonBehaviours on scene with given Udon programs to this instance of SlowUpdate.\n" +
                    "Shows Gizmos lines from this object to recievers.", MessageType.Info
                );
                EditorGUILayout.PropertyField(AutoBindPrograms, true);

                OnInspectorRefreshGUI();

                EditorGUILayout.Space();

                KawaGizmos.DrawEditorGizmosGUI();

                serializedObject.ApplyModifiedProperties();
            }

            public override void OnInspectorRefreshGUI()
            {
                base.OnInspectorRefreshGUI();

            }
        }

        public override void Refresh()
        {
            var udon = GetSingleUdonBehaviour();

            var slow_update_rvent_name = udon.GetPubVarValue<string>(SlowUpdateEventName, null);
            if (string.IsNullOrWhiteSpace(slow_update_rvent_name))
            {
                Debug.LogErrorFormat(this,
                    "[Kawa|SlowUpdate] {1} does not have proper value for \"{2}\" ({3}). Is it configured properly? @ {0}",
                    kawaHierarchyPath, udon, SlowUpdateEventName, slow_update_rvent_name
                );
                return;
            }

            var programs = new HashSet<AbstractUdonProgramSource>(AutoBindPrograms);

            var udons = gameObject.scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<UdonBehaviour>(true))
                .Where(u => programs.Contains(u.programSource) && u.HasEntryPoint(slow_update_rvent_name) && !u.transform.IsEditorOnly())
                .Cast<Component>().ToArray();
            udon.SetPubVarValue(EventReceivers, udons);

            _gizmos_next_update_ = -1;
        }

        public void OnDrawGizmosSelected()
        {
            OnDrawGizmosUpdate();

            var self_pos = transform.position;

            Gizmos.color = Color.white.Alpha(KawaGizmos.GizmosAplha);
            Gizmos.DrawWireSphere(self_pos, 0.1f);

            Gizmos.color = Color.cyan.Alpha(KawaGizmos.GizmosAplha);
            foreach (var target_pos in _gizmos_recievers_)
                Gizmos.DrawLine(self_pos, target_pos);
        }

        public void OnDrawGizmosUpdate()
        {
            if (_gizmos_next_update_ > EditorApplication.timeSinceStartup) return;

            var udon = GetSingleUdonBehaviour(false);
            if (udon == null) return;

            var recievers = udon.GetPubVarValue<Component[]>(EventReceivers, null);
            if (recievers != null)
            {
                _gizmos_recievers_ = recievers.Select(c => c.transform.position).ToArray();
            }

            _gizmos_next_update_ = EditorApplication.timeSinceStartup + 5;
        }

#endif
    }
}
