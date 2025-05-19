#define REVERSE_Z

using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

[ExecuteInEditMode]
public class VoxelCompute : MonoBehaviour
{

    private Camera cam;
    private List<Vector3> ndcPos = new List<Vector3>();
    private Matrix4x4 InverseVP;
    private Matrix4x4 InverseP;
    private List<Vector3> worldPos = new List<Vector3>();
    private List<Vector3> cameraPos = new List<Vector3>();
    private Vector4 logarithmicDepthDecodingParams;
    private Vector4 logarithmicDepthEncodingParams;
    private float farPlane = 64.0f;
    public bool EncodeDepth = true;
    Vector4 zParam;

    void Awake()
    {
        cam = GetComponent<Camera>();

        
        if(cam)
        {
            /*            ndcPos.Add(new Vector3(1, 1, 1));
                        ndcPos.Add(new Vector3(1, -1, 1));
                        ndcPos.Add(new Vector3(-1, -1, 1));
                        ndcPos.Add(new Vector3(-1, 1, 1));
                        ndcPos.Add(new Vector3(1, 1, -1));
                        ndcPos.Add(new Vector3(1, -1, -1));
                        ndcPos.Add(new Vector3(-1, -1, -1));
                        ndcPos.Add(new Vector3(-1, 1, -1));
                        ndcPos.Add(new Vector3(1, 1, 0));
                        ndcPos.Add(new Vector3(1, -1, 0));
                        ndcPos.Add(new Vector3(-1, -1, 0));
                        ndcPos.Add(new Vector3(-1, 1, 0));*/

            for (int i = 0; i < 9; i++) 
            {
                ndcPos.Add(new Vector3(0, 0, -1.0f + i * 0.25f));
                ndcPos.Add(new Vector3(-1.0f + i * 0.25f, 0, 1));
                ndcPos.Add(new Vector3(0, -1.0f + i * 0.25f, 1));
                ndcPos.Add(new Vector3(-1, 0, -1.0f + i * 0.25f));
                ndcPos.Add(new Vector3(1, 0, -1.0f + i * 0.25f));
                ndcPos.Add(new Vector3(0, 1, -1.0f + i * 0.25f));
                ndcPos.Add(new Vector3(0, -1, -1.0f + i * 0.25f));
            }

            

            float c = 0.5f;
            logarithmicDepthDecodingParams = ComputeLogarithmicDepthDecodingParams(cam.nearClipPlane, farPlane, c);
            logarithmicDepthEncodingParams = ComputeLogarithmicDepthEncodingParams(cam.nearClipPlane, farPlane, c);
            zParam = GetZParam(cam.nearClipPlane, farPlane);

            Matrix4x4 projectionM = Matrix4x4.Perspective(cam.fieldOfView, cam.aspect, cam.nearClipPlane, 64.0f);
            InverseP = projectionM.inverse;
            InverseVP = cam.worldToCameraMatrix.inverse * projectionM.inverse;

            foreach(Vector3 ndc in ndcPos) {
                if(EncodeDepth)
                {
                    worldPos.Add(GetGridCellPosByLog(ndc));
                }
                else
                {
                    worldPos.Add(GetGridCellPosByUniform(ndc));
                }
                
                cameraPos.Add(GetGridCP(ndc));
            }

        }

    }

    // Update is called once per frame
    void Update()
    {
    }

    private void OnDrawGizmos()
    {
        if (cam)
        {
            Matrix4x4 originMatrix = Gizmos.matrix;
            Gizmos.color = Color.red;
            foreach(Vector3 world in worldPos){
                //Gizmos.DrawSphere(world, 0.02f * world.magnitude);
                Gizmos.DrawSphere(world, 0.2f );
            }

            Gizmos.matrix = Matrix4x4.TRS(
                cam.transform.position,
                cam.transform.rotation,
                Vector3.one);

            Gizmos.DrawFrustum(Vector3.zero,cam.fieldOfView,64.0f,cam.nearClipPlane,cam.aspect);
/*            Gizmos.color = Color.cyan;
            foreach (Vector3 camp in cameraPos)
            {
                Gizmos.DrawCube(new Vector3(0.0f,0.0f,-camp.z), new Vector3(5.0f, 5.0f, 0.05f));
            }*/
            Gizmos.matrix = originMatrix;

        }
        
    }

    //采用对数编码深度的方式
    Vector3 GetGridCellPosByLog(Vector3 ndcPos)
    {
        float viewZ = DecodeLogarithmicDepthGeneralized((ndcPos.z + 1.0f)/2.0f, logarithmicDepthDecodingParams);
#if REVERSE_Z
        
        float devZ = (1-EyeDepthToProj(viewZ, zParam)) * 2 - 1;
#else
        float devZ = EyeDepthToProj(viewZ, zParam) * 2 - 1;
#endif
        Vector4 positionWS = InverseVP * new Vector4(ndcPos.x,ndcPos.y, devZ,1.0f);
        positionWS /= positionWS.w;
        return new Vector3(positionWS.x,positionWS.y,positionWS.z);
    }

    //采样相机空间均匀采样
    Vector3 GetGridCellPosByUniform(Vector3 ndcPos)
    {

        Vector4 positionVS;
#if REVERSE_Z
        positionVS.z = -(ndcPos.z + 1.0f)/ 2.0f * (farPlane - cam.nearClipPlane) - cam.nearClipPlane;
#else
        positionVS.z = (ndcPos.z + 1.0f)/ 2.0f * (farPlane - cam.nearClipPlane) + cam.nearClipPlane;
#endif
        positionVS.x = ndcPos.x * (math.abs(positionVS.z) * math.tan((cam.fieldOfView / 2.0f) * Mathf.Deg2Rad)) * cam.aspect;
        positionVS.y = ndcPos.y * (math.abs(positionVS.z) * math.tan((cam.fieldOfView / 2.0f) * Mathf.Deg2Rad));
        positionVS.w = 1.0f;
        Vector4 positionWS = cam.cameraToWorldMatrix * positionVS;
        return new Vector3(positionWS.x,positionWS.y,positionWS.z);
    }

    Vector3 GetGridCP(Vector3 ndcPos)
    {
        //float viewZ = DecodeLogarithmicDepthGeneralized(ndcPos.z, logarithmicDepthDecodingParams);
        //float devZ = EyeDepthToProj(viewZ, zParam) * 2 - 1;
        Vector4 positionWS = InverseP * new Vector4(ndcPos.x, ndcPos.y, ndcPos.z, 1.0f);
        positionWS /= positionWS.w;
        return new Vector3(positionWS.x, positionWS.y, positionWS.z);
    }

    //定义一些辅助计算的通用函数
    //'z' is the view space Z position (linear depth).
    //saturate(z) the output of the function to clamp them to the [0, 1] range.
    //d = log2(c * (z - n) + 1) / log2(c * (f - n) + 1)
    //  = log2(c * (z - n + 1/c)) / log2(c * (f - n) + 1)
    //  = log2(c) / log2(c * (f - n) + 1) + log2(z - (n - 1/c)) / log2(c * (f - n) + 1)
    //  = E + F * log2(z - G)
    //encodingParams = { E, F, G, 0 }
    //'d' is the logarithmically encoded depth value.
    //saturate(d) to clamp the output of the function to the [n, f] range.
    //z = 1/c * (pow(c * (f - n) + 1, d) - 1) + n
    //  = 1/c * pow(c * (f - n) + 1, d) + n - 1/c
    //  = 1/c * exp2(d * log2(c * (f - n) + 1)) + (n - 1/c)
    //  = L * exp2(d * M) + N
    //decodingParams = { L, M, N, 0 }
    //Graph: https://www.desmos.com/calculator/qrtatrlrba
    float DecodeLogarithmicDepthGeneralized(float d, float4 decodingParams)
    {
        return decodingParams.x * math.exp2(d * decodingParams.y) + decodingParams.z;
    }

    float EyeDepthToProj(float z, float4 ZBufferParams)
    {
        return (1 / z - ZBufferParams.w) / ZBufferParams.z;
    }

    //一些工具函数
    //farPlane是自定义的雾效最远距离 
    // See EncodeLogarithmicDepthGeneralized().
    static Vector4 ComputeLogarithmicDepthEncodingParams(float nearPlane, float farPlane, float c)
    {
        Vector4 depthParams = new Vector4();

        float n = nearPlane;
        float f = farPlane;

        depthParams.y = 1.0f / Mathf.Log(c * (f - n) + 1, 2);
        depthParams.x = Mathf.Log(c, 2) * depthParams.y;
        depthParams.z = n - 1.0f / c; // Same
        depthParams.w = 0.0f;

        return depthParams;
    }
    //farPlane是自定义的雾效最远距离 
    // See DecodeLogarithmicDepthGeneralized().
    static Vector4 ComputeLogarithmicDepthDecodingParams(float nearPlane, float farPlane, float c)
    {
        Vector4 depthParams = new Vector4();

        float n = nearPlane;
        float f = farPlane;

        depthParams.x = 1.0f / c;
        depthParams.y = Mathf.Log(c * (f - n) + 1, 2);
        depthParams.z = n - 1.0f / c; // Same
        depthParams.w = 0.0f;

        return depthParams;
    }
    //farClip是自定义的雾效最远距离 
    static Vector4 GetZParam(float nearClip, float farClip)
    {
        bool reZ = SystemInfo.usesReversedZBuffer;
        Vector4 zParam;
        if (reZ)
        {
            zParam.x = -1 + farClip / nearClip;
            zParam.y = 1;
            zParam.z = zParam.x / farClip;
            zParam.w = 1 / farClip;
        }
        else
        {
            zParam.x = 1 - farClip / nearClip;
            zParam.y = farClip / nearClip;
            zParam.z = zParam.x / farClip;
            zParam.w = zParam.y / farClip;
        }
        return zParam;
    }
}
