# 第一夜美术资产

场景优先使用允许在公开仓库再分发的现成模型和材质。所有本批第三方资产均为 CC0。Unity 保留源贴图尺寸，并导入法线、粗糙度、金属度和遮蔽图中适用的部分。

| 位置 | 使用的资产 |
| --- | --- |
| 大浴池、淋浴间和洗衣房 | tevy 的浴缸、洗衣机和坐便器。盥洗室的回水任务使用 tevy 洗手盆。 |
| 茶座备餐区 | tevy 的水槽底柜、抽屉柜和壁柜，搭配 Poly Haven 的茶具、木桌和圆凳。 |
| 前厅、休息室和宿舍 | Poly Haven 的金属办公桌、电视、更衣长凳、Sofa 02、Vintage Day Bed 和金属货架。沙发的木脚按下方藏点的净空调整。 |
| 地下设备间 | Poly Haven 的 Portable Generator、Tool Cart 和 Worn Metal Rack。实机维修仍使用原来面向玩家的操作点。 |
| 室外 | tevy 的微型面包车，Poly Haven 的卷帘窗和金属废物桶。 |
| 商铺后门、街道路肩、东堤 | tevy 的[带轮垃圾斗](https://tevy.dev/models/dumpster)和[防汛沙袋](https://tevy.dev/models/sandbag-wall-straight)，均为 v1、CC0 1.0。 |
| 按摩室、静养室、副楼布草间 | tevy 的[旧布屏风](https://tevy.dev/models/torn-hospital-privacy-screen)，v1、CC0 1.0。 |
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

- [值班台底图](../../ArtSource/Interface/DutyDesk.png)和[故障单纸纹](../../ArtSource/Interface/DutyPaper.png)使用内置 ImageGen 生成。按钮、状态、地图门洞和可交互区由 Unity UI Toolkit 绘制。影院化界面改为模糊的实时画面做底后，两张底图移到 `ArtSource/Interface/` 存档，不再进入安装包。
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
- [地图纸纹](../../ArtSource/Interface/AtlasPaper.png)使用内置 ImageGen 生成，现已随值班台底图一起存档。楼层轮廓、开口、当前位置与比例尺仍从游戏状态实时画出。

地图纸纹提示词

```text
Create a single game-ready material plate as an original photograph/scan. 16:9 landscape from straight overhead, entire frame filled with a single very dark antique smoked gray-green archival drafting paper from a forgotten 1990s Chinese public bathhouse. This is for readable floor-plan geometry to be drawn over it in the game. Low contrast black olive fiber board (luminance roughly RGB 66 to 96, no white), graphite scuffing with nearly invisible pencil-eraser smudges in the center, tiny water tide marks at two outside corners, gently worn deckled fibers and one faint crease at the very edge. Central 90 percent absolutely quiet, evenly lit. Fine large realistic texture, authentic and old, premium tactile film production prop, nearly matte fibrous material with a faint natural horizontal grain. Full-bleed material only, entire image usable, no objects or background framing, no pre-drawn geometry or grid, no words/letters/numbers, no maps, no colored marks, no UI, no artwork, no borders or thick edges. Cold and muted, dynamic range reserved for use over black green UI.
```


## 影院化画面与界面

- 画面后期分两段完成。世界相机先叠加半分辨率的环境光遮蔽；手持手电画在同一张 HDR 画面上之后，再统一做泛光与镜头污渍、AgX 色调映射、偏冷暗部和钨丝灯高光的夜间调色、胶片颗粒、边缘色差和暗角。被追时画面更冷更紧，被抓瞬间泛红。暂停、背包、地图和结局画面把实时画面模糊压暗后垫在界面下面。「选项 → 画面」可以调亮度，并单独开关灯光柔晕、环境光遮蔽和胶片颗粒。
- 每个房间、每段走廊和楼梯各有一个盒投影反射探针。标题画面期间分批拍完，供电恢复后每帧补拍三个面，湿瓷砖和搪瓷浴缸会映出房间里真实的灯。拍摄时会临时隐去手电和奶娃。
- 灯具下方有随灯开关的淡光柱。大浴池和淋浴间持续冒出蒸汽，浮尘只在手电光里显形。各层两座楼梯口挂着电池供电的「安全出口」灯箱，停电时也亮着，把走廊尽头染成绿色。灯泡、灯管和路灯罩的自发光提高到能触发泛光的亮度。
- 墙面在踢脚高度留有拖把和鞋底蹭出的暗带，顶角发黑。奶娃皮肤叠加程序生成的平铺毛孔法线，褶皱处更湿亮。手电光斑带反光杯纹理和灯丝阴影。
- 美术贴图改为 BC7 压缩，法线贴图另行压缩，画面基本无损，显存约降到原来的四分之一。4 GB 显存的笔记本显卡因此能放下整套贴图。
- 中文字库由 `Tools/subset_typefaces.py` 随界面用字更新；现用字体与授权见下方。
- 截取预览图：`AfterHours.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -afterhours-capture <文件夹>` 会依次拍下标题、出生点、浴池、地下室、供电后的走廊、街道、暂停、背包、地图和结局，然后自动退出。截图模式不会写入存档。这次改动拍下的 25 张 1600×900 预览存放在 [`ArtSource/Previews/cinematic-interface-pass/`](../../ArtSource/Previews/cinematic-interface-pass/)。

## 标题页暗处演出与环境音

- [走廊底图](../../Assets/Resources/Interface/AfterHoursCorridor.png)用内置 ImageGen 把角色与原[标题图](../../Assets/Resources/Interface/AfterHoursTitle.png)的场景拆开。灯、长凳和右侧门框沿用原构图。
- [门后的彩色奶娃](../../Assets/Resources/Interface/AfterHoursHallFrog.png)复用本项目透明背景的[奶娃右前视图](../../ArtSource/HorrorMilkFrog/haunted-fullbody-skin-03/08-front-right-look.png)，灯亮时以原有色彩从门框后探出。
- [贴脸黑影](../../Assets/Resources/Interface/AfterHoursIntruderClose.png)用内置 ImageGen 从此前的[近景稿](../../Assets/Resources/Interface/AfterHoursIntruder.png)进一步推近。保留透明背景。标题断电后，整张脸旋转 180°，在画面中央原地闪现。灯泡火花由 UI Toolkit 绘制，电弧爆响在游戏内合成。
- 标题环境音使用 congusbongus 的《Lost in a bad place》，CC0，编辑了 120 毫秒循环接缝。 [原曲与许可](https://opengameart.org/content/lost-in-a-bad-place-horror-ambience-loop)
- 浴场环境音使用 TinyWorlds 的《Deep Humidity》，CC0，处理高低频和 2.1 秒循环接缝。故障灯的电流声和贴脸时的近处摩擦、短促呼吸及闷响在 Unity 中独立合成。 [原曲与许可](https://opengameart.org/content/deep-humidity)

走廊底图使用的提示词如下。

```text
Precise object edit for a shipped 16:9 horror game title. The supplied image is the edit target. Remove only the large yellow frog peeking from the far-right foreground doorway. Reconstruct the now empty narrow band of corridor behind it with the same dim deep-green wall, receding window and wet tiled floor, matching the original perspective and subtle distance haze. Keep the near right-hand glazed teal tiled door jamb and its vertical occluding edge, the exact view and position of the caged amber bulb near the upper middle, the copper plumbing, low bench, floor reflection, and the dark plain negative space across the left 40 percent. The lamp is on at exactly the original warm exposure. Lock the framing, image aspect, palette and existing geometry. Deliver one photorealistic empty-corridor plate with no character, no silhouette, no text, no UI, no added objects, no stylization.
```

贴脸近景使用的提示词如下。

```text
Edit the supplied transparent foreground asset of our own Milk Frog mascot into the fixed extreme close-up for its horror title scare. Preserve its rounded frog-like head, dark ochre skin, deep narrow eye sockets and distinctive large, uneven teeth. Put the face almost against the viewer's lens, pitched a little to the left, with ONE sharply readable small pale-amber eye looking straight into the lens. Give it an asymmetrically opening jaw and a threatening rictus rather than a friendly or posed smile. Use very tight perspective, as if the viewer can almost feel its moist breath. Show mostly the head and part of the nearest shoulder; a few dark fingers appear only as a partial edge shape on the far lower right. Narrow dirty grey-green sidelight catches the eye, wet edge of the lower lid and a few real uneven teeth; keep the rest deep charcoal, with textured pores and restrained natural specularity. The eye and immediate shape must register in one second on a nearly black corridor, so avoid making the whole face uniformly black. No blood, gore, comic expression, hard clean rim, glow effects, framing, text, blur blanket or UI. Strong large head with a distinct three-quarter contour anchored in the lower-right. Portrait composition and a genuinely transparent empty exterior, exactly one standalone character image.
```

贴脸近景第二次修整边缘时使用的提示词如下。

```text
Precise edge repair of this transparent creature sprite for a polished horror title. Keep the same alarming character identity, same close perspective, turned head, eye staring at camera, dirty olive-ochre moist skin, and same unnerving open rictus, teeth and lower-right fingers. Reframe very slightly wider, reconstructing the missing rounded crown and the complete screen-left cheek and neck edge. The silhouette must be organically curved and fully contained along the top and left of the output. Leave an unmistakable band of clean transparent pixels at least 8 percent of the canvas along the ENTIRE top edge and ENTIRE left edge. Do not crop, truncate, flatten or straight-cut any part of the top or left contour. Only shoulders, hand or chest can exit the bottom and right sides. Keep the strong facial close-up, eyes and teeth briefly readable in darkness. Retain real alpha transparency outside the shape and the original photographic skin, desaturated palette, and shadow detail. One portrait standalone sprite, no scene, no rectangular dark background, no ground, no drop shadow, no text, no UI.
```

此前近景稿使用的提示词如下。

```text
Transparent-background foreground character cutout for the Unity horror game Milk Frog's Bathhouse: After Hours. The supplied image is an identity reference for our own Milk Frog, not the pose or backdrop. Create ONE new separate rendition of this same unsettling pear-shaped frog mascot, in a NEW close-to-camera three-quarter lean. It intrudes from below and to the viewer's right, tilting its head toward screen left, with one dark broad hand reaching onto the invisible lower edge of the viewer's screen, like it is peeking into the interface from outside its frame. Large rounded head, very hunched thick shoulder and belly, slightly visible hooked fingers, and an unmistakably sinister toothy grin as in the reference. As a shadow in front of the lens, the body is deep near-black umber/charcoal with just a cold slate rim outlining the head and hand, a faint muted amber glint in one eye and a few desaturated teeth catching the flicker. Keep it legible against a dark scene without turning it into a flat cutout or brighter yellow portrait. Realistic worn skin material with hard-edged silvery rim only, strong threatening silhouette. Crop legs out naturally as a waist-up close foreground figure, but leave some transparent breathing room around the head, forearm and hand for UI placement. Image composition is portrait-oriented. Actual clean transparency all around the character, no black background rectangle, no walls, no light flare outside the subject, no text or UI.
```
## 维修副楼与街区配件

- 二、三楼连廊和副楼继续使用本作已有的水磨石、抹灰、旧砖和钢板材质。两个楼层各有四个房间，配有旧工作台、铁架、无线电、布草车和沙发。房间门洞、东端折坡及楼下装卸区有实体碰撞。
- 机修间隔离箱和泵房墙上的接线箱使用 James Ray Cock 的 Utility Box 02。机修间和街尾维修站复用 Mateusz Sadek 的 Metal Toolbox。两件均使用 2K 色彩及法线，金属和粗糙度地图打包进 Unity 材质通道。[Utility Box 02](https://polyhaven.com/a/utility_box_02) · [Metal Toolbox](https://polyhaven.com/a/metal_toolbox) · [Poly Haven CC0 许可](https://polyhaven.com/license)

## 打烊后的街灯与鹭塘瓷板

街区云层使用 Greg Zaal 与 Jarod Guest 的 2K 天空全景，游戏材质将其调为低照度夜色。[Kloppenheim 06 Pure Sky](https://polyhaven.com/a/kloppenheim_06_puresky) · [CC0](https://polyhaven.com/license)

一楼南翼使用内置图像生成工具制作的原创鹭塘瓷板。生成提示词如下。

Create an original environment game texture, a straight-on orthographic conservation photograph of an entire wide original mid-century Chinese municipal bathhouse ceramic tile mural. Two long-necked white egrets in reeds and lotus leaves above dark turquoise water, pale jade mist and layered hill silhouettes. Restrained mineral pigments, ochre, celadon, muted petrol glaze, charcoal outlines and old ivory. Hand-painted kiln-fired ceramic panels with a regular grid of fine slightly aged grout, scattered glaze crazing, a few chips especially lower edge, believable original craftsmanship. Wide landscape 3:2, artwork fills all edges. Flat neutral diffuse lighting for albedo, even exposure. No framing wall, lettering, watermarks, cast shadows, perspective or spotlight.

沿街四幅搪瓷牌为原创绘制，依次安装在浴场、面馆、值守棚与街尾商廊，实机按牌面分别取样。原始提示词如下。

An ORIGINAL physically based game prop ALBEDO texture atlas, landscape 3:2 aspect. Four different real Chinese vintage porcelain enamel signs stacked vertically, exactly FOUR equal-height horizontal bands, each occupying the FULL WIDTH edge-to-edge, no margins or padding anywhere. Orthographic straight-on flat scan, neutral flat diffuse light, no perspective, no surroundings, no manufactured shadows. From top down: TOP QUARTER a municipal bathhouse fascia, faded celadon-green enamel, an engraved egret-and-ripples period seal at left, very legible large ivory hand-lettered Chinese '奶娃浴场' in center, double pinstripe border; SECOND QUARTER vermilion/oxblood enamel noodle shop, bowl and steam illustration at left, large hand-painted warm bone-white Chinese '石桥汤面', gold ornamental rules and faded lacquer; THIRD QUARTER soot-dark navy blue enamel with vintage off-white stenciled Chinese '夜班值守' and small old lantern emblem; BOTTOM QUARTER desaturated warm ivory enamel with historical dark green brush lettering '石桥商廊', faded rose commercial seal, green decorative pinstripe. Real age, oxidized bolt holes, small localized rust blooms and realistic localized flaking layers, brush edges, generations of cleaning. Printed graphics physically follow chipped surfaces. The letters MUST be exactly as specified, individually calligraphed with natural negative space; no other text or English. High-end first-person survival-horror environment asset, restrained realistic municipal Chinese 1980s art direction, NOT fantasy, high legibility. Fill all of each rectangular strip with the sign surface, ALL FOUR equal-size strips directly adjacent, no background. Treat the image as a digital scan, no flash glare, depth of field or tilt.


### 夜间墙面与中文字形

砖、漆面、湿区釉砖和水泥墙沿用各自扫描材质的 2K 原始高度图；色图、法线和高度使用相同 UV。墙面近处使用视差，真实墙裙和压条参与手电投影。[Brick Moss 001](https://polyhaven.com/a/brick_moss_001) · [Painted Concrete 02](https://polyhaven.com/a/painted_concrete_02) · [Worn Tile Floor](https://polyhaven.com/a/worn_tile_floor) · [Scuffed Cement](https://polyhaven.com/a/scuffed_cement)

浴场题字采用马善政毛笔楷书，道具、交班簿和章节采用霞鹜文楷。操作键与数字沿用思源黑体。随游戏分发静态字形子集，完整许可见 [马善政 OFL](../../Assets/Resources/Interface/TitleTypeface-OFL.txt) 与 [霞鹜文楷 OFL](../../Assets/Resources/Typeface/Ledger-OFL.txt)。[马善政源字体](https://github.com/google/fonts/tree/main/ofl/mashanzheng) · [霞鹜文楷源字体](https://github.com/lxgw/LxgwWenKai)


### 房间内的布料与收口

洗衣房的换洗帘和副楼折叠台的布挡采用 Poly Haven 的 Rough Linen（CC0），在双面褶皱网格上按米铺设颜色和切线法线。墙裙、管夹、门套、厨房与检修用铁件沿用现有浴场材质。斜光检查使用独立 QA 玩家中的 `Tools/QA/AfterHoursRoomSurfaceAudit.cs`；先用 `-afterhours-room-surfaces <输出目录> baseline` 记录机位，再以 `adjusted` 重拍同一组位置。[Rough Linen](https://polyhaven.com/a/rough_linen)

## 夜班工单

`Assets/Resources/Interface/DispatchBoard.jpg` 使用内置图像生成工具绘制无字夹板。标题、任务、状态印章与按键全部在运行时排印。底图保留原始 1536 × 1024 像素。

Top-down prop photograph for a Chinese municipal bathhouse night-shift game. One horizontal landscape maintenance clipboard, aspect ratio 3:2. Orthographic flat lay, paper edges parallel to frame. A stack of warm grey-beige unprinted forms covers the entire frame, with a narrow oxidized enamel backing and a small dull metal clip at the top center. Slight pressure dents and a folded corner, fine fibrous paper, tiny water damage on the extreme edges, uneven cut leaves at bottom. The central 85% is evenly lit, blank, matte and plain so actual typeset interactive text can be added in the game. Restrained aged olive brass and neutral buff paper under a fluorescent desk lamp. Extremely realistic close photographic detail. No writing, no symbols, no ink lines, no tools, no hands, no desk clutter. Crop close to the outer edges of the clipboard.

## 石桥东堤

水文站的搪瓷匾、铸铁水尺和暗房里的旧照片专为本作生成。归档后的旧照与宿舍看片灯台共用同一张底片。三张洪痕记录由玩家在游戏内实拍，连同显影进度保存在当局存档。建筑内的水槽、家具、工作台和车辆沿用本页已登记的模型。

| 美术 | 用途 |
| --- | --- |
| `Surfaces/RiverStationEnamel` | 街上可辨认的水文站门匾 |
| `Surfaces/FloodGauge` | 三处测量点的搪瓷水尺 |
| `Interface/SoundingDesk` | 测深仪的机身，河道、浮标和船位在运行时绘入空白荧光屏 |
| `Interface/FloodWatch` | 夜班合影、拼接用的三块玻璃底片 |

<details><summary>生成提示词</summary>

Straight-on orthographic material scan of one single long horizontal weathered enamel municipal hydrology station fascia, 3:1 landscape composition. The centered, very legible, large Chinese painted lettering reads exactly “石桥水文观测站”, six characters on one line, a 1970s Chinese river observatory. To the left a faded round emblem with three simple water lines and a gauging staff. Original muted petrol-blue enamel with dirty cream strokes, narrow ivory frame, small rust pits at bolts, faint rain streaks, authentic hand signwriting. Flat diffuse lighting, sign fills the entire image edge to edge, NO wall around it, NO perspective, no other words, no UI, original design for a Chinese survival horror game material.

A single wide industrial gauge backing plate from a 1980s Chinese municipal river observatory, shot flat-on for a realistic game texture. Landscape aspect 2.3:1. Entire image is a single aged ivory vitreous enamel instrument plate with a thin dark petrol blue metal edge, four battered rivet holes at corners, water scale deposits and a real old horizontal high-water stain. Left and right margins have boldly painted navy survey staff graduations like large alternating E-shaped blocks in a vertical column, evenly stepped, a diagonal thin wine-red flood datum across the lower-middle. In the middle an unlettered aged blank enamel space about one third width to apply a station-specific serial number in game. Readable in a flashlight beam. Honest mechanical detail, period industrial design, rust chips around the mounting holes, natural relief. No scenery, no wall, no modern digital elements, no extra lettering, no watermark.

An authentic 1980s small-town Chinese hydrology station group photograph during a flood watch, in-game archival evidence prop for a realistic first-person horror game. A single 3:2 horizontal silver-gelatin photographic print scanned edge to edge. A pair of rain-soaked workers in old rubber boots stand at the left and right of a tall flood gauge against a tiled brick sluice wall. Their faces are natural, serious and not posed for drama. Exactly between them a much heavier round-bodied humanlike shape has been deliberately rubbed out of the silver emulsion by hand; a few curved scratches and a pale stout silhouette remain at the waterline. Steps and an empty chair make the height legible. Bleak overcast night flash, wet concrete, readable midtones, charcoal blacks and warm grey highlights, subtle fibre, slight chemical staining at the print edge. Simple compelling photographic composition, believable optical grain. No titles, no modern equipment, no UI, no added typography or watermark, no collage.

Game prop texture photographed straight-on: the entire faceplate of a battered late-1980s Chinese municipal hydrographic remote sounding launch controller. Wide landscape 3:2. A large blank unlit rectangular green-black CRT window with slightly rounded corners sits exactly centrally, spanning 80% of the image width and 72% of its height. The playable radar raster will be drawn in software over that area, leave it completely empty, no reflections, text, waves, figures or maps. Thin rubbed black rubber CRT bezel, military green powder-coated cast metal body with recessed rivets, two small amber and green jewels and a knurled thumb knob in the lower metal margin only. Macro detail of fine patina, salt deposits, scars and stamped calibration markings. Flat front elevation no perspective, no hands or surrounding tabletop. The entire image is filled edge-to-edge by the instrument, corners only very slightly rounded. Neutral side lighting, realistic found industrial electrical equipment, legible midtones.

</details>

## 末班车

车厢结局底图为本项目生成的原创场景图。运行时从 Interface/LastBus 读取；结局文字与声音根据本局调查和证物处置分别呈现。

票夹使用本项目生成的无字油布装订纸叶。全部章印、正文和签收信息由游戏排印。旧票务所的桌、架、座椅、无线电与壁灯沿用清单中的已有模型。

## 夜班工作台与招领照

工作台油毡和前台的招领照均以内置 ImageGen 为本作生成。油毡用于前台桌面、背包、地图和设置。照片以原始宽高比保存在 `Assets/Resources/Interface/Items/LostPair.jpg`，场内相纸、拾取提示和背包正面共用同一份原画。背面文字由 Unity 排印。石桥旧站合影沿用暗房已有的 `FloodWatch.jpg`。

<details><summary>两张原画的生成提示词</summary>

Use case: photorealistic-natural. Create a landscape 16:9 full-bleed material photograph of the TOP surface of a real 1980s Chinese municipal night duty desk in an abandoned public bathhouse, shot straight down in close, with no perspective vanishing point. Old dark petroleum green linoleum bonded to wood, subtle fine pebbled grain, shallow scalpel scores, a ghostly ring from an enamel tea mug near the far upper right, edge-wear related to elbows near the lower edge. A narrow worn walnut lip and one small tarnished brass corner fastener right at the extreme lower border, not a frame around the image. Shallow rake of warm tungsten light from the right gives worn streaks a soft sheen, middle and left are broad unoccupied low-contrast, deep muted petrol green, visible midtones, hand polished patina. Feels like a photograph of one humble well-used object with decades of night shifts in it. Clean legibility for later live UI. No objects on top, no letters, no text, no papers, no line grids, no printed graphics, no HUD, no stylization, no vignette or hard spotlight. Highest fine material fidelity.

Use case: historical-scene. Landscape 3:2 single analog vernacular photograph, photographic image only, no margins or lettering. Taken by an ordinary night-shift attendant in a small Chinese municipal public bathhouse at dawn in the early 1990s. In the near foreground on a low old glazed tile bathhouse counter sits a single pair of very small worn canvas shoes, wet laces loosely tucked in: one shoe right-side up, the other upside down over an inverted plain white enamel teacup with a chipped blue rim to dry. Clearly recognizable shapes, honest prosaic objects and a faint damp footprint on the tile. A row of empty wooden hooks and a barred frosted window out of focus behind, glancing morning light draws a bright line across one toe. Natural midtones and visibly aged muted hand-printed color film, petrol green and warm yellow, faded blacks, gentle grain and emulsion wear. Tight, quiet but slightly unsettling composition of a real lost-property object with a story, not a horror poster. No people, no creatures, no gore, no visible lettering or dates, no illustration, no overlay, no photo border or table surrounding the photograph.

</details>

## 东堤冷样保管间

潮返者剪影和封存匣背包图为本项目生成的原创图像。部署后的尘浪、核爆云、建筑断裂和两个事故结局的镜头均在 Unity 中实时渲染。战场预览截自 Windows 试玩版，堤岸设施沿用本项目已有的授权模型与路面材质。
