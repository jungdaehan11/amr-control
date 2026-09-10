"""
상태 진단 판정 검증 스크립트
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
       '평균 + 경계 9.4'가 최적임을 확인.
     - 데이터 규모에 따라 최적 하이퍼파라미터가 달라진다는 것을 관찰.
  4) 구동 구간 단위 윈도우 (아래 참조)

★ 4) 구동 구간 단위 윈도우 — 왜 구간을 이어붙이지 않는가
  한 CSV 파일에는 FORWARD 구간이 여러 번 나타난다(주행 → 정지 → 주행 → ...).
  이전 버전은 이 구간들을 하나로 이어붙인 뒤 슬라이딩 윈도우를 만들었기 때문에,
  윈도우가 '구간 경계'를 넘나들며 앞 주행의 끝과 다음 주행의 시작을 한 윈도우에 섞었다.

  문제는 구간 시작마다 '모터 기동 돌입전류'가 나타난다는 점이다.
  정지 상태의 DC 모터는 역기전력(E=k*omega)이 0이라 기동 순간 전류가 V/R 까지 치솟고,
  회전이 붙으면서 역기전력이 커져 정상값으로 내려온다.
  실측에서도 구간 시작값이 본체 평균의 최대 2.58배까지 튀고 곧바로 감쇠한다.
    예) 13 -> 11 -> 10 -> 10 -> 9 ...   (본체 평균 약 8.5)

  전체 데이터 기준 구간 경계가 59개, 경계를 넘나드는 윈도우가 1,121개였고,
  이것이 정상 오탐의 실제 원인이었다. (그동안 '순간 스파이크'로만 설명하던 현상)

  구간을 분리해 윈도우를 만들면:
    정상 오탐률  2.43% -> 0.41%   /  전체 정확도  98.51% -> 99.29%

  "하나의 윈도우가 서로 다른 주행을 섞으면 안 된다"는 것은 데이터를 보기 전에도
  참인 명제이므로, 튜닝이 아니라 원칙적인 수정으로 판단해 채택했다.

경계값 근거 (윈도우 평균 기준):
  정상   8.95 이하 구간에 집중 (평균 ~8.6)
  부하   8.95 ~ 11.35        (평균 ~10.2)
  이물질 13.20 ~ 15.65       (평균 ~14.4)

  LOW=9.4:  정상 오탐과 부하 놓침이 균형을 이루는 최적점.
            정상과 부하는 분포가 겹쳐 완전 분리가 불가능하므로 두 오류의 균형점을 택함.
  HIGH=12.0: 부하 최대(11.35)와 이물질 최소(13.20) 사이의 빈 구간.
            두 분포가 겹치지 않아 이물질 재현율 100%를 달성.

일반화 성능 (classify_cv.py 참조):
  경계값은 전체 데이터로 탐색했으므로 in-sample 수치는 낙관적으로 편향된다.
  세트 단위 3-fold 교차검증(Leave-One-Set-Out) 결과 과적합 폭은 약 1.2%p 수준으로
  제한적이었고, 이물질 재현율은 모든 fold에서 100%였다.

주의: 이 판정 규칙과 '구간 단위 윈도우' 방식은 adsmartcar/Form1.cs 의 Classify() 및
      윈도우 버퍼 처리와 동일하게 유지해야 한다.
      한쪽만 바꾸면 검증 결과와 실제 앱 동작이 어긋난다.
      -> Form1.cs 는 주행 명령이 바뀌면(구간이 끊기면) 윈도우 버퍼를 비워야 한다.
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


def _strip_tail_zeros(seg):
    """주행 종료 후 전류가 0인데 명령만 유지된 '꼬리 0 블록' 제거"""
    tail = 0
    for v in reversed(seg):
        if v == 0:
            tail += 1
        else:
            break
    return seg[:len(seg) - tail] if tail else seg


def load_segments(path):
    """
    FORWARD 연속 구간을 각각 '분리된 배열'로 반환한다.
    구간별로 꼬리 0 블록을 제거하고, 윈도우를 만들 수 없는 짧은 구간은 버린다.
    구간을 이어붙이지 않는 이유는 상단 docstring 4) 참조.
    """
    df = pd.read_csv(path)
    cmd = df["command"].values
    cur = df["current_diff"].values

    segments, i = [], 0
    while i < len(cmd):
        if cmd[i] != "FORWARD":
            i += 1
            continue
        j = i
        while j < len(cmd) and cmd[j] == "FORWARD":
            j += 1
        seg = _strip_tail_zeros(cur[i:j].astype(float))
        if len(seg) >= W:
            segments.append(seg)
        i = j
    return segments


def load_series(path):
    """
    [구버전 / 비교용] FORWARD 구간을 모두 이어붙인 1차원 배열.
    현재 판정에는 쓰지 않는다. classify_cv.py 가 구/신 방식 비교에 사용한다.
    """
    s = pd.read_csv(path)
    s = s[s["command"] == "FORWARD"]["current_diff"].reset_index(drop=True).values
    return _strip_tail_zeros(s)


def classify(value):
    if value < LOW:
        return "정상"
    elif value < HIGH:
        return "부하"
    return "이물질"


def window_values(path, method="mean"):
    """구간별로 슬라이딩 윈도우를 만들어 대표값 배열을 반환 (구간 경계를 넘지 않음)"""
    out = []
    for seg in load_segments(path):
        win = np.lib.stride_tricks.sliding_window_view(seg, W)
        out.append(np.median(win, axis=1) if method == "median" else win.mean(axis=1))
    return np.concatenate(out) if out else np.array([], dtype=float)


def confusion_matrix(method="mean"):
    """method: 'mean'(최종) 또는 'median'(초기 소량 데이터용)"""
    cm = {a: {b: 0 for b in LABELS} for a in LABELS}
    for true_label, files in FILES.items():
        for fname in files:
            for rep in window_values(os.path.join(DATA, fname), method):
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
    """
    상태별 원시 통계 (샘플 단위).
    데이터 자체의 분포를 설명하는 값이라 구버전과 동일하게 '이어붙인 계열' 기준으로 낸다.
    (판정 성능은 위 confusion_matrix 가 구간 단위로 계산한다)
    """
    print("=== 상태별 통계 (3세트 합산, 꼬리 0 제거) ===")
    print(f"{'상태':<8}{'개수':>7}{'평균':>8}{'표준편차':>10}")
    for t, files in FILES.items():
        s = np.concatenate([load_series(os.path.join(DATA, f)) for f in files])
        print(f"{t:<8}{len(s):>7}{s.mean():>8.2f}{s.std():>10.2f}")
    print()


def segment_summary():
    """구동 구간 구조 요약 — 왜 구간 분리가 필요한지 보여주는 근거 출력"""
    print("=== 구동 구간 구조 (구간 단위 윈도우 근거) ===")
    total_seg = total_bound = 0
    ratios = []
    for files in FILES.values():
        for f in files:
            segs = load_segments(os.path.join(DATA, f))
            total_seg += len(segs)
            total_bound += max(len(segs) - 1, 0)
            for s in segs:
                if len(s) > 5 and s[5:].mean() > 0:
                    ratios.append(s[:3].max() / s[5:].mean())
    print(f"구동 구간 {total_seg}개 / 구간 경계 {total_bound}개")
    print(f"구간을 이어붙이면 경계를 넘나드는 윈도우가 {total_bound*(W-1)}개 발생")
    if ratios:
        print(f"구간 시작 돌입전류: 본체 평균 대비 평균 {np.mean(ratios):.2f}배, "
              f"최대 {np.max(ratios):.2f}배")
    print()


if __name__ == "__main__":
    stats()
    segment_summary()
    report(confusion_matrix("mean"), "판정: 구간 단위 윈도우 평균 (최종, LOW=9.4, HIGH=12.0)")