"""Re-render the east gate voice and contact Foley. Requires sherpa-onnx, NumPy, SciPy and ffmpeg.

Pass the extracted vits-melo-tts-zh_en directory. Only rendered audio ships with the game.
See ArtSource/EastGateAudio.md for the voice source.
"""
import argparse
from pathlib import Path
import subprocess

import numpy as np
from scipy.io.wavfile import write
from scipy.signal import butter, sosfilt
import sherpa_onnx
from tempfile import TemporaryDirectory

AUDIO = Path(__file__).resolve().parents[1] / "Assets/Resources/Audio"
LINES = {
    "intake": "东堤自动值守。核燃料已部署。东闸没有电。",
    "feedback": "东闸供电恢复。警告。旧浴场正在回送信号。",
    "evacuate": "检测到核事故。请尽快撤离浴场区域。",
    "radiation": "西侧探头，辐射指数九十七。四号楼，失去联系。",
    "gate": "东闸已开。跟着绿灯走。请勿回头。",
    "rollcall": "西区交班。出勤记录，零人。收到夜班回执，四十二份。日期，一九八七年七月十七日。",
}


def band(samples, low, high, rate):
    return sosfilt(butter(3, [low, high], fs=rate, btype="bandpass", output="sos"), samples)


def export(temp, name, samples, rate):
    wav = temp / (name + ".wav")
    peak = max(.001, np.max(np.abs(samples)))
    write(wav, rate, (samples * (.88 * 32767 / peak)).astype(np.int16))
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "libvorbis",
                    "-q:a", "6", str(AUDIO / (name + ".ogg"))], check=True)


def horn(samples, rate, index):
    voiced = np.flatnonzero(abs(samples) > .007)
    if len(voiced) < rate * .3:
        raise ValueError("No intelligible speech in take")
    dry = samples[max(0, voiced[0] - int(.055 * rate)):voiced[-1] + int(.16 * rate)]
    dry = np.pad(dry, (int(.36 * rate), int(.48 * rate)))
    t = np.arange(len(dry)) / rate
    damaged = index >= 2
    voice = band(dry, 345 if damaged else 210, 3750 if damaged else 4300, rate)
    voice = .62 * np.tanh(2.8 * voice) + .18 * dry
    noise = band(np.random.default_rng(348 + index).normal(size=len(t)), 800, 7800, rate)
    gaps = ((.30, .013), (.58, .029), (.86, .014)) if damaged else ((.76, .009),)
    for position, width in gaps:
        gap = np.exp(-((t - position * t[-1]) / width) ** 2)
        voice = voice * (1 - .76 * gap) + noise * .075 * gap
    mix = voice.copy()
    for delay, gain in ((.059, .16), (.127, .10), (.228, .042)):
        offset = int(rate * delay)
        mix[offset:] += voice[:-offset] * gain
    chime = np.where(t < .275, np.sin(np.pi * t / .275) ** 2, 0)
    mix += chime * .088 * (np.sin(t * 2 * np.pi * 765) + .42 * np.sin(t * 2 * np.pi * 1020))
    return mix + noise * (.0008 + .064 * np.exp(-((t - .055) / .009) ** 2)
                          + .049 * np.exp(-((t - t[-1] + .14) / .022) ** 2))


def guard_foley(temp):
    rate = 44100
    for name, length in (("guard-brace", .74), ("guard-strike", .79), ("guard-fall", 1.18)):
        t = np.arange(int(rate * length)) / rate
        noise = np.random.default_rng(552 + len(t)).normal(size=len(t))

        def hit(start, decay):
            return np.where(t >= start, np.exp(-np.maximum(0, t - start) * decay), 0)

        if name == "guard-brace":
            sound = (.42 * band(noise, 150, 1300, rate) + .15 * np.sin(2 * np.pi * (86*t - 22*t*t))) * np.sin(np.pi*t/length)**2
        elif name == "guard-strike":
            body = np.sin(2 * np.pi * (47*t + 2.6*(1 - np.exp(-t*28))))
            sound = (.54 * body * hit(.019, 9) + .22 * band(noise, 200, 4700, rate) * hit(.027, 27)
                     + .16 * band(noise, 350, 6000, rate) * hit(.091, 49) + .18 * band(noise, 35, 210, rate) * hit(.032, 6))
        else:
            sound = (.25 * band(noise, 65, 780, rate) * hit(.06, 11) + .16 * np.sin(2*np.pi*56*t) * hit(.08, 14)
                     + .19 * band(noise, 460, 4900, rate) * hit(.22, 9) + .12 * band(noise, 1200, 6000, rate) * hit(.51, 13))
        export(temp, name, sound, rate)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("model", type=Path)
    args = parser.parse_args()
    config = sherpa_onnx.OfflineTtsConfig()
    config.model.vits.model = str(args.model / "model.onnx")
    config.model.vits.tokens = str(args.model / "tokens.txt")
    config.model.vits.lexicon = str(args.model / "lexicon.txt")
    config.model.vits.dict_dir = str(args.model / "dict")
    config.model.num_threads = 4
    tts = sherpa_onnx.OfflineTts(config)
    with TemporaryDirectory() as session:
        work = Path(session)
        for index, (name, line) in enumerate(LINES.items()):
            result = tts.generate(line, sid=0, speed=.96 if index < 2 else 1.01)
            track = horn(np.array(result.samples), result.sample_rate, index)
            export(work, "east-pa-" + name, track, result.sample_rate)
        guard_foley(work)
