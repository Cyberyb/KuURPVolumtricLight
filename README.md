# KuURPVolumetricFog 体积雾

KuURPVolumetricFog 是一个基于 Unity URP 的体积雾示例工程，用于学习和验证屏幕空间 / Froxel 体积雾、体积散射、时间重投影、降采样与模糊等渲染流程。

当前项目基于：

- Unity：2022.3.62f3c1
- Universal Render Pipeline：14.0.12
- 主要示例场景：`Assets/Scenes/VolumetricLight.unity`
- 体积雾核心资源：`Assets/Scenes/Effects/VolumeLight`

## 快速开始

1. 使用 Unity Hub 打开本项目根目录。
2. 建议使用 Unity `2022.3.62f3c1` 或同一 LTS 系列版本打开项目。
3. 首次打开时等待 Unity 导入资源和编译脚本。
4. 打开示例场景：

   `Assets/Scenes/VolumetricLight.unity`

5. 点击 Play，即可查看项目内配置好的体积雾效果。

如果场景效果没有出现，请先确认当前 Quality / Graphics 设置正在使用 URP 管线资源，并检查 Renderer 中是否启用了 `KuRendererFeature`。

## 项目结构

```text
Assets/
  Scenes/
    VolumetricLight.unity              示例体积雾场景
    Effects/
      VolumeLight/
        VolumetricFogRenderPass.cs     体积雾主渲染 Pass
        VolumetricShadowRenderPass.cs  体积阴影相关 Pass
        VolumeLight_Volume.cs          Volume 面板参数定义
        ComputeVolume.compute          体积雾计算着色器
        VolumetricFog.shader           体积雾合成着色器
        BlueNoise470Dithering.png      抖动 / 降噪纹理
        VolumeLight_VolumeProfile.asset 示例 Volume Profile
    KutoryURP/
      KuRendererFeature.cs             将自定义 RenderPass 注入 URP
      KuRenderPass.cs                  自定义 Pass 基类
  Settings/
    URP-HighFidelity-Renderer.asset    已配置体积雾 Render Feature 的 Renderer
```

## 在 Unity 中使用体积雾

### 1. 确认 URP Renderer Feature

打开：

`Assets/Settings/URP-HighFidelity-Renderer.asset`

在 Renderer Features 中确认 `KuRendererFeature` 已启用，并且列表中包含：

- `RenderPassName`：`VolumetricFogRenderPass`
- `Shader`：`VolumetricFog.shader`
- `ComputeShader`：`ComputeVolume.compute`
- `Active`：启用

`KuRendererFeature` 会在运行时通过 `RenderPassName` 创建对应的自定义渲染 Pass，并将其插入 URP 渲染流程。

### 2. 添加或检查 Volume

体积雾参数通过 Unity Volume 系统控制。你可以直接使用示例资源：

`Assets/Scenes/Effects/VolumeLight/VolumeLight_VolumeProfile.asset`

也可以在自己的场景中创建：

1. 新建一个全局 Volume。
2. 创建或指定一个 Volume Profile。
3. 添加 Override：`Ku_PostProcessing / VolumeLight`。
4. 调整体积雾参数。

常用参数说明：

- `Light Intensity`：控制体积光 / 雾的整体亮度。
- `Step Times`：控制采样步数，越高越细腻，但性能开销越大。
- `Down Sample`：降低体积雾计算分辨率，用于提升性能。
- `Phase G`：控制散射方向性。
- `Extinction`：控制光在雾中的衰减。
- `Grid Pixel Size`、`Grid Size Z`：控制 Froxel 网格精度。
- `Use Froxel`：启用基于 Froxel 的体积雾计算。
- `Use Temporal Reproject`：启用时间重投影，降低闪烁和噪点。
- `Global Fog Density`：控制全局雾密度。
- `Fog Base Height`、`Height Fall Off`：控制高度雾分布。

### 3. 在自己的场景中接入

如果要把效果迁移到新场景，建议按这个顺序检查：

1. 场景相机使用 URP 渲染。
2. 当前 URP Asset 指向包含 `KuRendererFeature` 的 Renderer。
3. 场景中存在 Global Volume，并挂载 `VolumeLight` Override。
4. 场景中有 Directional Light 或其它光源用于观察散射效果。
5. Volume 参数中的 `Light Intensity` 或 `Step Times` 不为 0，否则效果会被判定为未启用。

## 调试建议

- 如果画面没有雾，优先检查 `KuRendererFeature` 是否启用。
- 如果 Scene View 有效果但 Game View 没有效果，检查相机和 URP Renderer 设置。
- 如果效果过暗，提升 `Light Intensity`、`Global Fog Density` 或光源强度。
- 如果性能较低，增大 `Down Sample`，降低 `Step Times` 或降低 `Grid Size Z`。
- 如果画面噪点明显，检查 `BlueNoise470Dithering.png` 是否正确绑定，并尝试启用 `Use Temporal Reproject`。

## 主要实现入口

- `VolumetricFogRenderPass.cs`：体积雾主流程，负责准备渲染纹理、分发 Compute Shader、执行散射和合成。
- `ComputeVolume.compute`：体积雾计算核心，包含散射、积分、降采样等计算 Kernel。
- `VolumetricFog.shader`：将体积雾结果合成到屏幕颜色。
- `VolumeLight_Volume.cs`：定义 Inspector 中可调的 Volume 参数。
- `KuRendererFeature.cs`：将自定义 Pass 注册到 URP 渲染管线。

## 注意事项

- 本项目更偏向渲染实验和学习示例，而不是开箱即用的生产插件。
- 推荐先从 `VolumetricLight.unity` 示例场景理解参数，再迁移到自己的场景。
- 修改 Compute Shader 或 Render Pass 后，需要等待 Unity 重新编译脚本和着色器。
