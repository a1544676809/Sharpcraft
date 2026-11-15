#!/usr/bin/env python3
# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors

import argparse
import sys
from pathlib import Path
from PIL import Image

def parse_roi(spec: str):
    name, coords = spec.split(':', 1)
    x0, y0, x1, y1 = [int(v) for v in coords.split(',')]
    return name, (x0, y0, x1, y1)

def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--vanilla', required=True, type=Path)
    ap.add_argument('--actual', required=True, type=Path)
    ap.add_argument('--mode', choices=['regression', 'cross-engine'], default='regression')
    ap.add_argument('--bg-rgb', default='0,0,0')
    ap.add_argument('--roi', action='append', default=[], help='name:x0,y0,x1,y1')
    ap.add_argument('--min-iou', type=float, default=None)
    ap.add_argument('--max-mae', type=float, default=None)
    ap.add_argument('--max-bg-leak', type=int, default=None)
    args = ap.parse_args()

    min_iou = args.min_iou if args.min_iou is not None else (0.99 if args.mode == 'regression' else 0.90)
    max_mae = args.max_mae if args.max_mae is not None else (1.0 if args.mode == 'regression' else 28.0)
    max_bg_leak = args.max_bg_leak if args.max_bg_leak is not None else (0 if args.mode == 'regression' else 999999)

    v_img = Image.open(args.vanilla).convert('RGB')
    a_img = Image.open(args.actual).convert('RGB')
    if v_img.size != a_img.size:
        print(f"FAIL size_mismatch vanilla={v_img.size} actual={a_img.size}")
        return 1

    w, h = v_img.size
    vp, apx = v_img.load(), a_img.load()
    bg = tuple(int(c) for c in args.bg_rgb.split(','))
    rois = [parse_roi(r) for r in args.roi] or [('full', (0, 0, w, h))]

    in_any_roi = [[False] * w for _ in range(h)]
    failed = False
    metrics = []

    for name, (x0, y0, x1, y1) in rois:
        inter = union = 0
        mae_sum = 0.0
        n = 0
        for y in range(max(0, y0), min(h, y1)):
            for x in range(max(0, x0), min(w, x1)):
                in_any_roi[y][x] = True
                vc, ac = vp[x, y], apx[x, y]
                v_fg = any(abs(vc[i] - bg[i]) > 6 for i in range(3))
                a_fg = any(abs(ac[i] - bg[i]) > 6 for i in range(3))
                if v_fg and a_fg:
                    inter += 1
                if v_fg or a_fg:
                    union += 1
                mae_sum += sum(abs(vc[i] - ac[i]) for i in range(3)) / 3.0
                n += 1
        iou = (inter / union) if union > 0 else 1.0
        mae = (mae_sum / n) if n > 0 else 0.0
        metrics.append((name, iou, mae))
        if iou < min_iou or mae > max_mae:
            failed = True

    bg_leak = 0
    if args.mode == 'regression':
        for y in range(h):
            for x in range(w):
                if not in_any_roi[y][x] and vp[x, y] != apx[x, y]:
                    bg_leak += 1
        if bg_leak > max_bg_leak:
            failed = True

    summary = ' '.join(f"{n}_iou={iou:.4f} {n}_mae={mae:.2f}" for n, iou, mae in metrics)
    status = 'FAIL' if failed else 'PASS'
    print(f"{status} mode={args.mode} bg_leak={bg_leak} {summary}")
    return 1 if failed else 0

if __name__ == '__main__':
    sys.exit(main())
