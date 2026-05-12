<div align=center>
  <img src="ProjectPSE.png" alt="" width="128">
</div>

<h2 align="center">
Project : Power Systems Engineer
</h2>

<div align="center">
✨该项目是作为大学学期考核的一部分而开发的😊
</div>
<div align="center">
💖该游戏使用 Unity 6 开发，并采用了通用渲染管线（URP）😎
</div>

<div align="center">

[English README](README.md) | [中文 README](README_zh.md)

</div>



## 安装

| Windows | [GitHub Releases](https://github.com/jin6yang/ProjectPowerSystemsEngineer/releases) |
| ------- | ------------------------------------------------------------ |

> [!TIP]
>
> macOS 可以使用 CrossOver
>
> Linux 推荐直接安装 SteamOS，或者使用 `Proton`



## 构建

### 开发工具及环境（前提条件）

Unity 6.3

IDE（推荐使用 Visual Studio 2026）

如果使用 Visual Studio 2026 ，需要
“.NET 桌面开发” 和 “使用Unity的游戏开发” 工作负荷

如果使用 Visual Studio Code ，需要
[Unity - Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=VisualStudioToolsForUnity.vstuc)

如果使用 Rider ，可以直接使用，但是推荐更新至最新版本。

### 构建

1. 使用`git`命令克隆项目，或者直接下载压缩包（选择 Code → Download ZIP）

   ```shell
   git clone https://github.com/jin6yang/ProjectPowerSystemsEngineer
   ```

2. 启动 Unity Hub，导航至项目页面，选择“添加”，再选择“从磁盘添加项目”，从弹出的窗口中导航至目录并选择项目文件夹 `ProjectPowerSystemsEngineer`

3. 接下来，如果 Unity Hub 弹出来版本警告，请选择已安装的 Unity 6.3 (6000.3.13f1) 版本并选择对应平台（建议选择当前平台）并继续。

4. 等待 Unity 加载完毕即可使用引擎构建项目。

   如需直接在编辑器中运行，请点击开始按钮。

   如需要构建，请选择 File → Build And Run (请确保 Build Profiles 配置正确)



## 开场动画和主菜单动画

本项目的动画使用了 `Python` 库: [Manim Community](https://www.manim.community/)

动画项目开源地址: [jin6yang/PSE-Animation](https://github.com/jin6yang/PSE-Animation)



## 第三方资源

感谢以下免费的 Unity Asset Store 资源！

[Skybox Series Free | 2D Sky | Unity Asset Store](https://assetstore.unity.com/packages/2d/textures-materials/sky/skybox-series-free-103633)

[Simple Water Shader URP | 2D Water | Unity Asset Store](https://assetstore.unity.com/packages/2d/textures-materials/water/simple-water-shader-urp-191449)



## 游玩手册

EN: [Player Guide](Player Guide.md)

ZH: [游玩手册](游玩手册.md)



## 许可证

自定义 [LICENSE](LICENSE.txt)

