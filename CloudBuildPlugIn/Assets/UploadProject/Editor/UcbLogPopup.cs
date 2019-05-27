using System;
using UnityEditor;
using UnityEngine;

namespace UcbEditorWindow
{
    public class UcbLogPopup : EditorWindow
    {
        public string editorLog, exectionLog;

        public UcbLogPopup()
        {
            titleContent = new GUIContent("Cloud Build Log");
        }

        private void OnEnable()
        {
            minSize = new Vector2(300, 350);
            maxSize = new Vector2(300, 350);
        }

        public static void Open(string edLog, string exLog)
        {
            UcbLogPopup window = ScriptableObject.CreateInstance(typeof(UcbLogPopup)) as UcbLogPopup;
            window.editorLog = edLog;
            window.exectionLog = exLog;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(editorLog))
            {
                EditorGUILayout.LabelField("EditorLog", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(editorLog);
            }
            if (!string.IsNullOrEmpty(exectionLog))
            {
                EditorGUILayout.LabelField("ExcutionLog", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(exectionLog);
            }
        }

    }
}
