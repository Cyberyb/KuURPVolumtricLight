using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointLightMove : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;          // 移动速度
    [SerializeField] private float moveDistance = 2f;       // 移动距离（上下各移动的量）
    
    private Vector3 initialPosition;                         // 初始位置

    // Start is called before the first frame update
    void Start()
    {
        // 记录初始位置
        initialPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        // 使用正弦波实现上下平滑移动
        float yOffset = Mathf.Sin(Time.time * moveSpeed) * moveDistance;
        transform.position = initialPosition + Vector3.up * yOffset;
    }
}
