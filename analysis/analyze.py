import pandas as pd
import matplotlib.pyplot as plt
import os

base = os.path.dirname(os.path.abspath(__file__))

# 세 가지 데이터 읽기
normal = pd.read_csv(os.path.join(base, "data", "forward.csv"))
load   = pd.read_csv(os.path.join(base, "data", "forward_load.csv"))
debris = pd.read_csv(os.path.join(base, "data", "forward_debris.csv"))

# 주행(FORWARD) 구간만
n_fwd = normal[normal["command"] == "FORWARD"]["current_diff"]
l_fwd = load[load["command"] == "FORWARD"]["current_diff"]
d_fwd = debris[debris["command"] == "FORWARD"]["current_diff"]

# ===== 통계 비교 =====
print("=== FORWARD 구간 통계 비교 ===")
print(f"{'상태':<10}{'평균':>8}{'표준편차':>10}{'최소':>6}{'최대':>6}")
print(f"{'정상':<10}{n_fwd.mean():>8.2f}{n_fwd.std():>10.2f}{n_fwd.min():>6}{n_fwd.max():>6}")
print(f"{'부하(무게)':<10}{l_fwd.mean():>8.2f}{l_fwd.std():>10.2f}{l_fwd.min():>6}{l_fwd.max():>6}")
print(f"{'이물질(테이프)':<10}{d_fwd.mean():>8.2f}{d_fwd.std():>10.2f}{d_fwd.min():>6}{d_fwd.max():>6}")

# ===== 그래프 =====
plt.figure(figsize=(12, 8))

# 1. 시계열 파형 (셋 다)
plt.subplot(3, 1, 1)
plt.plot(normal["elapsed_ms"]/1000, normal["current_diff"], color="green", linewidth=1, label="Normal")
plt.title("Normal - Waveform")
plt.ylabel("Current diff")
plt.ylim(0, 25)
plt.grid(True, alpha=0.3)

plt.subplot(3, 1, 2)
plt.plot(load["elapsed_ms"]/1000, load["current_diff"], color="blue", linewidth=1, label="Load")
plt.title("Load (weight) - Waveform")
plt.ylabel("Current diff")
plt.ylim(0, 25)
plt.grid(True, alpha=0.3)

plt.subplot(3, 1, 3)
plt.plot(debris["elapsed_ms"]/1000, debris["current_diff"], color="red", linewidth=1, label="Debris")
plt.title("Debris (tape) - Waveform")
plt.xlabel("Time (s)")
plt.ylabel("Current diff")
plt.ylim(0, 25)
plt.grid(True, alpha=0.3)

plt.tight_layout()
plt.show()