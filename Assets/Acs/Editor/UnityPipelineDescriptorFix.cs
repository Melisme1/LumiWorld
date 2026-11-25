using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.Acs.Editor
{
    /// <summary>
    /// Workaround for Unity 6000.5.1f1 / 6000.5.2f1 where com.unity.pipeline's
    /// PipelineDescriptorPublisher.OnCodeLoaded() fails because AssetDatabase.IsAssetImportWorkerProcess()
    /// is restricted during [OnCodeLoaded] (PreDeserialization), preventing Library/Pipeline/.unity-pipeline-port
    /// from being written even though the Pipeline HTTP server starts normally on port 7800 during [InitializeOnLoad].
    /// </summary>
    internal static class UnityPipelineDescriptorFix
    {
        private const string PublisherTypeName = "Unity.Pipeline.PipelineDescriptorPublisher, Unity.Pipeline";

        [InitializeOnLoadMethod]
        private static void EnsureDescriptorPublishedOnLoad()
        {
            TryEnsureDescriptorPublished();
            EditorApplication.delayCall += TryEnsureDescriptorPublished;
        }

        [MenuItem("Window/Pipeline/Repair Descriptor File (6000.5.1f1)", priority = 3055)]
        private static void MenuRepairDescriptor()
        {
            TryEnsureDescriptorPublished(logWhenAlreadyActive: true);
        }

        private static void TryEnsureDescriptorPublished()
        {
            TryEnsureDescriptorPublished(logWhenAlreadyActive: false);
        }

        private static void TryEnsureDescriptorPublished(bool logWhenAlreadyActive)
        {
            try
            {
                Type publisherType = Type.GetType(PublisherTypeName, throwOnError: false);
                if (publisherType == null)
                {
                    return;
                }

                FieldInfo fileField = publisherType.GetField("s_File", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo startMethod = publisherType.GetMethod("Start", BindingFlags.Static | BindingFlags.NonPublic);
                if (fileField == null || startMethod == null)
                {
                    return;
                }

                object currentFile = fileField.GetValue(null);
                if (currentFile != null)
                {
                    if (logWhenAlreadyActive)
                    {
                        Debug.Log("[UnityPipelineDescriptorFix] Pipeline descriptor publisher is already active.");
                    }
                    return;
                }

                startMethod.Invoke(null, null);

                if (fileField.GetValue(null) != null)
                {
                    Debug.Log("[UnityPipelineDescriptorFix] Published Library/Pipeline/.unity-pipeline-port on Unity " + Application.unityVersion);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[UnityPipelineDescriptorFix] Could not invoke PipelineDescriptorPublisher.Start(): " + ex.Message);
            }
        }
    }
}
