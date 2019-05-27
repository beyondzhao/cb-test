using System;
using UnityEditor;
using UnityEngine;

namespace UcbEditorWindow
{
    public class UcbQrPopup : EditorWindow
    {
        public string QrString;

        public UcbQrPopup()
        {
            titleContent = new GUIContent("Cloud Build Download Apk");
        }

        private void OnEnable()
        {
            minSize = new Vector2(300, 350);
            maxSize = new Vector2(300, 350);
        }

        public static void Open(string qrString)
        {
            UcbQrPopup window = ScriptableObject.CreateInstance(typeof(UcbQrPopup)) as UcbQrPopup;
            window.QrString = qrString;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(QrString))
            {
                DrawQrCode(QrString);
                GUIStyle gUIStyle = new GUIStyle() { alignment = TextAnchor.MiddleCenter, richText = true };
                GUI.color = Color.white;
                EditorGUI.LabelField(new Rect(0, 270, position.width, 20), "<color=#aaaaaa>Scan QR code to download apk</color>", gUIStyle);
                EditorGUI.LabelField(new Rect(0, 290, position.width, 20), "<color=#aaaaaa>direct into your mobile devices</color>", gUIStyle);

            }

        }

        private void DrawQrCode(string content)
        {
            float margin = 50;
            float qrSize = Math.Min(position.width - margin * 2, 200);
            margin = Math.Max(margin, (position.width - qrSize) / 2);

            GUI.DrawTexture(new Rect(margin, margin, qrSize, qrSize), UcbUtils.QRHelper.generateQR(content), ScaleMode.ScaleToFit);
        }
    }
}
