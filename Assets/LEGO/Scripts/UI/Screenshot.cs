using System.Collections;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace Unity.LEGO.UI
{
    // A script that allows the user to take screenshot.
    // This can only be used while playing in the Editor.

    [RequireComponent(typeof(OptionsMenuManager))]
    public class Screenshot : MonoBehaviour
    {
        [Header("References")]

        [SerializeField, Tooltip("The canvas that contains the menu.\nThis is used to hide the menu while taking the screenshot.")]
        CanvasGroup m_MenuCanvas = default;

        [SerializeField, Tooltip("The game object that contains and masks the screenshot preview.")]
        GameObject m_PreviewMask = default;

        [SerializeField, Tooltip("The image used for displaying the screenshot preview.")]
        RawImage m_PreviewImage = default;

        [Header("Screenshot")]

        [SerializeField, Tooltip("The name for the screenshot file.")]
        string m_FileName = "Screenshot";

        Texture2D m_Texture;

        bool m_FeatureDisabled = false;

        const string k_ScreenshotPath = "Assets/";

        string GetPath() => k_ScreenshotPath + m_FileName + ".png";

        public void Take()
        {
            if (!m_FeatureDisabled)
            {
                StartCoroutine(DoTake());
            }
        }

        IEnumerator DoTake()
        {
            m_MenuCanvas.alpha = 0;
            ScreenCapture.CaptureScreenshot(GetPath());

            yield return null;

            m_MenuCanvas.alpha = 1;
            LoadScreenshot();

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        void Awake()
        {
#if !UNITY_EDITOR
            // This feature is available only in the editor.
            m_FeatureDisabled = true;
            m_PreviewMask.SetActive(false);
#else
            LoadScreenshot();
#endif
        }

        void LoadScreenshot()
        {
            if (File.Exists(GetPath()))
            {
                var bytes = File.ReadAllBytes(GetPath());

                m_Texture = new Texture2D(2, 2);
                m_Texture.LoadImage(bytes);
                m_Texture.Apply();

                m_PreviewImage.texture = m_Texture;

                // Resize the preview to match the screenshot aspect ratio.
                Vector2 size = new Vector2(m_PreviewImage.rectTransform.sizeDelta.x, m_PreviewImage.rectTransform.sizeDelta.x * m_Texture.height / m_Texture.width);
                m_PreviewImage.rectTransform.sizeDelta = size;

                m_PreviewMask.SetActive(true);
            }
            else
            {
                m_PreviewMask.SetActive(false);
            }
        }
    }
}
