using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class LocalFog
{
    public Vector3 center;
    public Vector3 extent;
    public float density;
    public float extinction;
    public Vector3 albedo;
    public Matrix4x4 worldToLocalMatrix;

        public LocalFog(Vector3 center, Vector3 extent, float density, float extinction, Vector3 albedo, Matrix4x4 worldToLocalMatrix)
        {
            this.center = center;
            this.extent = extent;
            this.density = density;
            this.extinction = extinction;
            this.albedo = albedo;
            this.worldToLocalMatrix = worldToLocalMatrix;
        }
}
