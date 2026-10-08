using System.Collections;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

namespace UmdJam.UI
{
    public sealed class RoundResultsDirector : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Camera arenaCamera;
        [SerializeField] private Transform resultsCameraTarget;
        [SerializeField] private RoundResultsView view;
        [SerializeField] private CouchPlayerHud hud;
        [SerializeField] private Renderer[] overheadOccluders = new Renderer[0];
        [SerializeField, Min(0.01f)] private float cameraMoveDuration = 1f;
        [SerializeField, Min(1f)] private float framingPadding = 1.15f;
        [SerializeField, Min(0f)] private float zoneIntroduction = 0.25f;
        [SerializeField, Min(0.01f)] private float flaskInterval = 0.2f;
        [SerializeField, Min(0.01f)] private float minimumFlaskInterval = 0.06f;
        [SerializeField, Min(0.01f)] private float zoneCountingBudget = 3f;
        [SerializeField, Min(0.01f)] private float deductionDuration = 0.65f;
        [SerializeField, Min(0f)] private float deductionHold = 0.55f;
        [SerializeField, Min(0f)] private float winnerHold = 1f;
        [SerializeField] private AudioSource tickSource;
        [SerializeField] private AudioClip tickClip;

        private Coroutine sequence;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private RoundResults results;
        private bool overhead;
        private bool replayReady;
        private bool loading;
        private InputAction submitHeld;
        private bool[] originalOccluderStates;

        public bool IsPresenting { get; private set; }
        public bool IsComplete { get; private set; }

        public void SkipToResults()
        {
            if (!IsPresenting || IsComplete || loading) return;
            if (sequence != null) StopCoroutine(sequence);
            if (tickSource != null) tickSource.Stop();
            overhead = true;
            FrameCamera();
            sequence = StartCoroutine(Finish());
        }

        private void Awake()
        {
            if (gameManager == null || arenaCamera == null || resultsCameraTarget == null || view == null || hud == null)
            {
                Debug.LogError("Round results requires its manager, camera, overhead target, view, and HUD.", this);
                enabled = false;
                return;
            }
            // Observes release only. UI Toolkit remains the sole owner of navigation and submit callbacks.
            submitHeld = new InputAction("ResultsSubmitHeld", InputActionType.Button);
            submitHeld.AddBinding("<Keyboard>/enter");
            submitHeld.AddBinding("<Keyboard>/numpadEnter");
            submitHeld.AddBinding("<Keyboard>/space");
            submitHeld.AddBinding("<Gamepad>/buttonSouth");
            submitHeld.AddBinding("<Mouse>/leftButton");
        }

        private void OnEnable()
        {
            GameManager.RoundEnded += Begin;
            if (view != null)
            {
                view.SkipRequested += SkipToResults;
                view.ReplayRequested += Replay;
            }
            submitHeld?.Enable();
        }

        private void Start()
        {
            if (gameManager != null && gameManager.Results != null) Begin();
        }

        private void LateUpdate()
        {
            if (IsPresenting && (!view.isActiveAndEnabled || !view.IsReady))
            {
                Release();
                return;
            }
            if (!IsPresenting && gameManager != null && gameManager.Results != null && view.IsReady) Begin();
            if (overhead) FrameCamera();
        }

        private void OnDisable()
        {
            GameManager.RoundEnded -= Begin;
            if (view != null)
            {
                view.SkipRequested -= SkipToResults;
                view.ReplayRequested -= Replay;
            }
            submitHeld?.Disable();
            Release();
        }

        private void OnDestroy() => submitHeld?.Dispose();

        private void Begin()
        {
            if (IsPresenting || gameManager.Results == null || !view.IsReady) return;
            results = gameManager.Results;
            originalPosition = arenaCamera.transform.position;
            originalRotation = arenaCamera.transform.rotation;
            IsPresenting = true;
            IsComplete = false;
            replayReady = false;
            originalOccluderStates = new bool[overheadOccluders.Length];
            for (int i = 0; i < overheadOccluders.Length; i++)
            {
                if (overheadOccluders[i] == null) continue;
                originalOccluderStates[i] = overheadOccluders[i].forceRenderingOff;
                overheadOccluders[i].forceRenderingOff = true;
            }
            hud.SetResultsPresentationActive(true);
            view.Begin(results);
            sequence = StartCoroutine(Present());
        }

        private IEnumerator Present()
        {
            float elapsed = 0f;
            while (elapsed < cameraMoveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, cameraMoveDuration));
                arenaCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(originalPosition, TargetPosition(), t),
                    Quaternion.Slerp(originalRotation, resultsCameraTarget.rotation, t));
                yield return null;
            }
            overhead = true;
            FrameCamera();
            foreach (PlayerRoundResult player in results.Players)
            {
                view.FocusZone(player);
                yield return new WaitForSecondsRealtime(zoneIntroduction);
                int count = player.Flasks.Count;
                int penalty = 0;
                float interval = count == 0 ? 0f : Mathf.Clamp(zoneCountingBudget / count,
                    minimumFlaskInterval, Mathf.Max(minimumFlaskInterval, flaskInterval));
                for (int i = 0; i < count; i++)
                {
                    FlaskPenaltyEntry flask = player.Flasks[i];
                    penalty += flask.Points;
                    view.ShowFlask(flask, i + 1, count, penalty);
                    if (tickSource != null && tickClip != null) tickSource.PlayOneShot(tickClip);
                    yield return new WaitForSecondsRealtime(interval);
                }
                elapsed = 0f;
                do
                {
                    elapsed += Time.unscaledDeltaTime;
                    view.ShowDeduction(player, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, deductionDuration)));
                    yield return null;
                } while (elapsed < deductionDuration);
                view.ShowDeduction(player, 1f);
                yield return new WaitForSecondsRealtime(deductionHold);
            }
            yield return Finish();
        }

        private IEnumerator Finish()
        {
            IsComplete = true;
            view.ShowWinners(results);
            yield return new WaitForSecondsRealtime(winnerHold);
            float releasedFor = 0f;
            while (releasedFor < 0.2f)
            {
                bool pressed = false;
                foreach (InputControl control in submitHeld.controls)
                {
                    if (control is not ButtonControl button || !button.isPressed) continue;
                    pressed = true;
                    break;
                }
                releasedFor = pressed ? 0f : releasedFor + Time.unscaledDeltaTime;
                yield return null;
            }
            replayReady = true;
            view.ShowReplay();
            sequence = null;
        }

        private Vector3 TargetPosition()
        {
            Bounds bounds = gameManager.ArenaBounds;
            float tangent = Mathf.Tan(arenaCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float aspect = Mathf.Max(0.1f, arenaCamera.aspect);
            float height = Mathf.Max(bounds.extents.z / tangent, bounds.extents.x / (tangent * aspect));
            Vector3 target = resultsCameraTarget.position;
            target.y = Mathf.Max(target.y, bounds.max.y + height * framingPadding);
            return target;
        }

        private void FrameCamera()
        {
            arenaCamera.transform.SetPositionAndRotation(TargetPosition(), resultsCameraTarget.rotation);
        }

        private void Replay()
        {
            if (!replayReady || loading || !IsComplete) return;
            loading = true;
            view.DisableReplay();
            SceneManager.LoadSceneAsync(gameObject.scene.path, LoadSceneMode.Single);
        }

        private void Release()
        {
            if (sequence != null) StopCoroutine(sequence);
            sequence = null;
            if (IsPresenting && arenaCamera != null)
                arenaCamera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            if (IsPresenting && originalOccluderStates != null)
            {
                for (int i = 0; i < overheadOccluders.Length; i++)
                    if (overheadOccluders[i] != null)
                        overheadOccluders[i].forceRenderingOff = originalOccluderStates[i];
            }
            if (tickSource != null) tickSource.Stop();
            if (view != null) view.End();
            if (hud != null) hud.SetResultsPresentationActive(false);
            overhead = false;
            IsPresenting = false;
            IsComplete = false;
            replayReady = false;
        }
    }
}
