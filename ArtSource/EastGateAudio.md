# 东堤应急广播与现场音

六段东堤广播采用本作原创中文台词。录制时使用 MyShell 的 MeloTTS 中文模型和 sherpa-onnx 做本地语音合成。游戏包内仅包含已经剪辑、混音的 Ogg 音轨，离线可以播放。[MeloTTS](https://github.com/myshell-ai/MeloTTS) · [中文模型与 MIT 许可](https://huggingface.co/myshell-ai/MeloTTS-Chinese) · [sherpa-onnx 中文模型出处](https://k2-fsa.github.io/sherpa/onnx/tts/pretrained_models/vits.html)

| 音轨 | 场内触发 |
| --- | --- |
| east-pa-intake | 样芯接入值守台。播报东闸失电。 |
| east-pa-feedback | 切换应急供电。回送信号沿街传播。 |
| east-pa-evacuate | 冲击抵达值守台后，广播要求撤离。 |
| east-pa-radiation | 西侧监测探头失联。破音落在词间，保留关键数字。 |
| east-pa-rollcall | 第二波潮返者出现，广播收到来自一九八七年的夜班回执。 |
| east-pa-gate | 守闸人开启东廊。 |

人声经过窄带均衡、削波、短时断续和对岸反射处理。奶娃的吸气、撞击和摔落声由噪声滤波、低音与短反射原创合成。尘浪及楼板撞地分别使用独立的声源并按场内位置混音。
