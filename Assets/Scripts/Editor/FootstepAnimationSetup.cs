using System;
using System.Collections.Generic;
using UmdJam.Multiplayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class FootstepAnimationSetup
    {
        private const int SamplesPerCycle = 120;

        [MenuItem("Tools/UmdJam/Configure Footstep Animation Events")]
        public static void Apply()
        {
            CharacterSkinCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterSkinCatalog>(
                "Assets/Data/Multiplayer/CharacterSkinCatalog.asset");
            if (catalog == null || catalog.Count == 0 || catalog.AnimationController == null)
                throw new InvalidOperationException("Footstep setup requires the character catalog and animation controller.");

            CharacterSkinCatalog.Entry skin = catalog.Get(0);
            GameObject preview = Object.Instantiate(skin.Prefab);
            preview.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Animator animator = preview.GetComponent<Animator>();
                if (animator == null) animator = preview.AddComponent<Animator>();
                animator.avatar = skin.Avatar;
                animator.applyRootMotion = false;
                animator.fireEvents = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                HashSet<AnimationClip> visited = new();
                int configured = 0;
                foreach (AnimationClip clip in catalog.AnimationController.animationClips)
                {
                    if (!visited.Add(clip) || !clip.isLooping ||
                        clip.name.IndexOf("Run", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    float[] contacts = FindContacts(animator, clip);
                    string path = AssetDatabase.GetAssetPath(clip);
                    ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null) throw new InvalidOperationException($"Expected an imported locomotion clip: {path}");
                    ModelImporterClipAnimation[] clips = importer.clipAnimations;
                    if (clips.Length == 0) clips = importer.defaultClipAnimations;
                    bool matched = false;
                    foreach (ModelImporterClipAnimation settings in clips)
                    {
                        if (settings.name != clip.name) continue;
                        List<AnimationEvent> events = new();
                        foreach (AnimationEvent existing in settings.events)
                            if (existing.functionName != "Footstep") events.Add(existing);
                        for (int foot = 0; foot < contacts.Length; foot++)
                            events.Add(new AnimationEvent { functionName = "Footstep", time = contacts[foot], intParameter = foot });
                        events.Sort((a, b) => a.time.CompareTo(b.time));
                        settings.events = events.ToArray();
                        matched = true;
                    }
                    if (!matched) throw new InvalidOperationException($"Missing importer clip settings for {clip.name}.");
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                    configured++;
                    Debug.Log($"Footstep contacts: {path}, left={contacts[0]:F3}, right={contacts[1]:F3} of cycle.");
                }
                if (configured == 0) throw new InvalidOperationException("No looping run clips found for footstep setup.");
            }
            finally
            {
                Object.DestroyImmediate(preview);
            }
        }

        private static float[] FindContacts(Animator animator, AnimationClip clip)
        {
            PlayableGraph graph = PlayableGraph.Create("Footstep contact sampling");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
                graph.Evaluate(0f);
                Transform[] feet = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
                if (feet[0] == null || feet[1] == null) throw new InvalidOperationException("Character avatar requires both foot bones.");
                float[][] heights = { new float[SamplesPerCycle], new float[SamplesPerCycle] };
                float[][] forwardPositions = { new float[SamplesPerCycle], new float[SamplesPerCycle] };
                for (int sample = 0; sample < SamplesPerCycle; sample++)
                {
                    playable.SetTime(clip.length * sample / SamplesPerCycle);
                    graph.Evaluate(0f);
                    for (int foot = 0; foot < feet.Length; foot++)
                    {
                        Vector3 position = animator.transform.InverseTransformPoint(feet[foot].position);
                        if (!float.IsFinite(position.y) || !float.IsFinite(position.z))
                            throw new InvalidOperationException("Foot pose contains invalid coordinates.");
                        heights[foot][sample] = position.y;
                        forwardPositions[foot][sample] = position.z;
                    }
                }
                return new[] { ContactPhase(heights[0], forwardPositions[0]), ContactPhase(heights[1], forwardPositions[1]) };
            }
            finally
            {
                graph.Destroy();
            }
        }

        private static float ContactPhase(float[] heights, float[] forwardPositions)
        {
            int forwardmost = 0;
            for (int i = 1; i < heights.Length; i++)
                if (forwardPositions[i] > forwardPositions[forwardmost]) forwardmost = i;
            // Plant follows the forward swing. Global height minima can instead describe the rear-foot push-off.
            int lowest = forwardmost;
            for (int i = 1; i <= heights.Length / 4; i++)
            {
                int sample = (forwardmost + i) % heights.Length;
                if (heights[sample] < heights[lowest]) lowest = sample;
            }
            float threshold = Mathf.Lerp(heights[lowest], heights[forwardmost], 0.15f);
            int contact = forwardmost;
            while (contact != lowest && heights[contact] > threshold)
                contact = (contact + 1) % heights.Length;
            return (float)contact / heights.Length;
        }
    }
}
