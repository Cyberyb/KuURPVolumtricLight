using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class VolumeHelper
{
    /** [ Halton 1964, "Radical-inverse quasi-random point sequence" ] */
    public static float Halton(int Index, int Base)
    {
        float Result = 0.0f;
        float InvBase = 1.0f / (float)Base;
        float Fraction = InvBase;
        while (Index > 0)
        {
            Result += (float)(Index % Base) * Fraction;
            Index /= Base;
            Fraction *= InvBase;
        }
        return Result;
    }
}