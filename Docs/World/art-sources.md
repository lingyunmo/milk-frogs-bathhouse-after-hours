# 第一夜美术资产

场景优先使用允许在公开仓库再分发的现成模型和材质。所有本批第三方资产均为 CC0。Unity 保留源贴图尺寸，并导入法线、粗糙度、金属度和遮蔽图中适用的部分。

| 位置 | 使用的资产 |
| --- | --- |
| 大浴池、淋浴间和洗衣房 | tevy 的浴缸、洗衣机和坐便器。盥洗室的回水任务使用 tevy 洗手盆。 |
| 茶座备餐区 | tevy 的水槽底柜、抽屉柜和壁柜，搭配 Poly Haven 的茶具、木桌和圆凳。 |
| 前厅、休息室和宿舍 | Poly Haven 的金属办公桌、电视、更衣长凳、Sofa 02、Vintage Day Bed 和金属货架。沙发的木脚按下方藏点的净空调整。 |
| 地下设备间 | Poly Haven 的 Portable Generator、Tool Cart 和 Worn Metal Rack。实机维修仍使用原来面向玩家的操作点。 |
| 室外 | tevy 的微型面包车，Poly Haven 的卷帘窗和金属废物桶。 |
| 地面、墙面和路面 | ambientCG Tiles074、Concrete034、Asphalt017，及 Poly Haven 的 Square Tiles、Painted Concrete 02、Brown Planks 05、Brick Moss 001。 |

- [tevy 模型目录及许可](https://tevy.dev/models) · [浴缸](https://tevy.dev/models/bathtub) · [备餐水槽](https://tevy.dev/models/kitchen-base-unit-sink)
- [Poly Haven 模型与材质许可](https://polyhaven.com/license) · [Sofa 02](https://polyhaven.com/a/sofa_02) · [Portable Generator](https://polyhaven.com/a/portable_generator) · [Square Tiles](https://polyhaven.com/a/square_tiles)
- [ambientCG 许可](https://docs.ambientcg.com/license/) · [Tiles074](https://ambientcg.com/a/Tiles074) · [Concrete034](https://ambientcg.com/a/Concrete034) · [Asphalt017](https://ambientcg.com/a/Asphalt017)

奶娃的肤色图和口腔贴图来自本项目 Blender 原稿。Unity 版的口腔用静止和大笑两个姿态的 164 个唇缘顶点生成，并保留牙齿与舌头各自的变形。重建工具为 `Tools/export_hunter.py`，导出时验证口腔与身体共享坐标，且两种表情的唇缘贴合。

## 第一夜标题与场景补充

标题图参考[本项目奶娃的左前视图](../../ArtSource/HorrorMilkFrog/haunted-fullbody-skin-03/views/02-front-left.png)，生成后作为[全屏底图](../../Assets/Resources/Interface/AfterHoursTitle.png)导入 Unity。标题、按键和进度状态由 UI Toolkit 渲染。

| 使用位置 | CC0 模型来源 |
| --- | --- |
| 更衣室、洗衣房和浴池 | [旧寄存柜](https://tevy.dev/models/locker-room-tall-lockers)、[叠毛巾](https://tevy.dev/models/folded-towel-stack)、[洗衣筐](https://tevy.dev/models/laundry-basket)、[镀锌水桶](https://tevy.dev/models/metal-bucket) |
| 第一人称和拾取物 | [Small Plastic Torch](https://polyhaven.com/a/small_plastic_torch) |
| 更衣区、走廊、茶座和前台 | [工业壁灯](https://polyhaven.com/a/industrial_wall_sconce)、[掉漆木椅](https://polyhaven.com/a/painted_wooden_chair_02)、[老式短波机](https://polyhaven.com/a/vintage_radio_transceiver)、[旧皮箱](https://polyhaven.com/a/vintage_suitcase)、[吊扇](https://polyhaven.com/a/ceiling_fan)、[挂钟](https://polyhaven.com/a/wall_clock) |
| 可游玩街道的背景 | [老式住宅楼](https://tevy.dev/models/public-housing-slab)，v1 LOD1 |

沙发厅继续使用 Sofa 02。下方保留 0.60 米真实净空，并把腿部高度与模型、碰撞体一起调整。

标题图生成提示词

```text
Use case: stylized-concept. Asset type: a single photoreal cinematic environment still for the Windows title screen of the original horror game Milk Frog's Bathhouse: After Hours. Create one widescreen 16:9 image, edge to edge. The reference shows our exact original antagonist; use it for character anatomy only. Set the shot inside a weathered 1990s Chinese public bathhouse after closing, viewed from the dry side of an entry threshold. A warm, old, caged wall lamp glows down the far tiled aisle. Coarse green-gray ceramic wall tiles with mineral deposits, chipped old plaster, damp charcoal and green floor tiles, layered steam catching light gently, iron piping, tired wooden bathing benches, battered room doors. Right third: the original yellow frog creature from the reference half concealed behind a wet tiled pier, ochre skin, short arms, broad pale stomach, low set amber eyes and disquieting wide grin with small worn teeth. It is a believable weighty practical creature, seen only partly, with silhouette readable and face subtly edged by the caged light. Center-right corridor creates depth. Keep the left 38 percent in simple near-black shadow with textured plaster and minimal detail for a native title and menu overlay. Shoot at eye level, realistic 35mm cinematography, dark shadows with preserved detail, one warm tungsten practical against restrained cold green night tones, close material grain. Realistic rather than cartoon, melancholy dread, subtle mist, no hard beam rays, no Dutch angle. No gore, no people, no logos, no lettering or signage, no UI, no frames, no watermark.
```
