using System;

[Flags]
public enum SuperArmorSource
{
    None = 0,
    Attack = 1 << 0,  // 공격 패턴 중 부여
    Shield = 1 << 1,  // 실드 보유
    Skill = 1 << 2,  // 향후 스킬/버프
}