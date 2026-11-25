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


## 深夜值班台

背包采用左侧分类和物件、右侧近看与使用的结构。布局沿用网页首作可操作的背包，台面、纸张、物件图和版式按本作浴场重新制作。

- [值班台底图](../../Assets/Resources/Interface/DutyDesk.png)和[故障单纸纹](../../Assets/Resources/Interface/DutyPaper.png)使用内置 ImageGen 生成。按钮、状态、地图门洞和可交互区由 Unity UI Toolkit 绘制。
- 六件道具的 768 像素透明图使用 Blender 5.1 Cycles 渲染。手电使用场景内原模型，另外五件使用项目制作的网格。手电来自 [Poly Haven Small Plastic Torch](https://polyhaven.com/a/small_plastic_torch)。
- 湿区、门厅和设备层使用三组 2048² 实拍 PBR 材质。色彩、法线以及打包后的金属度、粗糙度与 AO 在游戏中共同参与照明。[Worn Tile Floor](https://polyhaven.com/a/worn_tile_floor) · [Old Mosaic Floor](https://polyhaven.com/a/old_mosaic_floor) · [Scuffed Cement](https://polyhaven.com/a/scuffed_cement)

底图提示词

```text
Create a single premium photorealistic overhead background plate for the character inventory and pause screen of an original first-person Chinese bathhouse horror game. One 16:9 widescreen 1536x1024 or wider crop-safe raster image, luxurious tactile PBR material photography. Picture an abandoned 1990s municipal bathhouse attendant's workspace at 2am. The entire inner 75% is a very dark, bottle-green/charcoal worn leather and fine charcoal canvas work mat, quiet low-contrast grain, fingerprints and realistic scratches barely visible. Keep it usable for light UI typography all across the central and left areas. Objects are strictly confined to the extreme outer crop edges and should be small: a little of distressed brown wood with one brass nail at the left, a sliver of an old canvas shoulder bag and leather strap entering from upper right beyond x=88%, an out-of-focus edge of faded paper at the very bottom left, a droplet stain in the lower right. Soft practical tungsten grazing from offscreen top left with very restrained cool green bounce, preserve black tonal detail. This is a cohesive material field, NOT a user interface and not a display of objects, no inventory slots or rectangles or borders, no titles/signage/text/letters, no markings that look like numbers, no readable text, no logos, no hands, no characters, no center object, no skulls/symbols, no blood. Blacks need details, calm and almost black center, a hint of antique warm brass on corners only. High fidelity documentary film still, real woven grain and distressed leather, designed to sit behind native overlays in a horror game.
```

纸纹提示词

```text
One horizontal game material plate, macro tabletop scan of a densely fibrous, worn Chinese municipal maintenance logbook's blank cardstock liner from the late 1990s. Uniform warm gray-olive and smoked tobacco, medium-dark at midtones (about RGB 107 100 80, not white), with finely visible pressed paper fibers, tiny creases, restrained greasy stains and a subtle folded shadow down one horizontal side. Full-bleed rectangular material region without any other props or background. Fine believable high-resolution paper texture usable as an asset under typography and architectural floor-plan linework. No writing, no lines, no grid, no letters, no symbols, no borders, no book frame, no large color blocks, no vignette stronger than 10%. Looks like a direct photo of a real worn stock sheet under diffuse warm studio lighting.
```


## 第一夜视觉细化

- 走廊、设备层和更衣室的建筑表面保留实拍底图。附加的微细法线、跨墙段的水渍和沿地漏分布的湿区随场景光源变化。门框、门锁、蒸汽管、法兰、电箱和地漏是实体渲染网格。
- 管路使用 Amal Kumar 的 2K 色彩和法线，粗糙度、金属度及遮蔽图按 Unity Standard 通道打包。[Poly Haven Rusty Metal Sheet](https://polyhaven.com/a/rusty_metal_sheet)
- 背包的六件实物使用离屏相机。手电沿用场景中的 Poly Haven 模型，其他五件沿用背包 2D 图标的 Blender 网格。材质在独立灯光中呈现，可拖动检查细节。
- [地图纸纹](../../Assets/Resources/Interface/AtlasPaper.png)使用内置 ImageGen 生成。楼层轮廓、开口、当前位置与比例尺仍从游戏状态实时画出。

地图纸纹提示词

```text
Create a single game-ready material plate as an original photograph/scan. 16:9 landscape from straight overhead, entire frame filled with a single very dark antique smoked gray-green archival drafting paper from a forgotten 1990s Chinese public bathhouse. This is for readable floor-plan geometry to be drawn over it in the game. Low contrast black olive fiber board (luminance roughly RGB 66 to 96, no white), graphite scuffing with nearly invisible pencil-eraser smudges in the center, tiny water tide marks at two outside corners, gently worn deckled fibers and one faint crease at the very edge. Central 90 percent absolutely quiet, evenly lit. Fine large realistic texture, authentic and old, premium tactile film production prop, nearly matte fibrous material with a faint natural horizontal grain. Full-bleed material only, entire image usable, no objects or background framing, no pre-drawn geometry or grid, no words/letters/numbers, no maps, no colored marks, no UI, no artwork, no borders or thick edges. Cold and muted, dynamic range reserved for use over black green UI.
```
