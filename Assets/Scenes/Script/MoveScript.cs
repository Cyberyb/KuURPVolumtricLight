using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    public float moveSpeed = 10f;        // 移动速度
    public float rotationSpeed = 100f;  // 旋转速度
    public float boostMultiplier = 2f;  // 加速倍数

    private float yaw = 0f;             // 水平旋转
    private float pitch = 0f;           // 垂直旋转

    void Update()
    {
        // 获取鼠标输入控制摄像机旋转
        if (Input.GetMouseButton(1)) // 按住鼠标右键旋转
        {
            yaw += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -90f, 90f); // 限制垂直角度
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }

        // 获取键盘输入控制摄像机移动
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) // 按住Shift加速
        {
            speed *= boostMultiplier;
        }

        Vector3 move = new Vector3(
            Input.GetAxis("Horizontal"),
            0,
            Input.GetAxis("Vertical")
        );

        if (Input.GetKey(KeyCode.Q)) move.y -= 1; // 向下
        if (Input.GetKey(KeyCode.E)) move.y += 1; // 向上

        transform.Translate(move * speed * Time.deltaTime, Space.Self);
    }
}
