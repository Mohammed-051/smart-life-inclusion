#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace SmartLife.EditorTools
{
    /// <summary>
    /// Android 11+ hides speech services from apps unless the manifest declares them. Without this,
    /// SpeechRecognizer.isRecognitionAvailable returns false on the tablet. Compiled only when the
    /// Android build target is active, since UnityEditor.Android needs that module.
    /// </summary>
    public class AndroidVoiceManifestPatcher : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string ServiceAction = "android.speech.RecognitionService";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            XmlElement manifest = doc.DocumentElement;
            if (manifest == null || doc.OuterXml.Contains(ServiceAction)) return;

            XmlElement queries = doc.CreateElement("queries");
            XmlElement intent = doc.CreateElement("intent");
            XmlElement action = doc.CreateElement("action");
            action.SetAttribute("name", AndroidNs, ServiceAction);
            intent.AppendChild(action);
            queries.AppendChild(intent);
            manifest.AppendChild(queries);

            XmlElement permission = doc.CreateElement("uses-permission");
            permission.SetAttribute("name", AndroidNs, "android.permission.RECORD_AUDIO");
            manifest.AppendChild(permission);

            doc.Save(manifestPath);
        }
    }
}
#endif
