using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightShadowCompute : MonoBehaviour
{
    public Light mainLight; // 目标光源（平行光/点光/聚光）
    public Renderer targetRenderer; // 需要计算距离的物体

    void Update()
    {
        if (mainLight == null || targetRenderer == null) return;
        
        // 步骤1：构建光源的视图矩阵（世界→光源视角）
        Matrix4x4 lightViewMatrix = mainLight.transform.worldToLocalMatrix;
        // 步骤2：构建光源的投影矩阵
        Matrix4x4 lightProjMatrix;
        if (mainLight.type == LightType.Directional)
        {
            // 平行光：正交投影（URP默认阴影投影参数）
            float shadowDistance = QualitySettings.shadowDistance;
            lightProjMatrix = Matrix4x4.Ortho(-shadowDistance, shadowDistance, -shadowDistance, shadowDistance, 0.1f, shadowDistance);
        }
        else if (mainLight.type == LightType.Spot)
        {
            // 聚光：透视投影
            lightProjMatrix = Matrix4x4.Perspective(mainLight.spotAngle, 1f, 0.1f, mainLight.range);
        }
        else
        {
            // 点光：透视投影（简化，实际需立方体贴图）
            lightProjMatrix = Matrix4x4.Perspective(90f, 1f, 0.1f, mainLight.range);
        }
        
        // 步骤3：合并视图矩阵和投影矩阵（世界→光源裁剪空间）
        Matrix4x4 lightVP = lightProjMatrix * lightViewMatrix;

        Debug.Log("Light VP Matrix:\n" + lightVP);
        // 传递到着色器
        targetRenderer.material.SetMatrix("_LightVP", lightVP);
    }
}
