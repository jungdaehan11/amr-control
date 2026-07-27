import pandas as pd
import matplotlib.pyplot as plt
import os

base = os.path.dirname(os.path.abspath(__file__))

# 정상 직진 / 부하 직진 데이터 읽기
normal = pd.read_csv(os.path.join(base, "data", "forward.csv"))
load   = pd.read_csv(os.path.join(base, "data", "forward_load.csv"))

# 주행(FORWARD) 구간만 뽑기 - 정지 구간 제외
normal_fwd = normal[normal["command"] == "FORWARD"]["current_diff"]
load_fwd   = load[load["command"] == "FORWARD"]["current_diff"]

# ===== 통계 비교 =====
print("=== 정상 직진 (FORWARD만) ===")
print(f"평균: {normal_fwd.mean():.2f}   표준편차: {normal_fwd.std():.2f}   최대: {normal_fwd.max()}")

print("\n=== 부하 직진 (FORWARD만) ===")
print(f"평균: {load_fwd.mean():.2f}   표준편차: {load_fwd.std():.2f}   최대: {load_fwd.max()}")

# ===== 그래프: 두 파형 겹쳐 그리기 =====
plt.figure(figsize=(12, 6))

# 위쪽: 시계열 파형 비교
plt.subplot(2, 1, 1)
plt.plot(normal["elapsed_ms"]/1000, normal["current_diff"], color="green", linewidth=1, label="Normal")
plt.plot(load["elapsed_ms"]/1000, load["current_diff"], color="red", linewidth=1, alpha=0.7, label="Load")
plt.title("Normal vs Load - Current Waveform")
plt.xlabel("Time (s)")
plt.ylabel("Current diff")
plt.legend()
plt.grid(True, alpha=0.3)

# 아래쪽: 분포 히스토그램 비교 (주행 구간만)
plt.subplot(2, 1, 2)
plt.hist(normal_fwd, bins=20, color="green", alpha=0.5, label="Normal (FORWARD)")
plt.hist(load_fwd, bins=20, color="red", alpha=0.5, label="Load (FORWARD)")
plt.title("Current Distribution (FORWARD only)")
plt.xlabel("Current diff")
plt.ylabel("Count")
plt.legend()
plt.grid(True, alpha=0.3)

plt.tight_layout()
plt.show()