# 奶娃浴场：打烊后

*Milk Frog’s Bathhouse: After Hours* 是一款开发中的第一人称恐怖游戏。这是独立的 Unity 工程。项目现已包含恐怖奶娃角色源文件、首夜关卡设计与可在编辑器中查看的全场景白盒。

[查看第一夜总场景设计、四层平面图与室外路线](Docs/World/first-night.md)。

## 打开工程

安装 Git LFS 后克隆本仓库，在仓库根目录执行 `git lfs pull`。在 Unity Hub 中添加仓库根目录，使用 Unity 6000.3.29f1 打开。Unity 会在首次打开时建立本地导入缓存。

Unity 资源放在 `Assets/`。Blender 源工程、原尺寸贴图和参考图放在 `ArtSource/`，依据制作阶段归档。协作流程见 [贡献指南](CONTRIBUTING.md)。

## 角色美术

用 Blender 5.1 打开[最新全身工程](ArtSource/HorrorMilkFrog/haunted-fullbody-skin-03/Horror-Milk-Frog-03.blend)。模型已打包贴图，旁边保留 [4096 像素肤色图](ArtSource/HorrorMilkFrog/haunted-fullbody-skin-03/Skin-albedo-4096.png)与材质源图。[八向对照图](ArtSource/HorrorMilkFrog/haunted-fullbody-skin-03/Eight-views.png)可直接预览造型。

| 阶段 | 内容 |
| --- | --- |
| [头部研究](ArtSource/HorrorMilkFrog/haunted-head-study-01) | 头雕、表情参考、转台视频及逐帧渲染 |
| [全身初稿](ArtSource/HorrorMilkFrog/haunted-fullbody-study-01) | 身形、八向参考和捧腹大笑姿态 |
| [表面修整](ArtSource/HorrorMilkFrog/haunted-fullbody-surface-02) | 轮廓修整、肩部对比与多角度渲染 |
| [皮肤与材质](ArtSource/HorrorMilkFrog/haunted-fullbody-skin-03) | 皮肤、口腔、虹膜、材质生成提示词与全身定稿候选 |

美术制作文件位于 `ArtSource/`。后续供游戏使用的导出模型和材质放进 `Assets/`，由 Unity 生成并跟踪对应的 `.meta`。
