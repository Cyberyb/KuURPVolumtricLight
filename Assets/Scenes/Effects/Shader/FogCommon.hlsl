// ShaderTools.hlsl - 存放Shader通用工具函数
#ifndef FOGCOMMON_HLSL
#define FOGCOMMON_HLSL // 防止重复包含

// 工具函数1：将RGB颜色转换为灰度值
float RGBToGrayscale(float3 rgbColor)
{
    // 标准灰度转换公式：Y = 0.299R + 0.587G + 0.114B
    return dot(rgbColor, float3(0.299, 0.587, 0.114));
}

uint4 Rand4DPCG32(int4 p)
{
	// taking a signed int then reinterpreting as unsigned gives good behavior for negatives
	uint4 v = uint4(p);

	// Linear congruential step.
	v = v * 1664525u + 1013904223u;

	// shuffle
	v.x += v.y*v.w;
	v.y += v.z*v.x;
	v.z += v.x*v.y;
	v.w += v.y*v.z;

	// xoring high bits into low makes all 32 bits pretty good
	v ^= (v >> 16u);

	// final shuffle
	v.x += v.y*v.w;
	v.y += v.z*v.x;
	v.z += v.x*v.y;
	v.w += v.y*v.z;

	return v;
}

float4 Rand4DPCG32_01(int4 p)
{
    uint4 rand = Rand4DPCG32(p);
    return float4(rand) / 4294967295.0f;
}


// 常量定义（可选）

#endif