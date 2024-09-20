using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

public class MoveScript : MonoBehaviour
{
    public float speed = 0.02f;
    public float range = 1f;
    private Vector3 startPos;
    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = startPos + new Vector3(Mathf.Sin(Time.time * 6f * speed) * range, Mathf.Cos(Time.time * 6f * speed) * range, 0);
    }
}
