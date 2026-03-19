using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeLocalFog : MonoBehaviour
{
    [SerializeField] private float density = 0.1f;
    [SerializeField] private float extinction = 1f;
    [SerializeField] private Vector3 albedo = Vector3.one;
    [SerializeField] private bool showMatrixDebug = true; // 是否在Game视图中显示矩阵信息

    private LocalFog localFog;
    private Collider fogCollider;

    // Start is called before the first frame update
    void Start()
    {
        fogCollider = GetComponent<Collider>();
        
        if (fogCollider == null)
        {
            Debug.LogError("CubeLocalFog: 物体上没有找到Collider组件");
            return;
        }

        // 从碰撞盒获取中心和范围
        Bounds bounds = fogCollider.bounds;
        Vector3 center = bounds.center;
        Vector3 extent = bounds.size * 0.5f; // extent 是从中心到边界的距离

        // 获取世界空间到本地空间的变换矩阵
        Matrix4x4 worldToLocalMatrix = transform.worldToLocalMatrix;

        // 创建LocalFog对象
        localFog = new LocalFog(center, extent, density, extinction, albedo, worldToLocalMatrix);
    }

    // Update is called once per frame
    void Update()
    {
        // 如果Inspector中修改了参数，实时更新LocalFog
        if (localFog != null)
        {
            localFog.density = density;
            localFog.extinction = extinction;
            localFog.albedo = albedo;
            localFog.worldToLocalMatrix = transform.worldToLocalMatrix;
        }
    }

    // 在Game视图中显示worldToLocalMatrix矩阵信息
    void OnGUI()
    {
        if (!showMatrixDebug)
            return;

        GUILayout.BeginArea(new Rect(10, 10, 400, 300));
        GUILayout.Label("WorldToLocalMatrix Info", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
        
        if (localFog != null)
        {
            Matrix4x4 matrix = localFog.worldToLocalMatrix;
            
            GUILayout.Label($"[0,0]={matrix.m00:F4} [0,1]={matrix.m01:F4} [0,2]={matrix.m02:F4} [0,3]={matrix.m03:F4}");
            GUILayout.Label($"[1,0]={matrix.m10:F4} [1,1]={matrix.m11:F4} [1,2]={matrix.m12:F4} [1,3]={matrix.m13:F4}");
            GUILayout.Label($"[2,0]={matrix.m20:F4} [2,1]={matrix.m21:F4} [2,2]={matrix.m22:F4} [2,3]={matrix.m23:F4}");
            GUILayout.Label($"[3,0]={matrix.m30:F4} [3,1]={matrix.m31:F4} [3,2]={matrix.m32:F4} [3,3]={matrix.m33:F4}");
        }
        
        GUILayout.EndArea();
    }

    // 获取LocalFog对象的方法
    public LocalFog GetLocalFog()
    {
        return localFog;
    }
}
