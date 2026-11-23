"""게임 소리 파일 합성기. 순수 파이썬(wave, math, random)으로 여러 겹을 쌓아 WAV를 만든다.
실행: python devscripts/make_audio.py  ->  Assets/Resources/Audio/*.wav (16비트 모노 44.1kHz)
"""
import math
import os
import random
import struct
import wave

SR = 44100
OUT = os.path.join("Assets", "Resources", "Audio")
random.seed(20261123)
TAU = 2.0 * math.pi


# ---------- 기본 도구 ----------

def n_samples(sec):
    return int(sec * SR)


def noise(n):
    return [random.uniform(-1.0, 1.0) for _ in range(n)]


def lowpass(x, fc):
    a = math.exp(-TAU * fc / SR)
    y = []
    s = 0.0
    for v in x:
        s = (1.0 - a) * v + a * s
        y.append(s)
    return y


def highpass(x, fc):
    lp = lowpass(x, fc)
    return [v - l for v, l in zip(x, lp)]


def bandpass(x, f, q=2.0):
    """RBJ 바이쿼드 밴드패스."""
    w0 = TAU * f / SR
    alpha = math.sin(w0) / (2.0 * q)
    b0, b1, b2 = alpha, 0.0, -alpha
    a0, a1, a2 = 1.0 + alpha, -2.0 * math.cos(w0), 1.0 - alpha
    b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0
    y = []
    x1 = x2 = y1 = y2 = 0.0
    for v in x:
        o = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, v
        y2, y1 = y1, o
        y.append(o)
    return y


def env_exp(n, decay_sec, attack_sec=0.002):
    a = max(1, int(attack_sec * SR))
    out = []
    for i in range(n):
        t = i / SR
        e = math.exp(-t / decay_sec)
        if i < a:
            e *= i / a
        out.append(e)
    return out


def mul(x, e):
    return [a * b for a, b in zip(x, e)]


def scale(x, g):
    return [v * g for v in x]


def mix(*tracks):
    n = max(len(t) for t in tracks)
    out = [0.0] * n
    for t in tracks:
        for i, v in enumerate(t):
            out[i] += v
    return out


def pad(x, n):
    return x + [0.0] * max(0, n - len(x))


def sine(n, f, phase=0.0):
    return [math.sin(TAU * f * i / SR + phase) for i in range(n)]


def delayed(x, sec, total=None):
    d = [0.0] * n_samples(sec) + x
    return d if total is None else pad(d, total)[:total]


def reverb(x, wet=0.3, size=1.0, tail=1.2):
    """슈뢰더 잔향: 콤 필터 4개 + 올패스 2개. 소리가 방 안에서 울리는 느낌."""
    n = len(x) + n_samples(tail)
    src = pad(x, n)
    combs = []
    for d_ms, g in ((29.7, 0.77), (37.1, 0.75), (41.1, 0.73), (43.7, 0.71)):
        d = int(d_ms * size * SR / 1000)
        buf = [0.0] * d
        out = []
        idx = 0
        for v in src:
            o = buf[idx]
            buf[idx] = v + o * g
            idx += 1
            if idx >= d:
                idx = 0
            out.append(o)
        combs.append(out)
    y = [sum(c[i] for c in combs) * 0.25 for i in range(n)]
    for d_ms, g in ((5.0, 0.7), (1.7, 0.7)):
        d = int(d_ms * SR / 1000)
        buf = [0.0] * d
        out = []
        idx = 0
        for v in y:
            b = buf[idx]
            o = -g * v + b
            buf[idx] = v + g * o
            idx += 1
            if idx >= d:
                idx = 0
            out.append(o)
        y = out
    return [src[i] * (1.0 - wet) + y[i] * wet * 1.6 for i in range(n)]


def distort(x, drive):
    return [math.tanh(v * drive) / math.tanh(drive) for v in x]


def fade_edges(x, ms=8):
    n = int(ms * SR / 1000)
    x = list(x)
    for i in range(min(n, len(x) // 2)):
        g = i / n
        x[i] *= g
        x[-1 - i] *= g
    return x


def loopify(x, xfade_sec):
    """끝과 처음을 겹쳐서 이음매가 안 들리는 반복 소리로 만든다."""
    n = n_samples(xfade_sec)
    body = x[:len(x) - n]
    tail = x[len(x) - n:]
    for i in range(n):
        g = i / n
        body[i] = body[i] * g + tail[i] * (1.0 - g)
    return body


def normalize(x, peak=0.9):
    m = max(abs(v) for v in x) or 1.0
    return [v * peak / m for v in x]


def save(name, x, peak=0.9):
    os.makedirs(OUT, exist_ok=True)
    x = normalize(x, peak)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, v)) * 32767)) for v in x))
    print(f"{name}.wav  {len(x) / SR:.2f}s")


def partials(freqs, decays, gains, n):
    out = [0.0] * n
    for f, d, g in zip(freqs, decays, gains):
        for i in range(n):
            t = i / SR
            out[i] += g * math.sin(TAU * f * t) * math.exp(-t / d)
    return out


# ---------- 소리들 ----------

def amb_drone():
    sec = 26.0
    n = n_samples(sec)
    t = [i / SR for i in range(n)]
    # 깊은 저음 (느리게 숨 쉬듯 변한다)
    low = [0.0] * n
    for f, ph, rate in ((41.0, 0.0, 0.07), (55.0, 1.3, 0.05), (82.4, 2.1, 0.09), (110.0, 0.6, 0.04)):
        for i in range(n):
            lfo = 0.6 + 0.4 * math.sin(TAU * rate * t[i] + ph)
            low[i] += math.sin(TAU * f * t[i] + 2.0 * math.sin(TAU * 0.03 * t[i])) * lfo * 0.25
    # 먼 바람: 대역 잡음에 느린 변조
    wind = bandpass(noise(n), 320, 0.8)
    wind = [w * (0.5 + 0.5 * math.sin(TAU * 0.08 * t[i] + 1.0)) * 1.4 for i, w in enumerate(wind)]
    rumble = lowpass(noise(n), 90)
    rumble = scale(rumble, 3.5)
    layers = [low, scale(wind, 0.22), scale(rumble, 0.35)]
    # 멀리서 들리는 삐걱임과 쿵 소리
    ev = [0.0] * n
    for _ in range(9):
        start = random.uniform(1.0, sec - 4.0)
        kind = random.random()
        if kind < 0.5:   # 삐걱임: 천천히 내려가는 톱니파
            dur = random.uniform(0.9, 1.8)
            f0 = random.uniform(180, 420)
            m = n_samples(dur)
            ph = 0.0
            for i in range(m):
                f = f0 * (1.0 - 0.35 * i / m)
                ph += TAU * f / SR
                s = (ph / TAU) % 1.0 * 2.0 - 1.0
                e = math.sin(math.pi * i / m) ** 2
                ev[int(start * SR) + i] += s * e * 0.05
        else:            # 멀리서 쿵
            m = n_samples(1.2)
            th = mul(sine(m, random.uniform(48, 70)), env_exp(m, 0.22))
            for i in range(m):
                ev[int(start * SR) + i] += th[i] * 0.30
    ev = lowpass(ev, 1400)
    layers.append(ev)
    y = mix(*layers)
    y = reverb(y, wet=0.25, size=2.0, tail=0.1)[:n]
    y = loopify(y, 2.0)
    save("Amb_Drone", y, 0.8)


def heartbeat():
    n = n_samples(0.95)

    def thump(strength, f0):
        m = n_samples(0.22)
        pitch = [f0 * (1.0 - 0.45 * min(1.0, i / (0.12 * SR))) for i in range(m)]
        ph = 0.0
        s = []
        for p in pitch:
            ph += TAU * p / SR
            s.append(math.sin(ph))
        s = mul(s, env_exp(m, 0.06, 0.004))
        click = mul(lowpass(noise(m), 220), env_exp(m, 0.02, 0.001))
        return scale(mix(s, scale(click, 1.5)), strength)

    y = mix(pad(thump(1.0, 62), n), delayed(thump(0.7, 54), 0.27, n))
    y = reverb(y, wet=0.15, size=0.6, tail=0.05)[:n]
    save("Heartbeat", fade_edges(y), 0.9)


def footsteps():
    for k in range(1, 5):
        dur = 0.32
        n = n_samples(dur)
        brightness = random.uniform(1100, 2600)
        heel = mul(bandpass(noise(n), brightness, 1.2), env_exp(n, random.uniform(0.018, 0.03), 0.001))
        scuff = mul(highpass(noise(n), 3500), env_exp(n, 0.05, 0.015))
        thud = mul(sine(n, random.uniform(70, 110)), env_exp(n, 0.07, 0.002))
        body = mix(scale(heel, 2.4), scale(scuff, 0.35), scale(thud, 1.1))
        body = reverb(body, wet=0.28, size=1.1, tail=0.25)
        save(f"Footstep{k}", fade_edges(body), 0.85)


def door_slide():
    n = n_samples(1.1)
    t = [i / SR for i in range(n)]
    sweep = []
    ph = 0.0
    for i in range(n):
        f = 260 + 520 * math.sin(math.pi * i / n)
        ph += TAU * f / SR
        sweep.append(math.sin(ph) * 0.18)
    grit = bandpass(noise(n), 900, 0.9)
    env = [math.sin(math.pi * i / n) ** 0.7 for i in range(n)]
    roll = mul(mix(grit, sweep), env)
    rumble = mul(lowpass(noise(n), 140), env)
    end = delayed(mul(sine(n_samples(0.2), 82), env_exp(n_samples(0.2), 0.05)), 0.92, n)
    y = mix(scale(roll, 0.9), scale(rumble, 2.5), scale(end, 1.4))
    y = reverb(y, wet=0.2, size=1.2, tail=0.3)
    save("DoorSlide", fade_edges(y), 0.8)


def door_creak():
    n = n_samples(1.6)
    ph = 0.0
    saw = []
    for i in range(n):
        f = 140 + 90 * math.sin(TAU * 0.9 * i / SR) + 160 * (i / n)
        ph += TAU * f / SR
        saw.append(((ph / TAU) % 1.0) * 2.0 - 1.0)
    f1 = bandpass(saw, 700, 6.0)
    f2 = bandpass(saw, 1500, 8.0)
    env = [math.sin(math.pi * i / n) ** 0.5 for i in range(n)]
    y = mul(mix(scale(f1, 3.0), scale(f2, 2.0)), env)
    y = reverb(y, wet=0.3, size=1.3, tail=0.5)
    save("DoorCreak", fade_edges(y), 0.75)


def drawer():
    n = n_samples(0.7)
    env = [math.sin(math.pi * min(1.0, i / (0.55 * SR))) ** 0.8 if i < 0.55 * SR else 0.0 for i in range(n)]
    scrape = mul(bandpass(noise(n), 650, 0.9), env)
    wood = mul(lowpass(noise(n), 500), env)
    stop = delayed(mul(mix(sine(n_samples(0.15), 130), lowpass(noise(n_samples(0.15)), 600)), env_exp(n_samples(0.15), 0.04)), 0.5, n)
    y = mix(scale(scrape, 0.9), scale(wood, 1.4), scale(stop, 1.3))
    y = reverb(y, wet=0.2, size=0.8, tail=0.3)
    save("Drawer", fade_edges(y), 0.8)


def metal_clang():
    n = n_samples(2.4)
    freqs = [412, 1067, 1731, 2413, 3187, 4290]
    decays = [1.1, 0.7, 0.5, 0.35, 0.25, 0.15]
    gains = [1.0, 0.8, 0.6, 0.5, 0.35, 0.25]
    p = partials(freqs, decays, gains, n)
    hit = mul(highpass(noise(n), 1500), env_exp(n, 0.03, 0.0005))
    y = mix(p, scale(hit, 1.8))
    y = reverb(y, wet=0.35, size=1.6, tail=0.8)
    save("MetalClang", fade_edges(y), 0.95)


def key_jingle():
    n = n_samples(0.9)
    y = [0.0] * n
    for k, (f, d) in enumerate(((2093, 0.35), (2637, 0.3), (3136, 0.28), (4186, 0.2), (2349, 0.3))):
        start = 0.0 + k * 0.055
        tone = partials([f, f * 2.76, f * 5.4], [d, d * 0.6, d * 0.4], [1.0, 0.4, 0.2], n_samples(0.6))
        y = mix(y, delayed(tone, start, n))
    y = reverb(y, wet=0.25, size=0.7, tail=0.3)
    save("Key", fade_edges(y), 0.75)


def pickup():
    n = n_samples(0.35)
    rustle = mul(bandpass(noise(n), 2400, 0.8), [math.sin(math.pi * i / n) ** 2 for i in range(n)])
    tick = mul(sine(n, 520), env_exp(n, 0.05, 0.001))
    y = mix(scale(rustle, 1.6), scale(tick, 0.4))
    save("Pickup", fade_edges(y), 0.7)


def click():
    n = n_samples(0.08)
    y = mix(mul(sine(n, 1800), env_exp(n, 0.012, 0.0005)), mul(noise(n), env_exp(n, 0.006, 0.0003)))
    save("Click", fade_edges(y, 2), 0.6)


def ding():
    n = n_samples(2.2)
    p = partials([880, 2217, 3560, 5000], [1.6, 0.9, 0.5, 0.3], [1.0, 0.35, 0.2, 0.1], n)
    p = reverb(p, wet=0.3, size=1.4, tail=0.6)
    save("Ding", fade_edges(p), 0.8)


def elevator_hum():
    n = n_samples(4.0)
    t = [i / SR for i in range(n)]
    y = [0.0] * n
    for f, g in ((58.0, 1.0), (116.0, 0.5), (174.0, 0.25), (233.0, 0.15)):
        for i in range(n):
            y[i] += math.sin(TAU * f * t[i]) * g * (0.85 + 0.15 * math.sin(TAU * 1.5 * t[i]))
    rumble = scale(lowpass(noise(n), 160), 3.0)
    y = mix(y, rumble)
    y = loopify(y, 0.4)
    save("ElevatorHum", y, 0.6)


def whisper():
    sec = 7.0
    n = n_samples(sec)
    t = [i / SR for i in range(n)]
    base = noise(n)
    v1 = bandpass(base, 1100, 4.0)
    v2 = bandpass(base, 2600, 5.0)
    hiss = highpass(base, 5500)
    out = []
    for i in range(n):
        syll = max(0.0, math.sin(TAU * 3.1 * t[i]) * math.sin(TAU * 0.37 * t[i] + 1.0))
        out.append((v1[i] * 2.5 + v2[i] * 1.6 + hiss[i] * 0.4) * (0.15 + syll))
    out = reverb(out, wet=0.45, size=1.6, tail=0.1)[:n]
    save("Whisper", loopify(out, 0.8), 0.7)


def alarm():
    sec = 2.4
    n = n_samples(sec)
    ph = 0.0
    y = []
    for i in range(n):
        f = 660 if (i // int(0.3 * SR)) % 2 == 0 else 880
        ph += TAU * f / SR
        y.append(math.sin(ph) + 0.5 * math.sin(2 * ph) + 0.25 * math.sin(3 * ph))
    y = distort(y, 2.0)
    y = reverb(y, wet=0.25, size=1.2, tail=0.4)
    save("Alarm", fade_edges(y), 0.8)


def scream():
    sec = 2.4
    n = n_samples(sec)
    random.seed(99)
    voices = []
    for detune in (1.0, 1.012, 0.5):
        ph = 0.0
        v = [0.0] * n
        for i in range(n):
            t = i / SR
            glide = 480 + 520 * min(1.0, t / 0.35) - 120 * max(0.0, (t - 1.4) / 1.0)
            vib = 1.0 + 0.035 * math.sin(TAU * 6.3 * t) + 0.015 * math.sin(TAU * 11.0 * t)
            f0 = glide * vib * detune
            ph += TAU * f0 / SR
            s = 0.0
            k = 1
            while k * f0 < 9000 and k < 30:
                fk = k * f0
                w = math.exp(-((fk - 950) / 380) ** 2) + 0.8 * math.exp(-((fk - 2350) / 600) ** 2) + 0.35 * math.exp(-((fk - 3600) / 800) ** 2)
                s += math.sin(k * ph) * w / (k ** 0.35)
                k += 1
            attack = min(1.0, t / 0.03)
            release = min(1.0, max(0.0, (sec - t) / 0.7))
            v[i] = s * attack * release * (0.7 + 0.3 * math.sin(TAU * 9.0 * t))
        voices.append(v)
    breath = mul(bandpass(noise(n), 3000, 0.7), [min(1.0, i / (0.05 * SR)) * min(1.0, (n - i) / (0.6 * SR)) for i in range(n)])
    y = mix(voices[0], scale(voices[1], 0.7), scale(voices[2], 0.5), scale(breath, 0.9))
    y = distort(y, 3.0)
    y = reverb(y, wet=0.22, size=1.2, tail=0.5)
    save("Scream", fade_edges(y), 0.98)


def sting():
    n = n_samples(2.2)
    clusters = [220, 233.1, 311.1, 349.2, 466.2, 622.3, 987.8]
    p = [0.0] * n
    for f in clusters:
        p = mix(p, mul(sine(n, f), env_exp(n, 1.2, 0.004)))
    hit = mul(lowpass(noise(n), 3000), env_exp(n, 0.08, 0.0008))
    boom = mul(sine(n, 48), env_exp(n, 0.5, 0.002))
    y = mix(scale(p, 0.35), scale(hit, 2.0), scale(boom, 1.6))
    y = distort(y, 1.8)
    y = reverb(y, wet=0.4, size=1.8, tail=1.0)
    save("Sting", fade_edges(y), 0.98)


def lever():
    n = n_samples(0.9)
    thunk = mul(sine(n, 68), env_exp(n, 0.18, 0.001))
    clang = partials([310, 790, 1420, 2200], [0.5, 0.3, 0.2, 0.12], [1.0, 0.7, 0.5, 0.3], n)
    ratchet = mul(bandpass(noise(n), 1800, 2.0), [1.0 if (i // int(0.035 * SR)) % 2 == 0 else 0.15 for i in range(n)])
    ratchet = mul(ratchet, env_exp(n, 0.12, 0.001))
    y = mix(scale(thunk, 1.6), scale(clang, 0.9), scale(ratchet, 0.8))
    y = reverb(y, wet=0.3, size=1.3, tail=0.5)
    save("Lever", fade_edges(y), 0.9)


def power_up():
    n = n_samples(3.0)
    y = []
    ph = 0.0
    for i in range(n):
        t = i / SR
        f = 40 + 260 * (t / 3.0) ** 2
        ph += TAU * f / SR
        y.append(math.sin(ph) * min(1.0, t / 0.3) + 0.4 * math.sin(2 * ph))
    buzz = mul(lowpass(noise(n), 1800), [0.2 + 0.8 * (i / n) for i in range(n)])
    pops = [0.0] * n
    for _ in range(14):
        s = int(random.uniform(0.5, 2.8) * SR)
        m = n_samples(0.06)
        pop = mul(highpass(noise(m), 2000), env_exp(m, 0.01, 0.0005))
        for j in range(m):
            pops[s + j] += pop[j] * 0.7
    out = mix(scale(y, 0.6), scale(buzz, 0.5), pops)
    out = reverb(out, wet=0.3, size=1.5, tail=0.6)
    save("PowerUp", fade_edges(out), 0.9)


def growl():
    sec = 3.2
    n = n_samples(sec)
    ph = 0.0
    y = []
    for i in range(n):
        t = i / SR
        f = 58 + 22 * math.sin(TAU * 0.7 * t) + 14 * math.sin(TAU * 23 * t)
        ph += TAU * f / SR
        s = ((ph / TAU) % 1.0) * 2.0 - 1.0
        y.append(s)
    rough = bandpass(y, 260, 2.0)
    rough2 = bandpass(y, 740, 3.0)
    hiss = mul(bandpass(noise(n), 2200, 1.0), [0.4 + 0.6 * abs(math.sin(TAU * 4.3 * i / SR)) for i in range(n)])
    env = [min(1.0, i / (0.25 * SR)) * min(1.0, (n - i) / (0.9 * SR)) for i in range(n)]
    out = mul(mix(scale(rough, 2.2), scale(rough2, 1.4), scale(hiss, 0.25)), env)
    out = distort(out, 2.5)
    out = highpass(out, 30)   # 톱니파 왜곡에서 생기는 직류 성분 제거
    out = reverb(out, wet=0.3, size=1.5, tail=0.4)
    save("Growl", fade_edges(out), 0.9)


if __name__ == "__main__":
    for fn in (heartbeat, footsteps, door_slide, door_creak, drawer, metal_clang, key_jingle, pickup, click, ding,
               elevator_hum, whisper, alarm, scream, sting, lever, power_up, growl, amb_drone):
        fn()
    print("done")
