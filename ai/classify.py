"""
예지보전 판정 검증 스크립트
- 실측 CSV(정상/부하/이물질)에 W=20 슬라이딩 윈도우를 적용
- 윈도우 평균 current_diff로 상태를 판정하고, 혼동행렬·정확도를 산출

경계값 근거:
  정상 윈도우 평균 중앙값 8.6, 부하 10.2 → 중간점 약 9.5 (정상/부하 경계)
  부하 최대 11.1, 이물질 최소 13.2 → 그 사이 12.0 (부하/이물질 경계)
  (정확도 최적화가 아니라 두 분포의 중간점을 경계로 사용)
"""
import pandas as pd
import os

BASE = os.path.dirname(os.path.abspath(__file__))
FILES = {
    "정상":   os.path.join(BASE, "data", "forward.csv"),
    "부하":   os.path.join(BASE, "data", "forward_load.csv"),
    "이물질": os.path.join(BASE, "data", "forward_debris.csv"),
}
W = 20          # 슬라이딩 윈도우 크기
LOW, HIGH = 9.5, 12.0   # 판정 경계 (정상 < 9.5 <= 부하 < 12.0 <= 이물질)
LABELS = ["정상", "부하", "이물질"]


def classify(mean_val):
    if mean_val < LOW:
        return "정상"
    elif mean_val < HIGH:
        return "부하"
    return "이물질"


def main():
    cm = {a: {b: 0 for b in LABELS} for a in LABELS}

    for true_label, path in FILES.items():
        df = pd.read_csv(path)
        fwd = df[df["command"] == "FORWARD"]["current_diff"].reset_index(drop=True)
        # 겹치는 슬라이딩 윈도우(stride=1)
        for i in range(len(fwd) - W + 1):
            pred = classify(fwd[i:i + W].mean())
            cm[true_label][pred] += 1

    # 혼동행렬 출력
    print(f"=== 혼동행렬 (W={W}, 경계 {LOW}/{HIGH}) ===\n")
    print(f"{'실제\\예측':<8}" + "".join(f"{l:>7}" for l in LABELS) + f"{'합계':>7}")
    total = correct = 0
    for a in LABELS:
        row = cm[a]
        s = sum(row.values())
        total += s
        correct += row[a]
        print(f"{a:<8}" + "".join(f"{row[b]:>7}" for b in LABELS) + f"{s:>7}")

    print(f"\n총 윈도우: {total}")
    print(f"전체 정확도: {correct/total*100:.1f}%  ({correct}/{total})")
    for a in LABELS:
        s = sum(cm[a].values())
        print(f"{a} 재현율: {cm[a][a]/s*100:.1f}%  ({cm[a][a]}/{s})")
    n_wrong = cm["정상"]["부하"] + cm["정상"]["이물질"]
    n_total = sum(cm["정상"].values())
    print(f"정상 오탐률: {n_wrong/n_total*100:.1f}%  ({n_wrong}/{n_total})")


if __name__ == "__main__":
    main()
