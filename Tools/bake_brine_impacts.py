"""Render the district pressure clap and falling masonry as small mono Vorbis cues."""
from pathlib import Path
import subprocess

import numpy as np
from scipy.signal import lfilter

audio = Path(__file__).resolve().parents[1] / "Assets/Resources/Audio"
rate = 32000
for slab, label in [(False, "brine-pressure"), (True, "brine-masonry")]:
    t = np.arange(round(rate * (1.14 if slab else 2.65))) / rate
    rng = np.random.default_rng(8723 if slab else 8761)
    white = rng.uniform(-1, 1, t.shape)

    def filtered(pole):
        return lfilter([pole], [1, pole - 1], white)

    low, mid, high = filtered(.013), filtered(.115), filtered(.47)
    hz = 64 + 35*np.exp(-19*t) if slab else 38 + 50*np.exp(-9*t)
    body = np.sin(2*np.pi*np.cumsum(hz)/rate)*np.exp(-(11 if slab else 4.8)*t)*.51
    gravel = (.5+.5*np.sin(2*np.pi*(20*t+1.55*np.sin(14*t))))**5
    grit = (high-mid)*gravel*np.exp(-(4.1 if slab else 2.25)*t)
    air = (mid-low)*np.exp(-(7.1 if slab else 1.87)*t)
    tail = low*np.exp(-(5.4 if slab else 1.64)*t)
    mix = np.tanh((body+grit*(1.9 if slab else .92)+1.65*air+2.5*tail)*1.2)
    mix *= np.minimum(1, t*(290 if slab else 128))*np.minimum(1, (len(t)/rate-t)*100)
    # Reflection off the street's far side, followed by loose masonry falling against metal.
    for offset, amount in ([(.072, .17), (.149, .09)] if slab else [(.12, .19), (.254, .145), (.423, .085)]):
        delay = round(offset*rate)
        mix[delay:] += mix[:-delay].copy()*amount
    mix *= .93/max(.93, np.max(np.abs(mix)))
    subprocess.run(["ffmpeg","-v","error","-y","-f","f32le","-ar",str(rate),"-ac","1",
                    "-i","pipe:0","-c:a","libvorbis","-q:a","6",str(audio/(label+".ogg"))],
                   input=mix.astype("<f4").tobytes(),check=True)
