"""층마다 다른 공포 배경음과 불시에 들리는 괴이한 소리를 합성한다 (make_audio.py의 도구를 가져다 쓴다).
실행: python devscripts/make_floor_audio.py  ->  Assets/Resources/Audio/Amb_F0~F4.wav, Ev_*.wav

층 번호는 TowerLayout과 같다 (0=지하2 ... 4=3층). 위층일수록 순하고, 지하2층이 가장 괴이하다.
  F4 3층   불탄 자   : 먼 바람, 타닥이는 불씨, 이따금 삐걱임             (제일 약함)
  F3 2층   구속복    : 낮은 웅웅, 먼 신음, 쇠사슬, 긁는 소리
  F2 1층   살덩이    : 맥박 같은 저음, 질척이는 방울, 거친 숨, 신음
  F1 지하1 집도의    : 불협화 고음, 째깍거림, 톱 긁는 소리, 속삭임
  F0 지하2 익사체    : 깊은 저음 맥놀이, 물방울, 물속 기포, 합창 같은 신음, 이명, 심장 박동   (제일 무서움)
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_audio import (SR, TAU, n_samples, noise, lowpass, highpass, bandpass, env_exp, mul, scale, mix, pad, sine,
                        delayed, reverb, distort, fade_edges, loopify, save, partials)


def put(buf, sig, start_sec, gain=1.0):
    s = int(start_sec * SR)
    for i, v in enumerate(sig):
        j = s + i
        if 0 <= j < len(buf):
            buf[j] += v * gain


def voice(sec, f_start, f_end, formants, vib=0.03, rough=0.0, breath=0.15):
    """사람 같은 신음 한 번: 성대음 + 모음(포먼트) + 흔들림. rough가 크면 쉰 소리."""
    n = n_samples(sec)
    ph = 0.0
    src = []
    for i in range(n):
        t = i / SR
        k = i / n
        f = (f_start + (f_end - f_start) * k) * (1.0 + vib * math.sin(TAU * 5.2 * t) + rough * random.uniform(-1, 1) * 0.02)
        ph += TAU * f / SR
        src.append(((ph / TAU) % 1.0) * 2.0 - 1.0)
    out = [0.0] * n
    for fc, q, g in formants:
        band = bandpass(src, fc, q)
        for i in range(n):
            out[i] += band[i] * g
    br = bandpass(noise(n), 1800, 0.8)
    env = [(math.sin(math.pi * i / n) ** 1.3) for i in range(n)]
    return mul([out[i] + br[i] * breath for i in range(n)], env)


OO = ((330, 4.0, 1.6), (800, 5.0, 0.9))
AH = ((700, 4.0, 1.6), (1150, 5.0, 1.0), (2500, 6.0, 0.4))


def drone(n, freqs, lfo_rate=0.05):
    y = [0.0] * n
    for k, (f, g) in enumerate(freqs):
        for i in range(n):
            t = i / SR
            y[i] += math.sin(TAU * f * t + 1.5 * math.sin(TAU * 0.02 * t + k)) * g * (0.65 + 0.35 * math.sin(TAU * lfo_rate * (1 + k * 0.3) * t + k))
    return y


def wind(n, fc, depth, speed=0.08):
    w = bandpass(noise(n), fc, 0.8)
    return [v * (1 - depth + depth * (0.5 + 0.5 * math.sin(TAU * speed * i / SR + 1.0))) for i, v in enumerate(w)]


def creak(dur, f0):
    m = n_samples(dur)
    ph = 0.0
    out = []
    for i in range(m):
        f = f0 * (1.0 - 0.35 * i / m) * (1 + 0.04 * math.sin(TAU * 9 * i / SR))
        ph += TAU * f / SR
        out.append((((ph / TAU) % 1.0) * 2 - 1) * math.sin(math.pi * i / m) ** 2)
    return bandpass(out, f0 * 2.2, 5.0)


def drip(f=1100, decay=0.08):
    m = n_samples(0.5)
    ph = 0.0
    out = []
    for i in range(m):
        t = i / SR
        ph += TAU * (f * (1 + 0.9 * math.exp(-t / 0.012))) / SR
        out.append(math.sin(ph) * math.exp(-t / decay))
    return out


def bloop(f_hi=700, f_lo=180, dur=0.14):
    m = n_samples(dur)
    ph = 0.0
    out = []
    for i in range(m):
        k = i / m
        ph += TAU * (f_hi + (f_lo - f_hi) * k) / SR
        out.append(math.sin(ph) * math.sin(math.pi * k) ** 0.6)
    return lowpass(out, 1200)


def pop(m_sec=0.01, fc=3000):
    m = n_samples(m_sec)
    return mul(highpass(noise(m), fc), env_exp(m, m_sec / 4, 0.0003))


def heartbeat_low(n, bpm):
    y = [0.0] * n
    period = 60.0 / bpm
    t = 0.2
    while t < n / SR:
        for off, g in ((0.0, 1.0), (0.28, 0.7)):
            m = n_samples(0.25)
            th = mul(sine(m, 50), env_exp(m, 0.08, 0.004))
            put(y, th, t + off, g)
        t += period
    return lowpass(y, 160)


# ---------- 층별 배경음 ----------

def floor4():   # 3층: 불탄 자 - 가장 약함
    random.seed(4001)
    n = n_samples(18.0)
    layers = [scale(drone(n, ((43.0, 0.2), (64.5, 0.1))), 0.6), scale(wind(n, 380, 0.7), 0.16)]
    ev = [0.0] * n
    for _ in range(int(18 * 22)):
        put(ev, pop(random.uniform(0.004, 0.012), random.uniform(2500, 5500)), random.uniform(0, 18), random.uniform(0.05, 0.25))
    put(ev, creak(1.4, 230), 5.0, 0.07)
    put(ev, creak(1.1, 300), 13.0, 0.05)
    layers.append(ev)
    y = reverb(mix(*layers), wet=0.25, size=2.0, tail=0.1)[:n]
    save("Amb_F4", loopify(y, 1.5), 0.55)


def floor3():   # 2층: 구속복 환자 - 먼 신음, 쇠사슬, 긁는 소리
    random.seed(3002)
    n = n_samples(20.0)
    layers = [scale(drone(n, ((46.0, 0.25), (48.0, 0.2), (92.0, 0.08))), 0.7), scale(wind(n, 300, 0.6), 0.18)]
    ev = [0.0] * n
    for st in (4.0, 12.5):
        put(ev, lowpass(voice(2.4, 130, 95, OO, 0.04, 0.4, 0.1), 900), st, 0.45)
    for st in (8.0, 16.0):
        for k in range(7):   # 쇠사슬: 짧은 쇠 소리가 연달아
            ping = partials([random.uniform(1400, 2600), random.uniform(3400, 5200)], [0.08, 0.05], [1.0, 0.5], n_samples(0.25))
            put(ev, ping, st + k * random.uniform(0.05, 0.12), 0.07)
    for st in (2.0, 10.5, 17.5):   # 긁는 소리: 느린 리듬의 마찰
        m = n_samples(1.6)
        sc = mul(bandpass(noise(m), 2600, 1.2), [(0.2 + 0.8 * abs(math.sin(TAU * 2.6 * i / SR))) * math.sin(math.pi * i / m) for i in range(m)])
        put(ev, sc, st, 0.1)
    layers.append(lowpass(ev, 5000))
    y = reverb(mix(*layers), wet=0.3, size=2.2, tail=0.1)[:n]
    save("Amb_F3", loopify(y, 1.5), 0.62)


def floor2():   # 1층: 살덩이 - 맥박, 질척임, 거친 숨
    random.seed(2003)
    n = n_samples(20.0)
    throb = [math.sin(TAU * 38 * i / SR) * (0.35 + 0.65 * max(0.0, math.sin(TAU * 0.85 * i / SR)) ** 3) for i in range(n)]
    layers = [scale(throb, 0.9), scale(drone(n, ((41.0, 0.2), (61.0, 0.1))), 0.5), scale(wind(n, 260, 0.5), 0.18)]
    ev = [0.0] * n
    for st in [1.0 + k * random.uniform(1.0, 2.6) for k in range(10)]:
        put(ev, bloop(random.uniform(500, 900), random.uniform(120, 220), random.uniform(0.1, 0.22)), st, random.uniform(0.15, 0.35))
    for st in (3.0, 7.0, 11.0, 15.0):   # 거친 숨: 들이쉬고 내쉰다
        m = n_samples(2.2)
        br = mul(bandpass(noise(m), 900, 1.0), [math.sin(math.pi * i / m) ** 2 * (0.6 + 0.4 * math.sin(TAU * 18 * i / SR)) for i in range(m)])
        put(ev, br, st, 0.22)
    for st in (6.0, 16.5):
        put(ev, lowpass(voice(2.8, 100, 70, AH, 0.05, 0.8, 0.2), 1100), st, 0.5)
    layers.append(ev)
    y = reverb(mix(*layers), wet=0.3, size=2.0, tail=0.1)[:n]
    save("Amb_F2", loopify(y, 1.5), 0.68)


def floor1():   # 지하1: 집도의 - 불협화 고음, 째깍거림, 톱질, 속삭임
    random.seed(1004)
    n = n_samples(20.0)
    cluster = [0.0] * n
    for f, g in ((1760.0, 0.05), (1865.0, 0.05), (2349.0, 0.035), (3322.0, 0.02)):
        for i in range(n):
            t = i / SR
            cluster[i] += math.sin(TAU * f * t) * g * (0.5 + 0.5 * math.sin(TAU * 0.11 * t + f))
    layers = [drone(n, ((44.0, 0.28), (46.5, 0.28), (88.0, 0.1))), scale(wind(n, 420, 0.7), 0.16), cluster]
    ev = [0.0] * n
    t = 0.5
    while t < 19.5:   # 째깍
        m = n_samples(0.03)
        put(ev, mul(bandpass(noise(m), 3200, 3.0), env_exp(m, 0.006, 0.0005)), t, 0.35)
        t += 1.0
    for st in (4.2, 12.0, 17.2):   # 톱질: 쇠톱이 앞뒤로 밀리는 마찰
        m = n_samples(1.8)
        ph = 0.0
        saw = []
        for i in range(m):
            tt = i / SR
            ph += TAU * (900 + 300 * math.sin(TAU * 3.4 * tt)) / SR
            saw.append((((ph / TAU) % 1.0) * 2 - 1) * (0.3 + 0.7 * abs(math.sin(TAU * 3.4 * tt))) * math.sin(math.pi * i / m))
        put(ev, bandpass(saw, 2200, 1.2), st, 0.12)
    base = noise(n)
    whisper = highpass(mix(bandpass(base, 1200, 4.0), bandpass(base, 2800, 5.0)), 600)
    wenv = [max(0.0, math.sin(TAU * 2.9 * i / SR) * math.sin(TAU * 0.23 * i / SR + 2.0)) ** 1.5 for i in range(n)]
    layers.append(scale(mul(whisper, wenv), 0.4))
    layers.append(ev)
    y = reverb(mix(*layers), wet=0.35, size=2.4, tail=0.1)[:n]
    save("Amb_F1", loopify(y, 1.5), 0.75)


def floor0():   # 지하2: 익사체 - 가장 괴이함
    random.seed(7005)
    n = n_samples(22.0)
    sub = drone(n, ((31.0, 0.45), (33.2, 0.45), (62.0, 0.15), (66.4, 0.12)))
    layers = [scale(sub, 1.1), scale(wind(n, 240, 0.8, 0.06), 0.22), scale(heartbeat_low(n, 66), 0.5)]
    ev = [0.0] * n
    # 물방울: 길게 울린다
    t = 0.8
    while t < 21:
        put(ev, drip(random.uniform(800, 1700), random.uniform(0.05, 0.1)), t, random.uniform(0.15, 0.4))
        t += random.uniform(1.4, 3.8)
    # 물속 기포
    for _ in range(40):
        put(ev, bloop(random.uniform(300, 700), random.uniform(80, 200), random.uniform(0.05, 0.14)), random.uniform(0, 21), random.uniform(0.1, 0.25))
    # 합창 같은 신음: 음정이 어긋난 목소리가 겹쳐 부풀어 오른다
    for st in (2.5, 11.0):
        for f0, f1, g in ((118, 80, 0.4), (126, 96, 0.35), (210, 150, 0.22), (88, 60, 0.3)):
            put(ev, lowpass(voice(random.uniform(5.0, 6.5), f0, f1, OO, 0.06, 0.6, 0.1), 1400), st + random.uniform(0, 1.2), g)
    # 가까이서 속삭이는 아이 같은 높은 목소리
    for st in (7.0, 17.0):
        put(ev, lowpass(voice(3.2, 520, 430, AH, 0.08, 0.3, 0.3), 2500), st, 0.09)
    # 이명 (점점 커졌다 사라진다)
    ring = [math.sin(TAU * 6500 * i / SR) * 0.03 * max(0.0, math.sin(TAU * 0.07 * i / SR - 0.5)) ** 2 for i in range(n)]
    # 역재생 스웰: 서서히 커지다 뚝 끊기는 소리
    for st in (5.0, 14.5):
        m = n_samples(3.0)
        sw = mul(bandpass(noise(m), 700, 1.5), [(i / m) ** 3 for i in range(m)])
        put(ev, sw, st, 0.25)
    layers += [ev, ring]
    y = reverb(mix(*layers), wet=0.45, size=3.0, tail=0.1)[:n]
    save("Amb_F0", loopify(y, 1.8), 0.85)


# ---------- 불시에 들리는 소리 (코드가 층마다 정해진 간격으로 사방에서 들려 준다) ----------

def ev_moan():
    random.seed(31)
    y = mix(voice(2.8, 140, 85, OO, 0.05, 0.5, 0.1), scale(voice(2.8, 148, 90, AH, 0.05, 0.5, 0.1), 0.6))
    save("Ev_Moan", fade_edges(reverb(lowpass(y, 1800), wet=0.4, size=2.0, tail=0.5)), 0.8)


def ev_scrape():
    n = n_samples(2.0)
    ph = 0.0
    s = []
    for i in range(n):
        t = i / SR
        ph += TAU * (500 + 250 * math.sin(TAU * 1.3 * t)) / SR
        s.append((((ph / TAU) % 1.0) * 2 - 1) * (0.4 + 0.6 * abs(math.sin(TAU * 5.0 * t))) * math.sin(math.pi * i / n) ** 0.7)
    y = mix(bandpass(s, 1800, 1.5), scale(mul(bandpass(noise(n), 3500, 1.0), [math.sin(math.pi * i / n) for i in range(n)]), 0.4))
    save("Ev_Scrape", fade_edges(reverb(y, wet=0.3, size=1.6, tail=0.6)), 0.7)


def ev_drip():
    y = [0.0] * n_samples(2.5)
    for k, st in enumerate((0.0, 0.9, 1.6)):
        put(y, drip(random.uniform(900, 1500), 0.08), st, 1.0 - k * 0.25)
    save("Ev_Drip", fade_edges(reverb(y, wet=0.5, size=2.5, tail=0.8)), 0.7)


def ev_knock():
    y = [0.0] * n_samples(1.6)
    for st in (0.0, 0.32, 0.55):
        m = n_samples(0.25)
        put(y, mix(mul(sine(m, 110), env_exp(m, 0.05, 0.001)), mul(lowpass(noise(m), 900), env_exp(m, 0.02, 0.001))), st, 1.0)
    save("Ev_Knock", fade_edges(reverb(y, wet=0.35, size=1.5, tail=0.6)), 0.8)


def ev_slam():
    n = n_samples(2.2)
    y = mix(mul(sine(n, 62), env_exp(n, 0.25, 0.002)), scale(mul(lowpass(noise(n), 1400), env_exp(n, 0.05, 0.001)), 1.4),
            scale(partials([180, 410, 880], [0.5, 0.3, 0.2], [1.0, 0.6, 0.4], n), 0.4))
    save("Ev_Slam", fade_edges(reverb(y, wet=0.5, size=2.4, tail=0.8)), 0.85)


if __name__ == "__main__":
    for fn in (floor4, floor3, floor2, floor1, floor0, ev_moan, ev_scrape, ev_drip, ev_knock, ev_slam):
        fn()
