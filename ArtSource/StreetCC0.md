# 石桥路沿街建筑资产

四组建筑来自 tevy，均使用 CC0 1.0。构建导入固定版本 LOD0 的几何与贴图。

| 建筑用途 | 原始模型 |
| --- | --- |
| 后场工具棚 | [Tool Shed v2](https://tevy.dev/models/tool-shed) · [固定版本 GLB](https://tevy.dev/models/tool-shed/v2/lod0.glb) |
| 石桥路面馆 | [Noodle Shop v1](https://tevy.dev/models/noodle-shop) · [固定版本 GLB](https://tevy.dev/models/noodle-shop/v1/lod0.glb) |
| 转角三层商铺 | [Corner Shop Block v2](https://tevy.dev/models/corner-shop-block) · [固定版本 GLB](https://tevy.dev/models/corner-shop-block/v2/lod0.glb) |
| 值守点与街尾商住楼 | [Mixed Tenant Block v1](https://tevy.dev/models/mixed-tenant-block) · [固定版本 GLB](https://tevy.dev/models/mixed-tenant-block/v1/lod0.glb) |

解码模型和贴图后，使用 Blender 5.1 和仓库的 [FBX 导出脚本](../Tools/export_public_props.py) 导入 Unity。粗糙度、金属度与环境遮蔽按项目的 Standard 材质通道打包。照明、碰撞、落脚面和街边可通行的雨棚在 Unity 中组装。
