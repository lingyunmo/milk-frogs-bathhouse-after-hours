# 第一夜试玩版

Windows 本地输出在 `Builds/ArtPass/AfterHours.exe`。也可以用 Unity 6000.3.29f1 打开 `Assets/Scenes/AfterHours_Blockout.unity`，按 Play 进入同一个标题菜单。游戏会在用户数据目录建立单人存档，维修和拾取会记录进度。F5 记录当前位置和朝向。被抓后从存档恢复。

- WASD 移动，鼠标观察，按住 Shift 奔跑。C 切换蹲行，可钻到沙发、检修台和床架下面。站立需要上方净空。
- E 推门、拾取、翻窗和操作设备。维修条走满后完成当前步骤。飞轮和上行线路有独立面板，A / D 选择，空格确认，Esc 松手。
- F 开关手电，R 换电，G 抛石子。可把石子扔过门口，也可用维修声把奶娃引到另一层。
- B1 修发电机并配电，3F 接线路并打开路由器，1F 发出求援。随后在东端站牌翻开反光板，等车到站后从路肩登车。电线可以先接。修水槽和申请空投各自可选。

## 奶娃

训练脚本使用 PyTorch，在现场可见、可听和已记住的线索上生成有标签的示范样本。模型学的是给巡游点、声源、最后目击点及正在目击的目标打分。运行时只加载一个小型神经网络。在 8,000 个独立合成样本上的分数误差为 0.0327 RMSE，在 1,000 组候选中的首选一致率为 90.9%。这些数值衡量复现示范的程度，真实玩家追逃难度需要试玩。模型可在 `Training/train_policy.py` 重新训练。

## 当前交付范围

当前可玩版本为单人。联机阶段设计为每人拥有独立背包，物品拾取和丢弃由房间权威端裁决，维修与撤离使用共同的场景进度。前台、备餐区、浴室、洗衣房与设备层使用带贴图的家具和设施。角色表演使用腿部和表情变形。首次通关时长尚待人测。

## 素材来源

[场景资产与授权清单](art-sources.md)。

- 瓷砖、墙面与柏油使用 ambientCG 的原始 2K 色彩图，CC0。[Tiles074](https://ambientcg.com/a/Tiles074) · [Concrete034](https://ambientcg.com/a/Concrete034) · [Asphalt017](https://ambientcg.com/a/Asphalt017) · [ambientCG 许可](https://docs.ambientcg.com/license/)
- 字体使用 Noto Sans SC 的静态子集，SIL OFL 1.1。[字体及上游许可证](https://github.com/google/fonts/tree/main/ofl/notosanssc)
- 奶娃来自本项目的 Blender 源工程。4096 像素肤色图和源文件的 SHA‑256 一致。脚步、设备声与笑声由项目代码合成。
