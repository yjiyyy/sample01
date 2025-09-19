using System;

[System.Flags]
public enum SuperArmorSource
{
    None = 0,
    Shield = 1 << 0,
    Rush = 1 << 1,
    Skill = 1 << 2, // 확장 대비
}