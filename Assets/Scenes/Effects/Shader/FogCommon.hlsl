// ShaderTools.hlsl - 存放Shader通用工具函数
#ifndef FOGCOMMON_HLSL
#define FOGCOMMON_HLSL // 防止重复包含

// 工具函数1：将RGB颜色转换为灰度值
float RGBToGrayscale(float3 rgbColor)
{
    // 标准灰度转换公式：Y = 0.299R + 0.587G + 0.114B
    return dot(rgbColor, float3(0.299, 0.587, 0.114));
}




// 常量定义（可选）

#endif