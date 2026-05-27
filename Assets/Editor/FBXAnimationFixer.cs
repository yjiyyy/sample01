using UnityEditor;
using UnityEngine;

public class FBXAnimationFixer : Editor
{
    private const string WeaponMaskPath = "Assets/Arts/Characters/Ani_Player/Mask_Base_Weapon.mask";

    [MenuItem("Assets/Fix FBX Animation Settings", priority = 2000)]
    public static void FixSettings()
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);
        AvatarMask targetMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(WeaponMaskPath);

        if (targetMask == null)
        {
            Debug.LogError($"[FBX Fix 실패] AvatarMask를 찾을 수 없습니다: {WeaponMaskPath}");
            return;
        }

        int fixedCount = 0;

        foreach (Object obj in selectedObjects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer != null)
            {
                // 1. 애니메이션 압축 설정 (이미지 피드백 반영)
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                importer.animationRotationError = 0.1f;
                importer.animationPositionError = 0.1f;
                importer.animationScaleError = 0.1f;

                // 2. 루트 모션 및 Bake Into Pose 설정
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                if (clips.Length == 0) clips = importer.clipAnimations;
                if (clips.Length == 0) continue;

                for (int i = 0; i < clips.Length; i++)
                {
                    // Bake Into Pose 체크
                    clips[i].lockRootRotation = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].lockRootPositionXZ = true;
                    
                    // Based Upon을 Original로 설정
                    clips[i].keepOriginalOrientation = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].keepOriginalPositionXZ = true;

                    // 마스크를 외부 AvatarMask로 통일
                    clips[i].maskType = ClipAnimationMaskType.CopyFromOther;
                    clips[i].maskSource = targetMask;
                }

                importer.clipAnimations = clips;
                
                // 변경 사항 저장 및 재임포트
                importer.SaveAndReimport();
                fixedCount++;
                Debug.Log($"[FBX Fix 완료] {path}");
            }
        }

        Debug.Log($"[FBX Fix] 총 {fixedCount}개 FBX 처리 완료");
    }

    [MenuItem("Assets/Fix FBX Animation Settings", true)]
    public static bool ValidateFixSettings()
    {
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetImporter.GetAtPath(path) is ModelImporter)
            {
                return true;
            }
        }

        return false;
    }
}