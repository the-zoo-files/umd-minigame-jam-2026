using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UmdJam.Editor
{
    public static class BrowserBuildChecks
    {
        [MenuItem("Tools/UmdJam/Validate Browser Build Integration")]
        public static void Run()
        {
            string directory = Path.Combine(".utmp", "browser-build-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                string index = Path.Combine(directory, "index.html");
                File.WriteAllText(index, "<html><head></head><body><script>" +
                    "createUnityInstance(canvas, config, progress => {});</script></body></html>");
                BrowserBuildPostprocessor.ApplyResolutionBudget(directory);
                string first = File.ReadAllText(index);
                if (!first.Contains("UmdBrowserResolution.configure(canvas, config)") ||
                    !File.Exists(Path.Combine(directory, "browser-resolution.js")))
                    throw new InvalidOperationException("Web resolution policy was not integrated.");
                BrowserBuildPostprocessor.ApplyResolutionBudget(directory);
                if (first != File.ReadAllText(index))
                    throw new InvalidOperationException("Repeated build processing changed the output.");
                File.WriteAllText(index, "<html><head></head><body>Custom loader</body></html>");
                bool rejected = false;
                try { BrowserBuildPostprocessor.ApplyResolutionBudget(directory); }
                catch (BuildFailedException) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("Unsupported Web template silently accepted.");
                Debug.Log("Browser build integration checks: PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
