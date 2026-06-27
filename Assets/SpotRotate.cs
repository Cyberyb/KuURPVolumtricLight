using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpotRotate : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 45f; // 旋转速度（度/秒）

    void Update()
    {
        // 获取主摄像机
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Vector3 cameraPos = mainCamera.transform.position;
        Vector3 lightPos = transform.position;
        
        // 计算从光源指向摄像机的方向作为旋转轴
        Vector3 rotationAxis = new Vector3(0,0,1 ); // 默认绕Z轴旋转
        
        // 计算本帧的旋转角度
        float rotationAngle = rotationSpeed * Time.deltaTime;
        
        // 绕轴旋转光源的朝向（就像时钟指针在转动）
        Quaternion rotation = Quaternion.AngleAxis(rotationAngle, rotationAxis);
        transform.rotation = rotation * transform.rotation;
    }
}
