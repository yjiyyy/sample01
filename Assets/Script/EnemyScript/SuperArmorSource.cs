using System;

[System.Flags]
public enum SuperArmorSource
{
    None = 0,
    Shield = 1 << 0,
    Rush = 1 << 1,
    Attack = 1 << 2, // ★ 추가!
    // 필요시 다른 소스도 여기에 추가
}