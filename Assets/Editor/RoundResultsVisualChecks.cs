using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UmdJam.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class RoundResultsVisualChecks
    {
        public static IEnumerator Exercise()
        {
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            Type gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow window = EditorWindow.GetWindow(gameViewType);
            PropertyInfo selection = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int oldSelection = (int)selection.GetValue(window);
            try
            {
                yield return null;
                var lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                var round = GameManager.Instance;
                var director = Object.FindAnyObjectByType<RoundResultsDirector>();
                var view = Object.FindAnyObjectByType<RoundResultsView>();
                Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
                lobby.TrySetPlayerCount(4);
                for (int i = 0; i < 4; i++) Require(lobby.TryAddCpu(i), "Visual fixture CPU");
                int[] widths = { 1920, 1440 };
                foreach (int width in widths)
                {
                    SetSize(window, gameViewType, width, 1080);
                    for (int i = 0; i < 8; i++) yield return null;
                    ScreenCapture.CaptureScreenshot($".utmp/round-results-lobby-{width}.png");
                    for (int i = 0; i < 5; i++) yield return null;
                }
                Require(lobby.TryStartGame(), "Visual fixture starts");
                for (int i = 0; i < 4; i++) lobby.GetParticipant(i).Cpu.enabled = false;
                CouchPlayerController.ChangeScore(1, 24);
                CouchPlayerController.ChangeScore(2, 16);
                CouchPlayerController.ChangeScore(3, 19);
                CouchPlayerController.ChangeScore(4, 8);
                var prefab = AssetDatabase.LoadAssetAtPath<PickupFlask>("Assets/Gameplay/FlaskPlaceholder.prefab");
                Object.Instantiate(prefab, new Vector3(-4, 0.4f, 4), Quaternion.identity);
                Object.Instantiate(prefab, new Vector3(-2, 0.4f, 4), Quaternion.identity);
                Object.Instantiate(prefab, new Vector3(-4, 0.4f, 2), Quaternion.identity);
                round.EndRound();
                director.SkipToResults();
                director.StopAllCoroutines();
                foreach (int width in widths)
                {
                    SetSize(window, gameViewType, width, 1080);
                    for (int i = 0; i < 8; i++) yield return null;
                    view.Begin(round.Results);
                    view.FocusZone(round.Results.Players[0]);
                    view.ShowFlask(round.Results.Players[0].Flasks[0], 1, 3, round.Results.Players[0].Flasks[0].Points);
                    for (int i = 0; i < 5; i++) yield return null;
                    CheckProjection(view, round.Results.Players[0]);
                    ScreenCapture.CaptureScreenshot($".utmp/round-results-count-{width}.png");
                    for (int i = 0; i < 5; i++) yield return null;
                    view.ShowDeduction(round.Results.Players[0], 1f);
                    for (int i = 0; i < 5; i++) yield return null;
                    ScreenCapture.CaptureScreenshot($".utmp/round-results-deduction-{width}.png");
                    for (int i = 0; i < 5; i++) yield return null;
                    view.ShowWinners(round.Results);
                    view.ShowReplay();
                    for (int i = 0; i < 5; i++) yield return null;
                    ScreenCapture.CaptureScreenshot($".utmp/round-results-winner-{width}.png");
                    for (int i = 0; i < 5; i++) yield return null;
                    VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
                    Rect button = root.Q("playAgain").worldBound;
                    Rect panel = root.worldBound;
                    Require(button.yMax <= panel.yMax && button.xMax <= panel.xMax, "Replay fits panel");
                }
            }
            finally
            {
                selection.SetValue(window, oldSelection);
                Application.runInBackground = background;
            }
        }

        private static void CheckProjection(RoundResultsView view, PlayerRoundResult player)
        {
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement.Q("roundResults");
            Vector3 upperLeft = Camera.main.WorldToViewportPoint(new Vector3(player.ZoneBounds.min.x,
                player.ZoneBounds.min.y, player.ZoneBounds.max.z));
            Rect mask = root.Q("maskTop").layout;
            float expectedY = root.layout.height * (1f - upperLeft.y);
            Require(Mathf.Abs(mask.yMax - expectedY) < 2f, "Zone mask matches independently projected floor boundary");
            Vector3 flask = Camera.main.WorldToViewportPoint(player.Flasks[0].WorldPosition);
            Rect ring = root.Q("flaskMarker").layout;
            Require(Mathf.Abs(ring.center.x - flask.x * root.layout.width) < 2f &&
                Mathf.Abs(ring.center.y - (1f - flask.y) * root.layout.height) < 2f,
                "Flask marker matches frozen flask at this resolution");
        }

        private static void SetSize(EditorWindow window, Type gameViewType, int width, int height)
        {
            Assembly assembly = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType)
                .GetProperty("instance").GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
            Type groupClass = group.GetType();
            int total = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null);
            int selected = -1;
            for (int i = 0; i < total; i++)
            {
                object size = groupClass.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                Type type = size.GetType();
                if ((int)type.GetProperty("width").GetValue(size) == width &&
                    (int)type.GetProperty("height").GetValue(size) == height) selected = i;
            }
            if (selected < 0)
            {
                Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
                Type modeType = assembly.GetType("UnityEditor.GameViewSizeType");
                object size = Activator.CreateInstance(sizeType, new[] { Enum.Parse(modeType, "FixedResolution"),
                    (object)width, height, $"Results QA {width}x{height}" });
                groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                selected = total;
            }
            gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(window, selected);
            window.Focus();
            Directory.CreateDirectory(".utmp");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
