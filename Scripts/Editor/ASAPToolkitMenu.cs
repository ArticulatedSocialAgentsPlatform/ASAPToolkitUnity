#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ASAPToolkit.Unity.Retargeting;
using UnityEditor;
using UnityEngine;

namespace ASAPToolkit.Unity.Editor {

    public class ASAPToolkitMenu {

        [MenuItem("ASAP/Convert Face Target Text to Assets")]
        static void ConvertFaceTarget() {
            List<TextAsset> selectedTexts = new List<TextAsset>();
            foreach (Object o in Selection.objects) {
                if (o.GetType() == typeof(TextAsset)) {
                    selectedTexts.Add((TextAsset)o);
                }
            }

            foreach (TextAsset txt in selectedTexts) {
                BonePoseTarget bpt = ScriptableObject.CreateInstance<BonePoseTarget>();
                bpt.name = txt.name;
                List<BonePoseTarget.BonePoseSetting> bpts = new List<BonePoseTarget.BonePoseSetting>();
                foreach (string line in txt.text.Split(new[] { '\r', '\n' })) {
                    string[] args = line.Split(' ');
                    if (args.Length != 2) continue;
                    BonePoseTarget.BonePoseSetting targetSetting;
                    targetSetting.poseName = args[0].Trim();
                    targetSetting.poseValue = float.Parse(args[1]);
                    if (targetSetting.poseValue > 1.0f || targetSetting.poseValue < -1.0f) {
                        Debug.LogError("Invalid pose value: " + targetSetting.poseValue + " (" + txt.name+ ")");
                    } else {
                        bpts.Add(targetSetting);
                    }
                }
                bpt.bonePoseSettings = bpts.ToArray();

                if (bpt.bonePoseSettings.Length > 0) {
                    AssetDatabase.CreateAsset(bpt, "Assets/ASAPToolkitUnity/Resources/UMAFaceTargets/" + bpt.name + ".asset");
                    AssetDatabase.SaveAssets();
                }

            }


        }

        [MenuItem("ASAP/BML Editor Window")]
        static void ShowBMLEditorWindow() {
            BMLEditorWindow w = EditorWindow.GetWindow<BMLEditorWindow>(false, "BML Editor", true);
            w.Show();
            //w.Populate();
        }

        [MenuItem("ASAP/BML Feedback Window")]
        static void ShowBMLFeedbackWindow() {
            BMLFeedbackWindow w = EditorWindow.GetWindow<BMLFeedbackWindow>(false, "BML Feedback", true);
            w.Show();
            //w.Register();
        }

        [MenuItem("ASAP/Animation Exporter Window")]
        static void Init() {
            AnimationExporterWindow w = EditorWindow.GetWindow<AnimationExporterWindow>(false, "ASAP Animation Exporter", true);
            w.Show();
        }


        [MenuItem("ASAP/Create BoneMap")]
        public static void CreateBoneMapping() {
            string name = "New";
            BoneMapAsset bm = ScriptableObject.CreateInstance<BoneMapAsset>();
            SkeletonConfiguration cpp = Selection.activeGameObject.transform.GetComponent<SkeletonConfiguration>();
            if (cpp == null) cpp = Selection.activeGameObject.transform.GetComponentInChildren<SkeletonConfiguration>();
            if (cpp == null) cpp = Selection.activeGameObject.transform.GetComponentInParent<SkeletonConfiguration>();
            if (cpp != null) {
                name = cpp.transform.name;
                Transform[] bones = null;
                if (cpp.rootBone != null)
                     bones = CanonicalRepresentation.Bones(cpp.rootBone);
                else bones = CanonicalRepresentation.Bones(cpp.transform);

                bm.mappings = new BoneMapAsset.HAnimBoneMapping[bones.Length];
                for (int t = 0; t < bones.Length; t++) {
                    bm.mappings[t].src_bone = bones[t].name;
                    bm.mappings[t].hanim_bone = CanonicalRepresentation.HAnimBones.NONE;
                }
            } else {
                Debug.LogError("Failed to create a BoneMap. Select a SkeletonConfiguration in the hierarchy panel.");
            }

            AssetDatabase.CreateAsset(bm, "Assets/" + name + "BoneMapping.asset");
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = bm;
        }

        [MenuItem("ASAP/Create Stored Pose")]
        public static void CreateStoredPose() {

            string name = "New";
            StoredPoseAsset sp = ScriptableObject.CreateInstance<StoredPoseAsset>();

            SkeletonConfiguration cpp = Selection.activeGameObject.transform.GetComponent<SkeletonConfiguration>();
            if (cpp == null) cpp = Selection.activeGameObject.transform.GetComponentInChildren<SkeletonConfiguration>();
            if (cpp == null) cpp = Selection.activeGameObject.transform.GetComponentInParent<SkeletonConfiguration>();

            if (cpp != null) {
                name = cpp.transform.name;
                if (cpp.rootBone != null)
                     sp.pose = StoredPoseAsset.StorePose(cpp.rootBone);
                else sp.pose = StoredPoseAsset.StorePose(cpp.transform);
            } else {
                Debug.LogError("Failed to create a StoredPose. Select a SkeletonConfiguration in the hierarchy panel.");
            }

            AssetDatabase.CreateAsset(sp, "Assets/" + name + "StoredPose.asset");
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = sp;
        }
    }

}
#endif