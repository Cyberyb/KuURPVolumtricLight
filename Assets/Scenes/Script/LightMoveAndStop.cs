using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightMoveAndStop : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private Vector3 moveDirection = Vector3.right;
    [SerializeField] private float moveDistance = 2f;
    [SerializeField] private float moveSpeed = 1f;

    [Header("Capture Settings")]
    [SerializeField] private bool captureOnComplete = true;
    [SerializeField] private string captureFolderName = "Captures";
    [SerializeField] private string captureFilePrefix = "LightMove";

    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private bool _isMoving;
    private bool _movingToTarget;

    // Start is called before the first frame update
    void Start()
    {
        _startPosition = transform.position;
        UpdateTargetPosition();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            StartMoveCycle();
        }

        if (!_isMoving)
        {
            return;
        }

        Vector3 destination = _movingToTarget ? _targetPosition : _startPosition;
        transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, destination) <= 0.0001f)
        {
            transform.position = destination;
            if (_movingToTarget)
            {
                _movingToTarget = false;
            }
            else
            {
                _isMoving = false;
                if (captureOnComplete)
                {
                    StartCoroutine(CaptureFrame());
                }
            }
        }
    }

    private void StartMoveCycle()
    {
        _startPosition = transform.position;
        UpdateTargetPosition();
        _movingToTarget = true;
        _isMoving = true;
    }

    private void UpdateTargetPosition()
    {
        Vector3 normalizedDirection = moveDirection.sqrMagnitude > 0.0001f
            ? moveDirection.normalized
            : Vector3.right;
        _targetPosition = _startPosition + normalizedDirection * Mathf.Max(0f, moveDistance);
    }

    private IEnumerator CaptureFrame()
    {
        yield return new WaitForEndOfFrame();

        string folderPath = System.IO.Path.Combine(Application.persistentDataPath, captureFolderName);
        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
        }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
        string fileName = $"{captureFilePrefix}_{timestamp}.png";
        string filePath = System.IO.Path.Combine(folderPath, fileName);
        ScreenCapture.CaptureScreenshot(filePath);
        Debug.Log($"Capture saved to: {filePath}");
    }
}
