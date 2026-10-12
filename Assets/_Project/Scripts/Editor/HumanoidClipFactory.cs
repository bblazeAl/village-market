using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IdleArcade.EditorTools
{
    /// <summary>
    /// Authors humanoid animation clips procedurally (muscle curves) so every Synty humanoid shares
    /// one set of motions: idle, walk, run, carry pose, sit-and-eat, cheer. Muscle conventions were
    /// measured on the Synty rig: for "A-B" muscles, -1 is A (Front-Back: -1 = front; Down-Up: -1 = down);
    /// Stretch: -1 bent, +1 straight.
    /// </summary>
    public static class HumanoidClipFactory
    {
        public const string Dir = "Assets/_Project/Generated/Animation";
        const int Fps = 30;

        // muscle indices (HumanTrait.MuscleName)
        const int SpineFB = 0, SpineLR = 1, SpineTwist = 2, ChestFB = 3, ChestLR = 4, ChestTwist = 5;
        const int NeckNod = 9, HeadNod = 12, HeadTilt = 13, HeadTurn = 14;
        const int LUpperFB = 21, LUpperIO = 22, LLowerStretch = 24, LFoot = 26, LToes = 28;
        const int RUpperFB = 29, RUpperIO = 30, RLowerStretch = 32, RFoot = 34, RToes = 36;
        const int LShoulderDU = 37, LShoulderFB = 38, LArmDU = 39, LArmFB = 40, LArmTwist = 41, LForearm = 42, LForearmTwist = 43, LHandDU = 44;
        const int RShoulderDU = 46, RShoulderFB = 47, RArmDU = 48, RArmFB = 49, RArmTwist = 50, RForearm = 51, RForearmTwist = 52, RHandDU = 53;

        // rest pose (measured)
        const float LegFB = 0.35f, LegStretch = 0.95f, FootUD = 0.05f, LegIO = 0.05f;
        const float ArmDU = -0.7f, ArmFB = 0.4f, Forearm = 0.3f;

        delegate float Curve(float t); // t = 0..1 over the clip

        class Spec
        {
            public string name;
            public float length;
            public bool loop = true;
            public readonly Dictionary<int, Curve> muscles = new Dictionary<int, Curve>();
            public Curve rootY = t => 1f;
            public void Set(int m, Curve c) => muscles[m] = c;
            public void Set(int m, float v) => muscles[m] = t => v;
        }

        static float Sin(float t, float cycles = 1f, float phase = 0f) => Mathf.Sin((t * cycles + phase) * Mathf.PI * 2f);
        static float Cos(float t, float cycles = 1f, float phase = 0f) => Mathf.Cos((t * cycles + phase) * Mathf.PI * 2f);
        static float Pos(float v) => Mathf.Max(0f, v);

        static void Legs(Spec s)
        {
            s.Set(LUpperFB, LegFB); s.Set(RUpperFB, LegFB);
            s.Set(LLowerStretch, LegStretch); s.Set(RLowerStretch, LegStretch);
            s.Set(LFoot, FootUD); s.Set(RFoot, FootUD);
            s.Set(LUpperIO, LegIO); s.Set(RUpperIO, LegIO);
        }

        static void Arms(Spec s)
        {
            s.Set(LArmDU, ArmDU); s.Set(RArmDU, ArmDU);
            s.Set(LArmFB, ArmFB); s.Set(RArmFB, ArmFB);
            s.Set(LForearm, Forearm); s.Set(RForearm, Forearm);
        }

        static Spec Idle()
        {
            var s = new Spec { name = "Idle", length = 2.6f };
            Legs(s);
            Arms(s);
            s.Set(LArmDU, t => ArmDU + 0.025f * Sin(t));
            s.Set(RArmDU, t => ArmDU + 0.025f * Sin(t, 1, 0.1f));
            s.Set(ChestFB, t => -0.04f * Sin(t));
            s.Set(SpineLR, t => 0.03f * Sin(t, 1, 0.25f));
            s.Set(HeadNod, t => 0.04f * Sin(t, 1, 0.15f));
            s.Set(HeadTilt, t => 0.03f * Sin(t, 1, 0.4f));
            s.rootY = t => 1f - 0.006f * (1f + Sin(t));
            return s;
        }

        static Spec Walk()
        {
            var s = new Spec { name = "Walk", length = 0.88f };
            Legs(s);
            Arms(s);
            const float a = 0.5f;
            s.Set(LUpperFB, t => LegFB - a * Sin(t));
            s.Set(RUpperFB, t => LegFB + a * Sin(t));
            // knees bend while the leg swings forward
            s.Set(LLowerStretch, t => LegStretch - 0.75f * Mathf.Pow(Pos(Cos(t)), 1.4f) - 0.08f);
            s.Set(RLowerStretch, t => LegStretch - 0.75f * Mathf.Pow(Pos(-Cos(t)), 1.4f) - 0.08f);
            s.Set(LFoot, t => FootUD + 0.25f * Sin(t, 1, 0.1f));
            s.Set(RFoot, t => FootUD - 0.25f * Sin(t, 1, 0.1f));
            s.Set(LArmFB, t => ArmFB + 0.45f * Sin(t));
            s.Set(RArmFB, t => ArmFB - 0.45f * Sin(t));
            s.Set(LForearm, t => Forearm - 0.15f - 0.1f * Sin(t));
            s.Set(RForearm, t => Forearm - 0.15f + 0.1f * Sin(t));
            s.Set(SpineTwist, t => 0.1f * Sin(t));
            s.Set(ChestTwist, t => -0.06f * Sin(t));
            s.Set(SpineFB, -0.06f);
            s.Set(SpineLR, t => 0.04f * Sin(t));
            s.Set(HeadNod, t => 0.03f * Cos(t, 2));
            s.rootY = t => 0.985f + 0.022f * Cos(t, 2);
            return s;
        }

        static Spec Run()
        {
            var s = new Spec { name = "Run", length = 0.6f };
            Legs(s);
            Arms(s);
            const float a = 0.72f;
            s.Set(LUpperFB, t => LegFB - 0.08f - a * Sin(t));
            s.Set(RUpperFB, t => LegFB - 0.08f + a * Sin(t));
            s.Set(LLowerStretch, t => LegStretch - 1.05f * Mathf.Pow(Pos(Cos(t, 1, -0.05f)), 1.2f) - 0.12f);
            s.Set(RLowerStretch, t => LegStretch - 1.05f * Mathf.Pow(Pos(-Cos(t, 1, -0.05f)), 1.2f) - 0.12f);
            s.Set(LFoot, t => FootUD + 0.35f * Sin(t, 1, 0.12f));
            s.Set(RFoot, t => FootUD - 0.35f * Sin(t, 1, 0.12f));
            s.Set(LArmDU, ArmDU + 0.08f); s.Set(RArmDU, ArmDU + 0.08f);
            s.Set(LArmFB, t => ArmFB - 0.1f + 0.7f * Sin(t));
            s.Set(RArmFB, t => ArmFB - 0.1f - 0.7f * Sin(t));
            s.Set(LForearm, t => -0.25f - 0.15f * Sin(t));
            s.Set(RForearm, t => -0.25f + 0.15f * Sin(t));
            s.Set(SpineTwist, t => 0.14f * Sin(t));
            s.Set(ChestTwist, t => -0.08f * Sin(t));
            s.Set(SpineFB, -0.22f);
            s.Set(ChestFB, -0.08f);
            s.Set(HeadNod, 0.12f);
            s.rootY = t => 0.975f + 0.045f * Mathf.Abs(Sin(t, 2, 0.25f));
            return s;
        }

        static Spec Carry()
        {
            var s = new Spec { name = "CarryArms", length = 1f };
            // hands under the front of the stack (measured with a reach search)
            s.Set(LArmDU, t => -0.62f + 0.01f * Sin(t)); s.Set(RArmDU, t => -0.62f + 0.01f * Sin(t));
            s.Set(LArmFB, -0.32f); s.Set(RArmFB, -0.32f);
            s.Set(LForearm, 0.2f); s.Set(RForearm, 0.2f);
            s.Set(LArmTwist, 0.3f); s.Set(RArmTwist, 0.3f);
            s.Set(LForearmTwist, -0.3f); s.Set(RForearmTwist, -0.3f);
            s.Set(LHandDU, 0.2f); s.Set(RHandDU, 0.2f);
            s.Set(LShoulderFB, -0.4f); s.Set(RShoulderFB, -0.4f);
            return s;
        }

        static Spec Sit()
        {
            var s = new Spec { name = "SitEat", length = 2.2f };
            s.Set(LUpperFB, -0.85f); s.Set(RUpperFB, -0.85f);
            s.Set(LLowerStretch, -0.15f); s.Set(RLowerStretch, -0.15f);
            s.Set(LFoot, t => 0.1f + 0.15f * Sin(t, 2)); s.Set(RFoot, t => 0.1f - 0.15f * Sin(t, 2, 0.3f));
            s.Set(LUpperIO, 0.1f); s.Set(RUpperIO, 0.1f);
            // right hand brings food to the mouth, left rests on the table
            s.Set(RArmDU, t => -0.55f + 0.18f * Pos(Sin(t)));
            s.Set(RArmFB, t => -0.45f - 0.3f * Pos(Sin(t)));
            s.Set(RForearm, t => 0.1f - 0.8f * Pos(Sin(t)));
            s.Set(LArmDU, -0.6f); s.Set(LArmFB, -0.35f); s.Set(LForearm, 0.15f);
            s.Set(HeadNod, t => 0.08f * Pos(Sin(t)));
            s.Set(SpineFB, -0.1f);
            s.rootY = t => 0.93f;
            return s;
        }

        static Spec Cheer()
        {
            var s = new Spec { name = "Cheer", length = 0.9f, loop = false };
            Curve up = t => Mathf.Sin(Mathf.Clamp01(t / 0.9f) * Mathf.PI);
            Legs(s);
            s.Set(LArmDU, t => Mathf.Lerp(ArmDU, 0.75f, up(t)));
            s.Set(RArmDU, t => Mathf.Lerp(ArmDU, 0.75f, up(t)));
            s.Set(LArmFB, t => Mathf.Lerp(ArmFB, -0.2f, up(t)));
            s.Set(RArmFB, t => Mathf.Lerp(ArmFB, -0.2f, up(t)));
            s.Set(LForearm, t => Mathf.Lerp(Forearm, 0.6f, up(t)) + 0.15f * Sin(t, 3) * up(t));
            s.Set(RForearm, t => Mathf.Lerp(Forearm, 0.6f, up(t)) - 0.15f * Sin(t, 3) * up(t));
            s.Set(LLowerStretch, t => LegStretch - 0.5f * Mathf.Pow(Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f, 3f) * (1 - up(t) * 0.5f));
            s.Set(RLowerStretch, t => LegStretch - 0.5f * Mathf.Pow(Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f, 3f) * (1 - up(t) * 0.5f));
            s.Set(HeadNod, t => -0.15f * up(t));
            s.rootY = t => 1f + 0.09f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * up(t);
            return s;
        }

        // ------------------------------------------------------------------ assembly
        static AnimationClip Bake(Spec s)
        {
            var clip = new AnimationClip { name = s.name, frameRate = Fps };
            int frames = Mathf.Max(2, Mathf.RoundToInt(s.length * Fps));
            var names = HumanTrait.MuscleName;
            foreach (var kv in s.muscles)
            {
                var curve = new AnimationCurve();
                for (int f = 0; f <= frames; f++)
                {
                    float t = (float)f / frames;
                    curve.AddKey(new Keyframe(t * s.length, kv.Value(s.loop && f == frames ? 0f : t)));
                }
                for (int k = 0; k < curve.length; k++) AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.ClampedAuto);
                for (int k = 0; k < curve.length; k++) AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), names[kv.Key]), curve);
            }
            var ry = new AnimationCurve();
            for (int f = 0; f <= frames; f++)
            {
                float t = (float)f / frames;
                ry.AddKey(t * s.length, s.rootY(s.loop && f == frames ? 0f : t));
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y"), ry);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x"), AnimationCurve.Constant(0, s.length, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.z"), AnimationCurve.Constant(0, s.length, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.x"), AnimationCurve.Constant(0, s.length, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.y"), AnimationCurve.Constant(0, s.length, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.z"), AnimationCurve.Constant(0, s.length, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.w"), AnimationCurve.Constant(0, s.length, 1f));

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = s.loop;
            settings.loopBlend = s.loop;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        static T SaveAsset<T>(T obj, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(obj, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        public static Dictionary<string, AnimationClip> BuildClips()
        {
            System.IO.Directory.CreateDirectory(Dir);
            var result = new Dictionary<string, AnimationClip>();
            foreach (var spec in new[] { Idle(), Walk(), Run(), Carry(), Sit(), Cheer() })
            {
                var clip = Bake(spec);
                result[spec.name] = SaveAsset(clip, $"{Dir}/{spec.name}.anim");
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        public const string LibraryDir = "Assets/_Project/Art/Animations/Quaternius";

        /// <summary>
        /// Imports the Quaternius Universal Animation Library (CC0, motion-captured quality) as Humanoid clips
        /// with root motion baked into the pose, so they retarget onto every Synty character.
        /// </summary>
        public static Dictionary<string, AnimationClip> ImportLibraryClips()
        {
            var result = new Dictionary<string, AnimationClip>();
            if (!AssetDatabase.IsValidFolder(LibraryDir)) return result;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { LibraryDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = true;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.importBlendShapes = false;
                var clips = imp.defaultClipAnimations;
                foreach (var c in clips)
                {
                    bool loop = c.name.Contains("_Loop") || c.name.EndsWith("Loop");
                    c.loopTime = loop;
                    c.loopPose = loop;
                    c.lockRootRotation = true;
                    c.lockRootHeightY = true;
                    c.lockRootPositionXZ = true;
                    c.keepOriginalOrientation = false;   // body orientation: the library faces -Z, Synty faces +Z
                    c.keepOriginalPositionY = false;
                    c.heightFromFeet = true;
                    c.keepOriginalPositionXZ = true;
                }
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
                foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                    if (o is AnimationClip ac && !ac.name.StartsWith("__preview")) result[ac.name] = ac;
            }
            return result;
        }

        const string SyntyAnim = "Assets/_Project/Art/Synty/";

        /// <summary>Loads a Synty ANIMATION clip (Emotes / Idles packs) and makes it loop if asked. Null if the pack is absent.</summary>
        static AnimationClip SyntyClip(string relPath, bool loop)
        {
            string path = SyntyAnim + relPath;
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return null;
            var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            bool dirty = false;
            foreach (var c in clips)
            {
                if (c.loopTime != loop || c.loopPose != loop || !c.lockRootRotation || !c.lockRootHeightY || !c.lockRootPositionXZ)
                {
                    c.loopTime = loop; c.loopPose = loop;
                    c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
                    dirty = true;
                }
            }
            if (dirty) { imp.clipAnimations = clips; imp.SaveAndReimport(); }
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (o is AnimationClip ac && !ac.name.StartsWith("__preview")) return ac;
            return null;
        }

        const string Emo = "AnimationEmotesAndTaunts/Animations/Polygon/Feminine/";
        const string Idl = "AnimationIdles/Animations/Polygon/Feminine/";

        // emote ids used by CharacterAnim.Emote(): 1-5 happy, 6 disappointed, 7 shrug, 8 thanks
        static readonly string[] EmotePaths =
        {
            null,
            Emo + "Happy/A_POLY_EMOT_Happy_ThumbsUp_Femn.fbx",
            Emo + "Happy/A_POLY_EMOT_Happy_ArmsRaised_Femn.fbx",
            Emo + "Happy/A_POLY_EMOT_Happy_FistPump_Femn.fbx",
            Emo + "Celebrate/A_POLY_EMOT_Celebrate_Clapping_Polite_Femn.fbx",
            Emo + "Happy/A_POLY_EMOT_Happy_TwoThumbs_Mild_Femn.fbx",
            Idl + "HeadShake/Actions/A_POLY_IDL_HeadShake_Disappointed_Femn.fbx",
            Emo + "Sad/A_POLY_EMOT_Sad_Shrug_Femn.fbx",
            Idl + "HeadNod/Actions/A_POLY_IDL_HeadNod_Thanks_Femn.fbx",
        };

        static AnimationClip Pick(Dictionary<string, AnimationClip> lib, Dictionary<string, AnimationClip> procedural, string libName, string procName)
        {
            foreach (var kv in lib)
                if (kv.Key == libName || kv.Key.EndsWith("|" + libName)) return kv.Value;
            return procedural[procName];
        }

        public static AnimatorController BuildController()
        {
            var proc = BuildClips();
            var lib = ImportLibraryClips();
            Debug.Log($"[Anim] library clips: {lib.Count} ({string.Join(", ", lib.Keys)})");
            var clips = new Dictionary<string, AnimationClip>
            {
                ["Idle"] = Pick(lib, proc, "Idle_Loop", "Idle"),
                ["Walk"] = Pick(lib, proc, "Walk_Loop", "Walk"),
                ["Run"] = Pick(lib, proc, "Jog_Fwd_Loop", "Run"),
                ["CarryArms"] = Pick(lib, proc, "Walk_Carry_Loop", "CarryArms"),
                ["SitEat"] = Pick(lib, proc, "Sitting_Idle_Loop", "SitEat"),
                ["Cheer"] = Pick(lib, proc, "Yes", "Cheer"),
            };
            string path = $"{Dir}/Character.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveRate", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Sit", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Cheer", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Mood", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Emote", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("EmoteGo", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Eating", AnimatorControllerParameterType.Bool);
            var ps = ctrl.parameters;
            foreach (var p in ps) if (p.name == "MoveRate") p.defaultFloat = 1f;
            ctrl.parameters = ps;

            // base: locomotion blend + sit
            var baseSm = ctrl.layers[0].stateMachine;
            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            var footTap = SyntyClip(Idl + "Bored/Actions/A_POLY_IDL_Bored_FootTap_Femn.fbx", true);
            var armsFolded = SyntyClip(Idl + "ArmsFolded/Stances/A_POLY_IDL_ArmsFolded_Casual_Loop_Femn.fbx", true);
            if (footTap != null && armsFolded != null)
            {
                // Mood: 0 normal idle, 1 impatient foot tap, 2 relaxed arms folded (idle helpers)
                var idleTree = new BlendTree { name = "IdleMood", blendType = BlendTreeType.Simple1D, blendParameter = "Mood", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(idleTree, ctrl);
                idleTree.AddChild(clips["Idle"], 0f);
                idleTree.AddChild(footTap, 1f);
                idleTree.AddChild(armsFolded, 2f);
                tree.AddChild(idleTree, 0f);
            }
            else tree.AddChild(clips["Idle"], 0f);
            tree.AddChild(clips["Walk"], 0.45f);
            tree.AddChild(clips["Run"], 1f);
            var loco = baseSm.AddState("Locomotion");
            loco.motion = tree;
            loco.speedParameter = "MoveRate";
            loco.speedParameterActive = true;
            var sit = baseSm.AddState("Sit");
            sit.motion = clips["SitEat"];
            baseSm.defaultState = loco;
            var toSit = loco.AddTransition(sit);
            toSit.AddCondition(AnimatorConditionMode.If, 0, "Sit");
            toSit.duration = 0.15f;
            toSit.hasExitTime = false;
            var fromSit = sit.AddTransition(loco);
            fromSit.AddCondition(AnimatorConditionMode.IfNot, 0, "Sit");
            fromSit.duration = 0.15f;
            fromSit.hasExitTime = false;

            // carry: arms-only override
            var mask = new AvatarMask { name = "ArmsMask" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            AssetDatabase.AddObjectToAsset(mask, ctrl);
            var carrySm = new AnimatorStateMachine { name = "Carry", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(carrySm, ctrl);
            var carryState = carrySm.AddState("Carry");
            carryState.motion = clips["CarryArms"];
            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = "Carry",
                stateMachine = carrySm,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0f
            });

            // hands: eating at the table (arms + head only)
            var eat = SyntyClip(Idl + "Eat/Actions/A_POLY_IDL_Eat_Small_Femn.fbx", true);
            if (eat != null)
            {
                var handsMask = new AvatarMask { name = "HandsHeadMask" };
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) handsMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                handsMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
                handsMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                handsMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                handsMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                handsMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
                AssetDatabase.AddObjectToAsset(handsMask, ctrl);
                var handsSm = new AnimatorStateMachine { name = "Hands", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(handsSm, ctrl);
                var hEmpty = handsSm.AddState("Empty");
                var hEat = handsSm.AddState("Eat");
                hEat.motion = eat;
                handsSm.defaultState = hEmpty;
                var toEat = hEmpty.AddTransition(hEat); toEat.AddCondition(AnimatorConditionMode.If, 0, "Eating"); toEat.duration = 0.2f; toEat.hasExitTime = false;
                var fromEat = hEat.AddTransition(hEmpty); fromEat.AddCondition(AnimatorConditionMode.IfNot, 0, "Eating"); fromEat.duration = 0.2f; fromEat.hasExitTime = false;
                ctrl.AddLayer(new AnimatorControllerLayer { name = "Hands", stateMachine = handsSm, avatarMask = handsMask, blendingMode = AnimatorLayerBlendingMode.Override, defaultWeight = 1f });
            }

            // overlay: cheer one-shot over everything
            var overSm = new AnimatorStateMachine { name = "Overlay", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(overSm, ctrl);
            var empty = overSm.AddState("Empty");
            var cheer = overSm.AddState("Cheer");
            cheer.motion = clips["Cheer"];
            overSm.defaultState = empty;
            var any = overSm.AddAnyStateTransition(cheer);
            any.AddCondition(AnimatorConditionMode.If, 0, "Cheer");
            any.duration = 0.08f;
            any.hasExitTime = false;
            var back = cheer.AddTransition(empty);
            back.hasExitTime = true;
            back.exitTime = 0.92f;
            back.duration = 0.1f;
            for (int id = 1; id < EmotePaths.Length; id++)
            {
                var clip = SyntyClip(EmotePaths[id], false);
                if (clip == null) continue;
                var st = overSm.AddState("Emote" + id);
                st.motion = clip;
                var tIn = overSm.AddAnyStateTransition(st);
                tIn.AddCondition(AnimatorConditionMode.If, 0, "EmoteGo");
                tIn.AddCondition(AnimatorConditionMode.Equals, id, "Emote");
                tIn.duration = 0.12f;
                tIn.hasExitTime = false;
                tIn.canTransitionToSelf = false;
                var tOut = st.AddTransition(empty);
                tOut.hasExitTime = true;
                tOut.exitTime = 0.9f;
                tOut.duration = 0.2f;
            }
            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = "Overlay",
                stateMachine = overSm,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f
            });
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        /// <summary>
        /// Editor rendering of skinned meshes can be stale in edit mode, so previews bake the posed
        /// mesh into a static proxy object that renders reliably.
        /// </summary>
        public static GameObject PoseProxy(GameObject character, AnimationClip clip, float time, Vector3 offset)
        {
            Vector3 origin = character.transform.position;
            Quaternion originRot = character.transform.rotation;
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(character, clip, time);
            AnimationMode.EndSampling();
            // humanoid sampling may move the root; express parts relative to it, then place at the original spot
            Vector3 sampledRoot = character.transform.position;
            Quaternion toOrigin = originRot * Quaternion.Inverse(character.transform.rotation);
            var proxy = new GameObject(character.name + "_Pose_" + clip.name + "_" + time.ToString("0.00"));
            foreach (var smr in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var m = new Mesh();
                smr.BakeMesh(m, true);
                var part = new GameObject(smr.name);
                part.transform.SetParent(proxy.transform, false);
                Vector3 rel = toOrigin * (smr.transform.position - sampledRoot);
                part.transform.SetPositionAndRotation(origin + rel + offset, toOrigin * smr.transform.rotation);
                part.AddComponent<MeshFilter>().sharedMesh = m;
                part.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
            }
            AnimationMode.StopAnimationMode();
            return proxy;
        }

        [MenuItem("IdleArcade/Animation/Build Character Animations")]
        public static void Menu() => BuildController();
    }
}
