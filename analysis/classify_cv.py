"""
판정 규칙의 일반화 성능 검증 (교차검증)

목적:
  classify.py 가 보고하는 정확도는 경계값(LOW/HIGH)과 판정방식(평균/중앙값)을
  '전체 데이터'에서 전수 탐색해 고른 뒤 '같은 데이터'로 평가한 값이다.
  즉 학습과 평가에 같은 데이터를 써서 낙관적으로 편향(과적합)돼 있을 수 있다.
  이 스크립트는 세트 단위 교차검증으로 '처음 보는 데이터에서의 성능'을 추정한다.

왜 세트(파일) 단위로 나누는가:
  W=20 슬라이딩 윈도우는 이웃 윈도우와 19개 샘플을 공유한다.
  윈도우를 랜덤으로 섞어 train/test를 나누면 거의 동일한 윈도우가 양쪽에 들어가
  데이터 누수(leakage)가 발생하고, 테스트 점수가 실제보다 높게 나온다.
  따라서 측정 세트(파일) 단위로 분할해야 한다.

두 가지 수치를 보고한다:
  [A] 재탐색 CV  - 매 fold 마다 학습 세트에서 파라미터를 다시 탐색해 테스트 세트로 평가.
                   '경계값을 정하는 절차 전체'의 일반화 성능.
  [B] 고정 파라미터 CV - 현재 배포된 규칙(평균, LOW=9.4, HIGH=12.0)을 그대로
                   각 테스트 세트에 적용. '지금 앱에 들어있는 규칙'의 일반화 성능.

[C] 구동 구간 경계 분석 - 윈도우가 서로 다른 주행을 섞을 때의 영향을 비교한다.
    (구버전 '구간 이어붙임' vs 현재 '구간 분리')

주의: classify.py 에서 window_values / load_segments / W / FILES 를 그대로 import 한다.
      윈도우 생성·전처리 로직이 동일해야 숫자를 비교할 수 있기 때문이다.
      [A]/[B] 는 현재 방식(구간 단위 윈도우) 기준으로 계산된다.
"""
import os
import sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from classify import (load_series, load_segments, window_values,
                      W, FILES, DATA, LABELS,
                      LOW as SHIPPED_LOW, HIGH as SHIPPED_HIGH)

# 탐색 범위 (current_diff 가 정수이므로 W=20 평균의 해상도는 0.05)
LOW_GRID = np.round(np.arange(7.0, 12.001, 0.05), 2)
HIGH_GRID = np.round(np.arange(10.0, 16.001, 0.05), 2)
METHODS = ["mean", "median"]


# ----------------------------------------------------------------------
# 윈도우 대표값 추출
# ----------------------------------------------------------------------
def window_reps(path, method):
    """
    한 파일의 윈도우 대표값 배열 (classify.py 의 window_values 를 그대로 사용).
    구동 구간별로 윈도우를 만들며, 구간 경계를 넘지 않는다.
    """
    return window_values(path, method)


def window_reps_legacy(path, method="mean"):
    """[비교용] 구버전 방식 — FORWARD 구간을 모두 이어붙인 뒤 윈도우 생성."""
    s = load_series(path)
    if len(s) < W:
        return np.array([], dtype=float)
    win = np.lib.stride_tricks.sliding_window_view(s.astype(float), W)
    return np.median(win, axis=1) if method == "median" else win.mean(axis=1)


def build_reps():
    """{method: {label: [fold0 배열, fold1 배열, fold2 배열]}} 구조로 미리 계산."""
    reps = {}
    for method in METHODS:
        reps[method] = {}
        for label, files in FILES.items():
            reps[method][label] = [
                window_reps(os.path.join(DATA, f), method) for f in files
            ]
    return reps


# ----------------------------------------------------------------------
# 정확도 계산 (정렬 + 이분탐색으로 전수 탐색을 빠르게)
# ----------------------------------------------------------------------
def correct_counts(sorted_reps, low, high):
    """분류 규칙: v < LOW -> 정상 / v < HIGH -> 부하 / 그 외 -> 이물질"""
    n = np.searchsorted(sorted_reps["정상"], low, side="left")
    l = (np.searchsorted(sorted_reps["부하"], high, side="left")
         - np.searchsorted(sorted_reps["부하"], low, side="left"))
    d = len(sorted_reps["이물질"]) - np.searchsorted(sorted_reps["이물질"], high, side="left")
    return n, l, d


def accuracy(sorted_reps, low, high):
    n, l, d = correct_counts(sorted_reps, low, high)
    total = sum(len(v) for v in sorted_reps.values())
    return (n + l + d) / total if total else 0.0


def grid_search(reps_by_label):
    """학습 데이터에서 (method, LOW, HIGH) 를 전수 탐색. 동점이면 중앙값 조합 선택."""
    best = {"acc": -1.0}
    for method in METHODS:
        sorted_reps = {lab: np.sort(reps_by_label[method][lab]) for lab in LABELS}
        ties = []
        best_acc = -1.0
        for low in LOW_GRID:
            for high in HIGH_GRID:
                if high <= low:
                    continue
                acc = accuracy(sorted_reps, low, high)
                if acc > best_acc + 1e-12:
                    best_acc, ties = acc, [(low, high)]
                elif abs(acc - best_acc) <= 1e-12:
                    ties.append((low, high))
        # 동점 후보들의 중앙값에 가장 가까운 조합 선택 (결정적 tie-break)
        arr = np.array(ties)
        center = np.median(arr, axis=0)
        low, high = arr[np.argmin(((arr - center) ** 2).sum(axis=1))]
        if best_acc > best["acc"] + 1e-12:
            best = {"acc": best_acc, "method": method, "low": float(low),
                    "high": float(high), "n_ties": len(ties)}
    return best


# ----------------------------------------------------------------------
# 혼동행렬
# ----------------------------------------------------------------------
def predict(v, low, high):
    if v < low:
        return "정상"
    if v < high:
        return "부하"
    return "이물질"


def add_to_cm(cm, reps_by_label, method, low, high):
    for lab in LABELS:
        for v in reps_by_label[method][lab]:
            cm[lab][predict(v, low, high)] += 1


def empty_cm():
    return {a: {b: 0 for b in LABELS} for a in LABELS}


def print_cm(cm, title):
    print(f"--- {title} ---")
    header = "실제\\예측"
    print(f"{header:<8}" + "".join(f"{l:>8}" for l in LABELS) + f"{'합계':>8}")
    total = correct = 0
    for a in LABELS:
        row = cm[a]
        s = sum(row.values())
        total += s
        correct += row[a]
        print(f"{a:<8}" + "".join(f"{row[b]:>8}" for b in LABELS) + f"{s:>8}")
    print(f"\n총 윈도우 {total}  |  정확도 {correct/total*100:.2f}%  ({correct}/{total})")
    for a in LABELS:
        s = sum(cm[a].values())
        if s:
            print(f"  {a} 재현율 {cm[a][a]/s*100:6.2f}%  ({cm[a][a]}/{s})")
    print()
    return correct / total if total else 0.0


# ----------------------------------------------------------------------
# 메인
# ----------------------------------------------------------------------
def main():
    n_folds = len(next(iter(FILES.values())))
    reps = build_reps()

    print("=" * 62)
    print(" 세트 단위 교차검증 (Leave-One-Set-Out, {}-fold)".format(n_folds))
    print("=" * 62)
    print(f" 윈도우 W={W} | 분할 단위: 측정 세트(파일)")
    print(" 윈도우가 겹치므로 랜덤 분할은 데이터 누수 발생 -> 세트 단위로 분할\n")

    # ---------- 참고: 전체 데이터로 맞춘 in-sample 성능 ----------
    all_reps = {m: {lab: np.concatenate(reps[m][lab]) for lab in LABELS} for m in METHODS}
    full = grid_search(all_reps)
    print("[참고] 전체 데이터로 파라미터를 고르고 같은 데이터로 평가 (= 현재 방식)")
    print(f"       최적: {full['method']}, LOW={full['low']}, HIGH={full['high']}"
          f"  ->  정확도 {full['acc']*100:.2f}%")
    print(f"       (동점 조합 {full['n_ties']}개)\n")

    # ---------- [A] 재탐색 CV ----------
    print("=" * 62)
    print(" [A] 재탐색 CV — fold 마다 학습 세트에서 파라미터를 다시 탐색")
    print("=" * 62)
    cm_a = empty_cm()
    test_accs, chosen = [], []
    for k in range(n_folds):
        train = {m: {lab: np.concatenate([reps[m][lab][i] for i in range(n_folds) if i != k])
                     for lab in LABELS} for m in METHODS}
        test = {m: {lab: reps[m][lab][k] for lab in LABELS} for m in METHODS}

        best = grid_search(train)
        sorted_test = {lab: np.sort(test[best["method"]][lab]) for lab in LABELS}
        test_acc = accuracy(sorted_test, best["low"], best["high"])
        test_accs.append(test_acc)
        chosen.append(best)
        add_to_cm(cm_a, test, best["method"], best["low"], best["high"])

        print(f" Fold {k+1}: 학습=세트{[i+1 for i in range(n_folds) if i != k]}, 평가=세트{k+1}")
        print(f"   선택 파라미터  {best['method']}, LOW={best['low']}, HIGH={best['high']}")
        print(f"   학습 정확도    {best['acc']*100:6.2f}%")
        print(f"   평가 정확도    {test_acc*100:6.2f}%   <- 처음 보는 데이터")
        print()

    mean_a, std_a = float(np.mean(test_accs)), float(np.std(test_accs))
    print(f" 평균 평가 정확도: {mean_a*100:.2f}%  (표준편차 {std_a*100:.2f}%p)")
    print(f" fold별: {'  '.join(f'{a*100:.2f}%' for a in test_accs)}\n")
    print_cm(cm_a, "[A] 3개 fold 평가 결과 합산 혼동행렬")

    # ---------- [B] 고정 파라미터 CV ----------
    print("=" * 62)
    print(f" [B] 고정 파라미터 CV — 배포 규칙(mean, LOW={SHIPPED_LOW}, HIGH={SHIPPED_HIGH}) 그대로 평가")
    print("=" * 62)
    cm_b = empty_cm()
    fixed_accs = []
    for k in range(n_folds):
        test = {m: {lab: reps[m][lab][k] for lab in LABELS} for m in METHODS}
        sorted_test = {lab: np.sort(test["mean"][lab]) for lab in LABELS}
        acc = accuracy(sorted_test, SHIPPED_LOW, SHIPPED_HIGH)
        fixed_accs.append(acc)
        add_to_cm(cm_b, test, "mean", SHIPPED_LOW, SHIPPED_HIGH)
        print(f" 세트 {k+1}: 정확도 {acc*100:6.2f}%")
    mean_b, std_b = float(np.mean(fixed_accs)), float(np.std(fixed_accs))
    print(f"\n 평균 {mean_b*100:.2f}%  (표준편차 {std_b*100:.2f}%p)\n")
    print_cm(cm_b, "[B] 배포 파라미터 전체 적용 혼동행렬")

    # ---------- 결론 ----------
    gap = (full["acc"] - mean_a) * 100
    n_total = sum(len(v) for v in all_reps["mean"].values())
    n_eff = n_total / W
    ci = 1.96 * np.sqrt(mean_a * (1 - mean_a) / n_eff)

    print("=" * 62)
    print(" 결론")
    print("=" * 62)
    print(f" in-sample (현재 보고 중)   : {full['acc']*100:.2f}%")
    print(f" 교차검증 평균 [A]          : {mean_a*100:.2f}%")
    print(f" 과적합 폭 (차이)           : {gap:+.2f}%p")
    print(f" 배포 파라미터 CV [B]       : {mean_b*100:.2f}%")
    print()
    print(f" 총 윈도우 {n_total}개 / 겹침 보정 유효 표본 약 {n_eff:.0f}개")
    print(f" [A] 95% 신뢰구간 약 {mean_a*100:.1f}% +- {ci*100:.1f}%p")
    print()
    if abs(gap) < 1.0:
        print(" 판단: 과적합 폭이 1%p 미만 -> 파라미터가 2개뿐이고 클래스 분리가 뚜렷해")
        print("       일반화 성능이 유지된다고 볼 수 있음.")
    elif gap < 3.0:
        print(" 판단: 과적합 폭이 1~3%p -> 존재하지만 제한적. 보고 시 CV 수치를 함께 제시할 것.")
    else:
        print(" 판단: 과적합 폭이 3%p 이상 -> in-sample 수치는 과대평가.")
        print("       README/이력서에는 CV 수치를 대표값으로 쓸 것.")
    print()
    methods_used = {c["method"] for c in chosen}
    lows = [c["low"] for c in chosen]
    print(f" fold별 선택 방식: {methods_used}  |  선택된 LOW: {lows}")
    if len(methods_used) == 1 and "mean" in methods_used:
        print(" -> 모든 fold에서 '평균'이 선택됨. 판정 방식 선택이 데이터에 안정적.")
    print(f" LOW 변동폭 {max(lows)-min(lows):.2f}  "
          f"(작을수록 경계값이 데이터에 민감하지 않다는 뜻)")
    print("=" * 62)
    print()
    segment_analysis()


# ----------------------------------------------------------------------
# [C] 구동 구간 경계 분석 (부가 진단)
#
#   구버전 load_series() 는 한 파일의 여러 FORWARD 구간을 하나로 이어붙인다.
#   그래서 슬라이딩 윈도우가 '구간 경계'를 넘나들며, 앞 구동의 끝과
#   다음 구동의 시작(모터 기동 돌입전류 스파이크)을 한 윈도우에 섞는다.
#   이 스파이크가 정상 오탐의 원인인지 데이터로 확인한다.
# ----------------------------------------------------------------------
def reps_segmentwise(path, skip_start=0):
    """구간 단위 윈도우. skip_start>0 이면 각 구간의 앞부분을 그만큼 잘라낸다."""
    out = []
    for seg in load_segments(path):
        if skip_start:
            seg = seg[skip_start:]
        if len(seg) < W:
            continue
        out.append(np.lib.stride_tricks.sliding_window_view(seg, W).mean(axis=1))
    return np.concatenate(out) if out else np.array([], dtype=float)


def eval_scheme(get_reps, name):
    cm = empty_cm()
    for lab, files in FILES.items():
        for f in files:
            for v in get_reps(os.path.join(DATA, f)):
                cm[lab][predict(v, SHIPPED_LOW, SHIPPED_HIGH)] += 1
    total = sum(sum(r.values()) for r in cm.values())
    correct = sum(cm[a][a] for a in LABELS)
    n = sum(cm["정상"].values())
    fp = (cm["정상"]["부하"] + cm["정상"]["이물질"]) / n * 100 if n else 0
    print(f" {name:<34} 윈도우 {total:>5}  정확도 {correct/total*100:6.2f}%  정상오탐 {fp:5.2f}%")
    return correct / total, fp


def segment_analysis():
    print("=" * 62)
    print(" [C] 구동 구간 경계 분석 (부가 진단)")
    print("=" * 62)
    # 구간 구조 요약
    tot_seg = tot_bound = 0
    spikes = []
    for lab, files in FILES.items():
        for f in files:
            segs = load_segments(os.path.join(DATA, f))
            tot_seg += len(segs)
            tot_bound += max(len(segs) - 1, 0)
            for s in segs:
                if len(s) > 5 and s[5:].mean() > 0:
                    spikes.append(s[:3].max() / s[5:].mean())
    print(f" 전체 구동 구간 {tot_seg}개, 구간 경계 {tot_bound}개")
    print(f" 경계를 넘나드는 윈도우 = {tot_bound}x{W-1} = {tot_bound*(W-1)}개")
    if spikes:
        print(f" 구간 시작 돌입전류: 본체 평균 대비 평균 {np.mean(spikes):.2f}배, "
              f"최대 {np.max(spikes):.2f}배")
    print()
    print(f" 배포 파라미터(mean, {SHIPPED_LOW}, {SHIPPED_HIGH}) 고정, 윈도우 생성 방식만 변경:\n")
    a = eval_scheme(lambda p: window_reps_legacy(p, "mean"), "① 구버전 (구간 이어붙임)")
    b = eval_scheme(lambda p: reps_segmentwise(p, 0), "② 현재 방식 (구간 분리)  ★채택")
    c = eval_scheme(lambda p: reps_segmentwise(p, 5), "③ 구간 분리 + 시작 5샘플 제외")
    print()
    print(f" 정상 오탐률 변화: {a[1]:.2f}% -> {b[1]:.2f}% -> {c[1]:.2f}%")
    if b[1] < a[1] - 0.3:
        print(" -> 기동 돌입전류가 정상 오탐의 실제 원인임이 데이터로 확인됨.")
        print("    ②는 '한 윈도우가 서로 다른 주행을 섞으면 안 된다'는 원칙적 수정이라 채택.")
        print("    ③의 '5'는 임의값이고 추가 이득이 작아 채택하지 않음 (재튜닝은 과적합 위험).")
    else:
        print(" -> 윈도우 생성 방식 변경의 효과가 크지 않음.")
    print("=" * 62)


if __name__ == "__main__":
    main()