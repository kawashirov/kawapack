using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov
{
    public abstract class EditorCommonBehaviour : MonoBehaviour
    {
#if UNITY_EDITOR

        private string _hierarchy_path = null;

        [CanEditMultipleObjects, CustomEditor(typeof(EditorCommonBehaviour), true)]
        public class Editor : CommonEditor {
            // TODO
        }

        public static HashSet<GameObject> FindWithTagInactive(IEnumerable<GameObject> where, string tag)
        {
            var queue = new Queue<GameObject>(where);
            var tagged = new HashSet<GameObject>();
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.CompareTag(tag))
                {
                    tagged.Add(current);
                }
                if (!current.CompareTag("EditorOnly"))
                {
                    var t = current.transform;
                    for (var i = 0; i < t.childCount; ++i)
                    {
                        queue.Enqueue(t.GetChild(i).gameObject);
                    }
                }
            }
            return tagged;
        }

        public string kawaHierarchyPath { 
            get {
                if (string.IsNullOrWhiteSpace(_hierarchy_path))
                    _hierarchy_path = transform.KawaGetHierarchyPath();
                return _hierarchy_path;
                // Possible bug: hierarchy change may not update path
            }
        }

#endif
    }
}