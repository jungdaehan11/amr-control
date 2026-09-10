"""
예지보전 판정 검증 스크립트
- 실측 CSV(정상/부하/이물질, 각 3세트)에 W=20 슬라이딩 윈도우를 적용
- 윈도우 평균 current_diff를 경계값으로 분류하고, 혼동행렬·정확도를 산출

데이터 정제 / 판정 결정 과정:
  1) 주행 구간만: command == FORWARD 만 분석 (정지 STOP 구간 제외)
  2) 꼬리 0 블록 제거: 주행 종료 후 전류가 0으로 떨어졌는데 로깅이 마지막 명령을
     유지한 채 계속 기록된 '꼬리 연속 0' 구간을 제거.
     (정상 데이터에서 이 0 블록이 표준편차를 부풀렸음 - 로깅 종료 처리 문제, 재현 확인됨)
  3) 판정 방식 재선정:
     - 초기 2분 데이터에선 순간 스파이크 때문에 '중앙값'이 유리했으나,
     - 데이터를 약 8분(3세트, 4배)으로 확대해 재검증한 결과,
       스파이크 영향이 희석되고 부하-정상 경계 구분이 더 중요해져
       '평균 + 경계 9.4'가 최적(정확도 98.5%)임을 확인.
     - 데이터 규모에 따라 최적 하이퍼파라미터가 달라진다는 것을 관찰.

경계값 근거 (윈도우 평균 기준, 총 4,555 윈도우):
  정상   8.95 이하 구간에 집중 (평균 ~8.6)
  부하   8.95 ~ 11.35        (평균 ~10.2)
  이물질 13.20 ~ 15.65       (평균 ~14.4)

  LOW=9.4:  정상 오탐(2.4%)과 부하 놓침(2.3%)이 균형을 이루는 최적점.
            정상과 부하는 분포가 겹쳐 완전 분리가 불가능하므로 두 오류의 균형점을 택함.
  HIGH=12.0: 부하 최대(11.35)와 이물질 최소(13.20) 사이의 빈 구간.
            두 분포가 겹치지 않아 이물질 재현율 100%를 달성.

주의: 이 판정 규칙은 adsmartcar/Form1.cs 의 Classify() 와 동일하게 유지해야 한다.
      한쪽만 바꾸면 검증 결과와 실제 앱 동작이 어긋난다.
"""
import pandas as pd
import numpy as np
import os

BASE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(BASE, "data")
FILES = {
    "정상":   ["forward.csv", "forward2.csv", "forward3.csv"],
    "부하":   ["forward_load.csv", "forward_load2.csv", "forward_load3.csv"],
    "이물질": ["forward_debris.csv", "forward_debris2.csv", "forward_debris3.csv"],
}
W = 20                    # 슬라이딩 윈도우 크기
LOW, HIGH = 9.4, 12.0     # 판정 경계
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
    return s


def classify(value):
    if value < LOW:
        return "정상"
    elif value < HIGH:
        return "부하"
    return "이물질"


def confusion_matrix(method="mean"):
    """method: 'mean'(최종) 또는 'median'(초기 소량 데이터용)"""
    cm = {a: {b: 0 for b in LABELS} for a in LABELS}
    for true_label, files in FILES.items():
        for fname in files:
            s = load_series(os.path.join(DATA, fname))
            for i in range(len(s) - W + 1):
                win = s[i:i + W]
                rep = np.median(win) if method == "median" else win.mean()
                cm[true_label][classify(rep)] += 1
    return cm


def report(cm, title):
    print(f"=== {title} ===\n")
    # f-string 안에서는 백슬래시를 쓸 수 없다 (Python 3.11 이하 SyntaxError)
    header = "실제\\예측"
    print(f"{header:<8}" + "".join(f"{l:>7}" for l in LABELS) + f"{'합계':>7}")
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
    print(f"정상 오탐률: {nw/sum(cm['정상'].values())*100:.1f}%  ({nw}/{sum(cm['정상'].values())})")
    lm = cm["부하"]["정상"]
    print(f"부하 놓침(부하→정상): {lm/sum(cm['부하'].values())*100:.1f}%  ({lm}/{sum(cm['부하'].values())})\n")


def stats():
    print("=== 상태별 통계 (3세트 합산, 꼬리 0 제거) ===")
    print(f"{'상태':<8}{'개수':>7}{'평균':>8}{'표준편차':>10}")
    for t, files in FILES.items():
        s = np.concatenate([load_series(os.path.join(DATA, f)) for f in files])
        print(f"{t:<8}{len(s):>7}{s.mean():>8.2f}{s.std():>10.2f}")
    print()


if __name__ == "__main__":
    stats()
    report(confusion_matrix("mean"), "판정: 윈도우 평균 (최종, LOW=9.4)")