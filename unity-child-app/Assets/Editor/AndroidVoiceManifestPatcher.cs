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
    /// It also adds the com.google.ar.core meta-data (optional) so Vuforia can use ARCore when present.
    /// </summary>
    public class AndroidVoiceManifestPatcher : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string ServiceAction = "android.speech.RecognitionService";
        private const string ArCoreMeta = "com.google.ar.core";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            XmlElement manifest = doc.DocumentElement;
            if (manifest == null) return;
            bool changed = false;

            if (!doc.OuterXml.Contains(ServiceAction))
            {
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
                changed = true;
            }

            // "optional" so tablets without ARCore still install.
            XmlElement application = manifest["application"];
            if (application != null && !doc.OuterXml.Contains("\"" + ArCoreMeta + "\""))
            {
                XmlElement meta = doc.CreateElement("meta-data");
                meta.SetAttribute("name", AndroidNs, ArCoreMeta);
                meta.SetAttribute("value", AndroidNs, "optional");
                application.AppendChild(meta);
                changed = true;
            }

            if (changed) doc.Save(manifestPath);
        }
    }
}
#endif
