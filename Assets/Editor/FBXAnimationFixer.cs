using UnityEditor;
using UnityEngine;

public class FBXAnimationFixer : Editor
{
    [MenuItem("Assets/Fix FBX Animation Settings")]
    public static void FixSettings()
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);

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
                }

                importer.clipAnimations = clips;
                
                // 변경 사항 저장 및 재임포트
                importer.SaveAndReimport();
                Debug.Log($"[최적화 완료] {path} (Compression: Optimal / BakeIntoPose: ON)");
            }
        }
    }
}