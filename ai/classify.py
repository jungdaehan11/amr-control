"""
예지보전 판정 검증 스크립트
- 실측 CSV(정상/부하/이물질)에 W=20 슬라이딩 윈도우를 적용
- 윈도우 대표값(평균/중앙값)으로 상태를 판정하고, 혼동행렬·정확도를 산출

적용된 데이터 정제 / 판정 개선:
  1) 주행 구간만: command == FORWARD 만 분석 (정지 STOP 구간 제외)
  2) 꼬리 0 블록 제거: 주행 종료 후 전류가 0으로 떨어졌는데 로깅이 마지막 명령을
     유지한 채 계속 기록된 '꼬리 연속 0' 구간을 제거.
     (정상 데이터에서 이 0 블록 25개가 표준편차를 2.70으로 부풀렸음 -> 제거 시 1.66)
  3) 중앙값 판정: 정상 오탐의 원인이 순간 전류 스파이크(예: 0 -> 16 -> 21)임을 확인,
     스파이크에 강건한 중앙값(median)으로 대표값을 교체.
     (평균 대비 정상 오탐 4.9% -> 1.5%, 정확도 96.4% -> 97.5%)

경계값 근거(정확도 최적화가 아닌 도메인 판단):
  정상 윈도우 중앙값 ~8.6, 부하 ~10.2 -> 중간점 9.5 (정상/부하 경계)
  부하 최대 ~11.1, 이물질 최소 ~13.2 -> 그 사이 12.0 (부하/이물질 경계)
  경계를 9.6으로 올리면 정상 오탐 0%가 가능하나, 부하(이상)를 놓치는 비율이 증가.
  예지보전에서는 '거짓 경보'보다 '이상 놓침'이 더 위험하므로,
  전체 정확도가 가장 높고 이상 놓침이 최소인 9.5를 선택.
"""
import pandas as pd
import numpy as np
import os

BASE = os.path.dirname(os.path.abspath(__file__))
FILES = {
    "정상":   os.path.join(BASE, "data", "forward.csv"),
    "부하":   os.path.join(BASE, "data", "forward_load.csv"),
    "이물질": os.path.join(BASE, "data", "forward_debris.csv"),
}
W = 20                    # 슬라이딩 윈도우 크기
LOW, HIGH = 9.5, 12.0     # 판정 경계
LABELS = ["정상", "부하", "이물질"]


def load_series(path):
    """FORWARD 구간만 추출 + 주행 종료 후 꼬리 연속 0 블록 제거"""
    s = pd.read_csv(path)
    s = s[s["command"] == "FORWARD"]["current_diff"].reset_index(drop=True).values
    tail = 0
    for v in reversed(s):
        if v == 0:
            tail += 1
        else:
            break
    if tail > 0:
        s = s[:len(s) - tail]
    return pd.Series(s)


def classify(value):
    if value < LOW:
        return "정상"
    elif value < HIGH:
        return "부하"
    return "이물질"


def confusion_matrix(method="median"):
    """method: 'median'(개선) 또는 'mean'(초기)"""
    cm = {a: {b: 0 for b in LABELS} for a in LABELS}
    for true_label, path in FILES.items():
        s = load_series(path)
        for i in range(len(s) - W + 1):
            win = s[i:i + W].values
            rep = np.median(win) if method == "median" else win.mean()
            cm[true_label][classify(rep)] += 1
    return cm


def report(cm, title):
    print(f"=== {title} ===\n")
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
    nw = cm["정상"]["부하"] + cm["정상"]["이물질"]
    ns = sum(cm["정상"].values())
    print(f"정상 오탐률: {nw/ns*100:.1f}%  ({nw}/{ns})\n")


def stats():
    print("=== 상태별 통계 (꼬리 0 제거 후) ===")
    print(f"{'상태':<8}{'평균':>8}{'표준편차':>10}")
    for t, path in FILES.items():
        s = load_series(path)
        print(f"{t:<8}{s.mean():>8.2f}{s.std():>10.2f}")
    print()


if __name__ == "__main__":
    stats()
    report(confusion_matrix("mean"),   "초기: 평균 판정")
    report(confusion_matrix("median"), "개선: 중앙값 판정 (스파이크 강건)")