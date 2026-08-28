using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;

/// <summary>
/// 휴머노이드 캐릭터 FBX + 휴머노이드 애니 클립을 받아,
/// 내 캐릭터 뼈에 모션을 구운 뒤 FBX로 보냅니다. (Max에서 편집용)
/// 메뉴: Tools &gt; Animation &gt; Humanoid 애니 FBX 굽기
/// </summary>
public class HumanoidAnimFbxBakerWindow : EditorWindow
{
    private const string MenuPath = "Tools/Animation/Humanoid 애니 FBX 굽기";
    private const string PrefsLastFolder = "HumanoidAnimFbxBaker.LastFolder";
    private const string TempFolder = "Assets/Editor/TempHumanoidFbxBake";

    [SerializeField] private GameObject characterFbx;
    [SerializeField] private List<AnimationClip> clips = new List<AnimationClip>();
    private Vector2 clipScroll;
    private string statusMessage;
    private MessageType statusType = MessageType.Info;

    [MenuItem(MenuPath)]
    private static void Open()
    {
        var window = GetWindow<HumanoidAnimFbxBakerWindow>("휴머노이드 FBX 굽기");
        window.minSize = new Vector2(460f, 420f);
    }

    private void OnGUI()
    {
        if (clips == null)
            clips = new List<AnimationClip>();

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "캐릭터 FBX는 하나, 애니 클립은 여러 개 넣을 수 있습니다. 둘 다 휴머노이드만 됩니다.\n" +
            "버튼을 누르면 클립마다 FBX를 하나씩 만듭니다. 길이는 각 클립을 그대로 씁니다.\n" +
            "내보낸 FBX 최상위는 Max와 같게 Root_Dummy 입니다. (파일명 루트는 넣지 않습니다)",
            MessageType.Info);

        EditorGUILayout.Space(6f);

        EditorGUI.BeginChangeCheck();
        GameObject droppedCharacter = (GameObject)EditorGUILayout.ObjectField(
            "캐릭터 FBX", characterFbx, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            if (droppedCharacter != null && !IsHumanoidCharacter(droppedCharacter, out string charReason))
            {
                SetStatus(charReason, MessageType.Error);
            }
            else
            {
                characterFbx = droppedCharacter;
                ClearStatusIfError();
            }
        }

        EditorGUILayout.Space(8f);
        DrawClipList();

        EditorGUILayout.Space(12f);
        int validCount = CountValidClips();
        using (new EditorGUI.DisabledScope(!CanExport()))
        {
            string buttonLabel = validCount <= 1
                ? "FBX로 보내기"
                : $"FBX로 보내기 ({validCount}개)";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(32f)))
                Export();
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }
    }

    private void DrawClipList()
    {
        EditorGUILayout.LabelField($"애니 클립 ({CountValidClips()}개)", EditorStyles.boldLabel);

        Rect dropRect = GUILayoutUtility.GetRect(0f, 44f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "여기에 클립 또는 애니 FBX를 여러 개 끌어다 놓으세요", EditorStyles.helpBox);
        HandleClipDragAndDrop(dropRect);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("클립 칸 추가", GUILayout.Height(22f)))
            clips.Add(null);
        if (GUILayout.Button("프로젝트에서 선택한 클립 넣기", GUILayout.Height(22f)))
            TryAddFromSelection();
        if (GUILayout.Button("비우기", GUILayout.Height(22f), GUILayout.Width(64f)))
            clips.Clear();
        EditorGUILayout.EndHorizontal();

        clipScroll = EditorGUILayout.BeginScrollView(clipScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(260f));
        for (int i = 0; i < clips.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(24f));

            EditorGUI.BeginChangeCheck();
            AnimationClip droppedClip = (AnimationClip)EditorGUILayout.ObjectField(
                clips[i], typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (droppedClip != null && !IsHumanoidClip(droppedClip, out string clipReason))
                {
                    SetStatus(clipReason, MessageType.Error);
                }
                else
                {
                    clips[i] = droppedClip;
                    ClearStatusIfError();
                }
            }

            if (clips[i] != null)
            {
                EditorGUILayout.LabelField(
                    $"{clips[i].length:0.##}초",
                    GUILayout.Width(64f));
            }

            if (GUILayout.Button("X", GUILayout.Width(22f)))
            {
                clips.RemoveAt(i);
                i--;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void HandleClipDragAndDrop(Rect dropRect)
    {
        Event evt = Event.current;
        if (!dropRect.Contains(evt.mousePosition))
            return;

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                int added = TryAddClipsFromObjects(DragAndDrop.objectReferences);
                if (added > 0)
                    SetStatus($"휴머노이드 클립 {added}개를 넣었습니다.", MessageType.Info);
                evt.Use();
            }
            else
            {
                evt.Use();
            }
        }
    }

    private void TryAddFromSelection()
    {
        int added = TryAddClipsFromObjects(Selection.objects);
        if (added > 0)
            SetStatus($"휴머노이드 클립 {added}개를 넣었습니다.", MessageType.Info);
        else
            SetStatus("선택한 항목에서 휴머노이드 클립을 찾지 못했습니다.", MessageType.Warning);
    }

    private int TryAddClipsFromObjects(UnityEngine.Object[] objects)
    {
        if (objects == null)
            return 0;

        int added = 0;
        string lastReject = null;
        for (int i = 0; i < objects.Length; i++)
        {
            UnityEngine.Object obj = objects[i];
            if (obj == null)
                continue;

            if (obj is AnimationClip directClip)
            {
                if (TryAddClip(directClip, out lastReject))
                    added++;
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath))
                continue;

            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int s = 0; s < subAssets.Length; s++)
            {
                if (subAssets[s] is AnimationClip subClip && TryAddClip(subClip, out lastReject))
                    added++;
            }
        }

        if (added == 0 && !string.IsNullOrEmpty(lastReject))
            SetStatus(lastReject, MessageType.Error);

        return added;
    }

    private bool TryAddClip(AnimationClip clip, out string rejectReason)
    {
        rejectReason = null;
        if (clip == null)
            return false;

        if (!IsHumanoidClip(clip, out rejectReason))
            return false;

        if (clip.length <= 0.0001f)
        {
            rejectReason = $"'{clip.name}' 길이가 0이라 넣을 수 없습니다.";
            return false;
        }

        if (clips.Contains(clip))
            return false;

        // 빈 칸이 있으면 거기부터 채웁니다.
        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] == null)
            {
                clips[i] = clip;
                return true;
            }
        }

        clips.Add(clip);
        return true;
    }

    private int CountValidClips()
    {
        int count = 0;
        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] != null && IsHumanoidClip(clips[i], out _) && clips[i].length > 0.0001f)
                count++;
        }

        return count;
    }

    private List<AnimationClip> GetValidClips()
    {
        var valid = new List<AnimationClip>();
        for (int i = 0; i < clips.Count; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && IsHumanoidClip(clip, out _) && clip.length > 0.0001f && !valid.Contains(clip))
                valid.Add(clip);
        }

        return valid;
    }

    private bool CanExport()
    {
        return characterFbx != null
            && IsHumanoidCharacter(characterFbx, out _)
            && CountValidClips() > 0;
    }

    private static bool IsHumanoidCharacter(GameObject model, out string reason)
    {
        reason = null;
        if (model == null)
        {
            reason = "캐릭터 FBX가 없습니다.";
            return false;
        }

        string assetPath = AssetDatabase.GetAssetPath(model);
        if (!string.IsNullOrEmpty(assetPath) && AssetImporter.GetAtPath(assetPath) is ModelImporter importer)
        {
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                reason = $"'{model.name}'은(는) 휴머노이드 FBX가 아닙니다. Rig에서 Animation Type을 Humanoid로 바꾼 뒤 넣어 주세요.";
                return false;
            }

            return true;
        }

        Animator animator = FindHumanoidAnimator(model);
        if (animator == null)
        {
            reason = $"'{model.name}'에서 휴머노이드 Avatar를 찾지 못했습니다. 휴머노이드 FBX만 넣을 수 있습니다.";
            return false;
        }

        return true;
    }

    private static bool IsHumanoidClip(AnimationClip clip, out string reason)
    {
        reason = null;
        if (clip == null)
        {
            reason = "애니 클립이 없습니다.";
            return false;
        }

        if (clip.legacy)
        {
            reason = $"'{clip.name}'은(는) Legacy 클립입니다. 휴머노이드 클립만 넣을 수 있습니다.";
            return false;
        }

        if (!clip.isHumanMotion)
        {
            reason = $"'{clip.name}'은(는) 휴머노이드 클립이 아닙니다. 원본 FBX Rig가 Humanoid인지 확인해 주세요.";
            return false;
        }

        return true;
    }

    private static Animator FindHumanoidAnimator(GameObject root)
    {
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
                return animator;
        }

        return null;
    }

    private static float GetFrameRate(AnimationClip clip)
    {
        return clip.frameRate > 0.01f ? clip.frameRate : 30f;
    }

    private static int GetFrameCount(AnimationClip clip)
    {
        return Mathf.Max(1, Mathf.RoundToInt(clip.length * GetFrameRate(clip)));
    }

    private void Export()
    {
        if (!CanExport())
            return;

        List<AnimationClip> validClips = GetValidClips();
        if (validClips.Count == 0)
            return;

        string lastFolder = EditorPrefs.GetString(PrefsLastFolder, Application.dataPath);
        string folder = EditorUtility.OpenFolderPanel("FBX를 저장할 폴더", lastFolder, "");
        if (string.IsNullOrEmpty(folder))
            return;

        EditorPrefs.SetString(PrefsLastFolder, folder);

        bool overwriteAll = false;
        bool askedOverwrite = false;
        var usedNames = new HashSet<string>();
        var savedPaths = new List<string>();
        var skipped = new List<string>();
        var failed = new List<string>();

        GameObject instance = null;
        var tempControllers = new List<string>();
        var tempClipPaths = new List<string>();

        try
        {
            EditorUtility.DisplayProgressBar("휴머노이드 FBX 굽기", "캐릭터 준비 중...", 0f);

            instance = CreateTempInstance(characterFbx);
            Animator animator = FindHumanoidAnimator(instance);
            if (animator == null)
            {
                SetStatus("인스턴스에서 휴머노이드 Animator를 찾지 못했습니다.", MessageType.Error);
                return;
            }

            // Max 바이패드 .max 와 같게 FBX 최상위는 Root_Dummy.
            // Prefab/파일 루트를 그대로내면 층이 하나 더 생겨 머지·로컬 값이 어긋납니다.
            GameObject exportRoot = ResolveExportRoot(instance);
            if (exportRoot != instance)
                Debug.Log($"[HumanoidAnimFbxBaker] FBX 최상위: {exportRoot.name} (Max Root_Dummy 구조에 맞춤)");
            else
                Debug.LogWarning("[HumanoidAnimFbxBaker] Root_Dummy를 못 찾아 캐릭터 루트 그대로 보냅니다. Max 구조와 다를 수 있습니다.");

            DisableOtherBehaviours(animator);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.fireEvents = false;
            animator.enabled = true;

            var bakedClips = new List<AnimationClip>(validClips.Count);
            for (int i = 0; i < validClips.Count; i++)
            {
                AnimationClip sourceClip = validClips[i];
                EditorUtility.DisplayProgressBar(
                    "휴머노이드 FBX 굽기",
                    $"굽는 중: {sourceClip.name} ({i + 1}/{validClips.Count})",
                    i / (float)validClips.Count);

                string tempControllerPath = CreateTempController(sourceClip);
                tempControllers.Add(tempControllerPath);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(tempControllerPath);
                if (controller == null || controller.layers.Length == 0 || controller.layers[0].stateMachine.defaultState == null)
                {
                    failed.Add($"{sourceClip.name} (컨트롤러 생성 실패)");
                    bakedClips.Add(null);
                    continue;
                }

                animator.runtimeAnimatorController = controller;
                animator.Rebind();
                animator.Update(0f);

                int stateHash = controller.layers[0].stateMachine.defaultState.nameHash;
                AnimationClip bakedClip = BakeToGenericClip(
                    exportRoot, animator, sourceClip, stateHash,
                    $"굽는 중: {sourceClip.name} ({i + 1}/{validClips.Count})");
                if (bakedClip != null)
                    tempClipPaths.Add(SaveTempClipAsset(bakedClip));
                bakedClips.Add(bakedClip);
            }

            Object.DestroyImmediate(animator);
            var legacyAnim = exportRoot.AddComponent<Animation>();
            legacyAnim.playAutomatically = false;

            var options = new ExportModelOptions
            {
                ExportFormat = ExportFormat.Binary,
                ModelAnimIncludeOption = Include.ModelAndAnim,
                AnimateSkinnedMesh = true,
                ExportUnrendered = true,
                UseMayaCompatibleNames = false,
                EmbedTextures = false,
                KeepInstances = false,
                ObjectPosition = ObjectPosition.LocalCentered
            };

            for (int i = 0; i < validClips.Count; i++)
            {
                AnimationClip sourceClip = validClips[i];
                AnimationClip bakedClip = bakedClips[i];
                if (bakedClip == null)
                    continue;

                string fileName = MakeUniqueFileName(characterFbx.name, sourceClip.name, usedNames);
                string absPath = Path.Combine(folder, fileName);

                if (File.Exists(absPath))
                {
                    if (!askedOverwrite)
                    {
                        overwriteAll = EditorUtility.DisplayDialog(
                            "같은 이름 파일이 있습니다",
                            "이미 있는 FBX를 덮어쓸까요?\n아니오를 누르면 있는 파일은 건너뜁니다.",
                            "덮어쓰기",
                            "건너뛰기");
                        askedOverwrite = true;
                    }

                    if (!overwriteAll)
                    {
                        skipped.Add(fileName);
                        continue;
                    }
                }

                EditorUtility.DisplayProgressBar(
                    "휴머노이드 FBX 굽기",
                    $"저장 중: {fileName} ({i + 1}/{validClips.Count})",
                    0.7f + 0.3f * (i / (float)validClips.Count));

                ClearLegacyClips(legacyAnim);
                legacyAnim.AddClip(bakedClip, bakedClip.name);
                legacyAnim.clip = bakedClip;

                string exported = ModelExporter.ExportObject(absPath, exportRoot, options);
                if (string.IsNullOrEmpty(exported))
                {
                    failed.Add(sourceClip.name);
                    continue;
                }

                TrySetGenericImportSettings(absPath);
                savedPaths.Add(absPath);
                Debug.Log($"[HumanoidAnimFbxBaker] 저장 완료: {absPath}");
            }

            SetStatus(BuildExportSummary(savedPaths, skipped, failed), savedPaths.Count > 0 ? MessageType.Info : MessageType.Warning);
        }
        catch (System.Exception e)
        {
            SetStatus($"오류: {e.Message}", MessageType.Error);
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (instance != null)
                Object.DestroyImmediate(instance);
            for (int i = 0; i < tempControllers.Count; i++)
            {
                if (!string.IsNullOrEmpty(tempControllers[i]))
                    AssetDatabase.DeleteAsset(tempControllers[i]);
            }
            for (int i = 0; i < tempClipPaths.Count; i++)
            {
                if (!string.IsNullOrEmpty(tempClipPaths[i]))
                    AssetDatabase.DeleteAsset(tempClipPaths[i]);
            }
        }
    }

    private static void ClearLegacyClips(Animation legacyAnim)
    {
        AnimationClip[] existing = AnimationUtility.GetAnimationClips(legacyAnim.gameObject);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
                legacyAnim.RemoveClip(existing[i]);
        }
    }

    private static string MakeUniqueFileName(string characterName, string clipName, HashSet<string> usedNames)
    {
        string baseName = SanitizeFileName($"{characterName}_{clipName}");
        string fileName = baseName + ".fbx";
        int suffix = 2;
        while (usedNames.Contains(fileName))
        {
            fileName = $"{baseName}_{suffix}.fbx";
            suffix++;
        }

        usedNames.Add(fileName);
        return fileName;
    }

    private static string BuildExportSummary(List<string> saved, List<string> skipped, List<string> failed)
    {
        var lines = new List<string>();
        if (saved.Count > 0)
            lines.Add($"{saved.Count}개 저장했습니다.");
        if (skipped.Count > 0)
            lines.Add($"건너뜀 {skipped.Count}개: {string.Join(", ", skipped)}");
        if (failed.Count > 0)
            lines.Add($"실패 {failed.Count}개: {string.Join(", ", failed)}");
        if (saved.Count > 0)
        {
            lines.Add("Max에서 이 FBX는 일반 본입니다. 바이패드 .max 에 머지해 쓰세요.");
            lines.Add("최상위는 Root_Dummy 입니다. Max 바이패드 파일과 같은 계층입니다.");
            lines.Add("Max 도구: Tools/Max/BipedFbxMergeTool.ms");
        }
        if (lines.Count == 0)
            return "저장한 파일이 없습니다.";
        return string.Join("\n", lines);
    }

    private static readonly string[] RootDummyNames = { "Root_Dummy", "Root_dummy" };

    /// <summary>
    /// Max .max 와 같게 FBX 최상위를 Root_Dummy로 잡습니다.
    /// 없으면 인스턴스 루트를 그대로 씁니다.
    /// </summary>
    private static GameObject ResolveExportRoot(GameObject instance)
    {
        if (instance == null)
            return null;

        Transform found = FindRootDummy(instance.transform);
        return found != null ? found.gameObject : instance;
    }

    private static Transform FindRootDummy(Transform searchRoot)
    {
        if (searchRoot == null)
            return null;

        for (int i = 0; i < RootDummyNames.Length; i++)
        {
            Transform t = FindNamed(searchRoot, RootDummyNames[i]);
            if (t != null)
                return t;
        }

        return null;
    }

    private static GameObject CreateTempInstance(GameObject source)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
            instance = Object.Instantiate(source);

        instance.name = source.name;
        instance.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
        instance.SetActive(true);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static void DisableOtherBehaviours(Animator keep)
    {
        Behaviour[] behaviours = keep.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == keep)
                continue;
            behaviour.enabled = false;
        }
    }

    private static string CreateTempController(AnimationClip clip)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            AssetDatabase.CreateFolder("Assets", "Editor");
        if (!AssetDatabase.IsValidFolder(TempFolder))
            AssetDatabase.CreateFolder("Assets/Editor", "TempHumanoidFbxBake");

        string path = $"{TempFolder}/_BakeTemp_{System.Guid.NewGuid():N}.controller";
        AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
        return path;
    }

    private static AnimationClip BakeToGenericClip(
        GameObject root,
        Animator animator,
        AnimationClip sourceClip,
        int stateHash,
        string progressLabel)
    {
        float frameRate = GetFrameRate(sourceClip);
        int frameCount = GetFrameCount(sourceClip);
        Transform[] bones = root.GetComponentsInChildren<Transform>(true);
        var skipBone = new bool[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            skipBone[i] = IsFingerOrToeName(bones[i].name);

        var posX = CreateCurveArray(bones.Length);
        var posY = CreateCurveArray(bones.Length);
        var posZ = CreateCurveArray(bones.Length);
        var rotX = CreateCurveArray(bones.Length);
        var rotY = CreateCurveArray(bones.Length);
        var rotZ = CreateCurveArray(bones.Length);
        var rotW = CreateCurveArray(bones.Length);
        var scaleX = CreateCurveArray(bones.Length);
        var scaleY = CreateCurveArray(bones.Length);
        var scaleZ = CreateCurveArray(bones.Length);
        var prevRot = new Quaternion[bones.Length];

        Vector3 freezePos = root.transform.position;
        Quaternion freezeRot = root.transform.rotation;

        Transform com = FindNamed(root.transform, "Bip001");
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null)
            hips = FindNamed(root.transform, "Bip001 Pelvis");

        bool transferHipsToCom = com != null && hips != null && hips != com && hips.IsChildOf(com);
        // 이 프로젝트 바이패드는 Bip001과 Pelvis 월드 Pos가 항상 같음.
        // 휴머노이드가 넣은 Pelvis local Pos 오프셋은 쓰지 않음(아래만 어긋남).
        Vector3 restPelvisLocalPos = Vector3.zero;
        Quaternion restPelvisLocalRot = Quaternion.identity;
        Vector3 restPelvisLocalScale = Vector3.one;
        if (transferHipsToCom)
        {
            restPelvisLocalRot = hips.localRotation;
            restPelvisLocalScale = hips.localScale;
        }

        animator.speed = 1f;

        for (int frame = 0; frame <= frameCount; frame++)
        {
            float time = frame / frameRate;
            if (time > sourceClip.length)
                time = sourceClip.length;

            // 루프 클립이 마지막에서 첫 포즈로 넘어가지 않게 살짝 안쪽으로.
            float sampleTime = Mathf.Min(time, Mathf.Max(0f, sourceClip.length - 1e-4f));
            float normalized = sourceClip.length > 1e-5f ? Mathf.Clamp01(sampleTime / sourceClip.length) : 0f;

            animator.Play(stateHash, 0, normalized);
            animator.Update(0f);
            root.transform.SetPositionAndRotation(freezePos, freezeRot);
            if (transferHipsToCom)
                TransferHipsMotionToCom(com, hips, restPelvisLocalPos, restPelvisLocalRot, restPelvisLocalScale);

            if (frame % 5 == 0)
            {
                EditorUtility.DisplayProgressBar(
                    "휴머노이드 FBX 굽기",
                    $"{progressLabel}  프레임 {frame}/{frameCount}",
                    frame / (float)Mathf.Max(1, frameCount));
            }

            for (int i = 0; i < bones.Length; i++)
            {
                if (skipBone[i])
                    continue;

                Transform bone = bones[i];
                Vector3 p = bone.localPosition;
                Quaternion q = bone.localRotation;
                Vector3 s = bone.localScale;

                if (frame > 0 && Quaternion.Dot(prevRot[i], q) < 0f)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                prevRot[i] = q;

                posX[i].Add(new Keyframe(time, p.x));
                posY[i].Add(new Keyframe(time, p.y));
                posZ[i].Add(new Keyframe(time, p.z));
                rotX[i].Add(new Keyframe(time, q.x));
                rotY[i].Add(new Keyframe(time, q.y));
                rotZ[i].Add(new Keyframe(time, q.z));
                rotW[i].Add(new Keyframe(time, q.w));
                scaleX[i].Add(new Keyframe(time, s.x));
                scaleY[i].Add(new Keyframe(time, s.y));
                scaleZ[i].Add(new Keyframe(time, s.z));
            }
        }

        var baked = new AnimationClip
        {
            name = sourceClip.name,
            legacy = true,
            frameRate = frameRate,
            wrapMode = WrapMode.Once
        };

        for (int i = 0; i < bones.Length; i++)
        {
            if (skipBone[i])
                continue;

            string path = AnimationUtility.CalculateTransformPath(bones[i], root.transform);
            SetTransformCurve(baked, path, "m_LocalPosition.x", posX[i]);
            SetTransformCurve(baked, path, "m_LocalPosition.y", posY[i]);
            SetTransformCurve(baked, path, "m_LocalPosition.z", posZ[i]);
            SetTransformCurve(baked, path, "m_LocalRotation.x", rotX[i]);
            SetTransformCurve(baked, path, "m_LocalRotation.y", rotY[i]);
            SetTransformCurve(baked, path, "m_LocalRotation.z", rotZ[i]);
            SetTransformCurve(baked, path, "m_LocalRotation.w", rotW[i]);
            SetTransformCurve(baked, path, "m_LocalScale.x", scaleX[i]);
            SetTransformCurve(baked, path, "m_LocalScale.y", scaleY[i]);
            SetTransformCurve(baked, path, "m_LocalScale.z", scaleZ[i]);
        }

        ApplyClipLength(baked, sourceClip.length, frameRate);
        return baked;
    }

    private static bool IsFingerOrToeName(string n)
    {
        if (string.IsNullOrEmpty(n))
            return false;
        string lower = n.ToLowerInvariant();
        return lower.Contains("finger") || lower.Contains("toe");
    }

    private static Transform FindNamed(Transform t, string exactName)
    {
        if (t.name == exactName)
            return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform found = FindNamed(t.GetChild(i), exactName);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// 휴머노이드는 몸 이동을 Hips(Pelvis)에 넣습니다. Max 바이패드는 그 값이 Bip001(COM)에 있어야 하므로 옮깁니다.
    /// restPelvisLocalPos는 보통 0입니다. (원본 바이패드에서 COM과 Pelvis 월드 Pos가 같음)
    /// </summary>
    private static void TransferHipsMotionToCom(
        Transform com,
        Transform hips,
        Vector3 restPelvisLocalPos,
        Quaternion restPelvisLocalRot,
        Vector3 restPelvisLocalScale)
    {
        Matrix4x4 hipsWorld = Matrix4x4.TRS(hips.position, hips.rotation, Vector3.one);
        Matrix4x4 restLocal = Matrix4x4.TRS(restPelvisLocalPos, restPelvisLocalRot, restPelvisLocalScale);
        Matrix4x4 comWorld = hipsWorld * restLocal.inverse;

        com.position = comWorld.MultiplyPoint3x4(Vector3.zero);
        com.rotation = comWorld.rotation;
        hips.localPosition = restPelvisLocalPos;
        hips.localRotation = restPelvisLocalRot;
        hips.localScale = restPelvisLocalScale;
    }

    private static void ApplyClipLength(AnimationClip baked, float length, float frameRate)
    {
        baked.frameRate = frameRate;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(baked);
        settings.loopTime = false;
        settings.startTime = 0f;
        settings.stopTime = Mathf.Max(length, 1f / frameRate);
        AnimationUtility.SetAnimationClipSettings(baked, settings);
    }

    private static string SaveTempClipAsset(AnimationClip baked)
    {
        string path = $"{TempFolder}/_BakeClip_{System.Guid.NewGuid():N}.anim";
        AssetDatabase.CreateAsset(baked, path);
        AssetDatabase.SaveAssets();
        return path;
    }

    private static List<Keyframe>[] CreateCurveArray(int count)
    {
        var curves = new List<Keyframe>[count];
        for (int i = 0; i < count; i++)
            curves[i] = new List<Keyframe>(128);
        return curves;
    }

    private static void SetTransformCurve(AnimationClip clip, string path, string property, List<Keyframe> keys)
    {
        var curve = new AnimationCurve(keys.ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }

        var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), property);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static void TrySetGenericImportSettings(string absPath)
    {
        string assetPath = ToProjectPath(absPath);
        if (string.IsNullOrEmpty(assetPath))
            return;

        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            return;

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.animationCompression = ModelImporterAnimationCompression.Off;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.clipAnimations;
        if (clips != null)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }

        importer.SaveAndReimport();
    }

    private static string ToProjectPath(string absPath)
    {
        if (string.IsNullOrEmpty(absPath))
            return null;

        string full = Path.GetFullPath(absPath).Replace('\\', '/');
        string data = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        if (!full.StartsWith(data, System.StringComparison.OrdinalIgnoreCase))
            return null;

        return "Assets" + full.Substring(data.Length);
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');
        return fileName;
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
    }

    private void ClearStatusIfError()
    {
        if (statusType == MessageType.Error)
            statusMessage = null;
    }
}
