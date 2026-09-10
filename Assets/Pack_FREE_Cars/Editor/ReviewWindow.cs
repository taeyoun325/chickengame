using UnityEditor;
using UnityEngine;
using System.IO;

namespace AssetReview.Editor
{
    public class ReviewWindow : EditorWindow
    {
        const string assetName = "FREE Cartoon Car Pack";

        public static void ShowNow()
        {
            var w = GetWindow<ReviewWindow>("Share Your Feedback!");
            var size = new Vector2(300, 380);
            w.minSize = size;
            w.maxSize = size;
        }

        Texture2D previewTex;

        void OnEnable()
        {
            previewTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Pack_FREE_Cars/Editor/preview.png"
            );
        }

        void OnGUI()
        {
            GUILayout.Space(30);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label(assetName, titleStyle, GUILayout.ExpandWidth(true));

            GUILayout.Space(5);

            if (previewTex != null)
            {
                var r = GUILayoutUtility.GetAspectRect((float)previewTex.width / previewTex.height);
                EditorGUI.DrawPreviewTexture(r, previewTex);
            }

            GUILayout.Space(5);

            GUILayout.Label("★★★★★", new GUIStyle()
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                normal = { textColor = Color.yellow }
            });

            GUILayout.Space(5);

            GUILayout.Label("If you found this asset useful, your review means a lot to us!",
                new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12
                });

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Leave a Review", GUILayout.Height(40), GUILayout.Width(140)))
            {
                Application.OpenURL("https://assetstore.unity.com/packages/3d/vehicles/land/free-cartoon-car-pack-simple-vehicles-282425#reviews");
                Close();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void OnDestroy()
        {
            DeleteEditorFolder();
        }

        static void DeleteEditorFolder()
        {
            string editorPath = "Assets/Pack_FREE_Cars/Editor";
            if (Directory.Exists(editorPath))
            {
                FileUtil.DeleteFileOrDirectory(editorPath);
                FileUtil.DeleteFileOrDirectory(editorPath + ".meta");
                AssetDatabase.Refresh();
            }
        }
    }
}
