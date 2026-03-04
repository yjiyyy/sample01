using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하나의 스테이지 정보를 담는 데이터입니다.
/// </summary>
[Serializable]
public class StageInfo
{
    [Tooltip("Build Settings에 등록된 씬 이름 (예: Stage01)")]
    public string sceneName;

    [Tooltip("UI 버튼에 표시할 이름 (예: 스테이지 1)")]
    public string displayName;

    [Tooltip("선택 UI에 표시할 썸네일 (선택 사항)")]
    public Sprite thumbnail;
}

/// <summary>
/// 게임 내에서 선택 가능한 스테이지들의 목록입니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Stage List", fileName = "StageList")]
public class StageListSO : ScriptableObject
{
    [Tooltip("게임 내에서 선택 가능한 스테이지 리스트")]
    public List<StageInfo> stages = new List<StageInfo>();
}

