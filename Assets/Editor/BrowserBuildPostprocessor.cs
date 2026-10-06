using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace UmdJam.Editor
{
    public sealed class BrowserBuildPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            ApplyResolutionBudget(report.summary.outputPath);
        }

        public static void ApplyResolutionBudget(string outputPath)
        {
            string indexPath = Path.Combine(outputPath, "index.html");
            string html = File.ReadAllText(indexPath);
            if (html.Contains("UmdBrowserResolution.configure")) return;
            // Unity's standard template names these arguments canvas/config. Fail visibly if a
            // custom template needs integration instead of silently shipping unlimited resolution.
            Regex loader = new(@"createUnityInstance\(\s*canvas\s*,\s*config\s*,");
            if (loader.Matches(html).Count != 1 || !html.Contains("</head>"))
                throw new BuildFailedException("Web template must call createUnityInstance(canvas, config, ...). " +
                    "Integrate browser-resolution.js when using a custom template.");
            html = loader.Replace(html, "createUnityInstance(canvas, UmdBrowserResolution.configure(canvas, config),", 1);
            html = html.Replace("</head>", "<script src=\"browser-resolution.js\"></script>\n</head>");
            File.Copy("Assets/Editor/browser-resolution.js", Path.Combine(outputPath, "browser-resolution.js"), true);
            File.WriteAllText(indexPath, html);
        }
    }
}
