# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
"""Summarises a speedscope profile per thread: exclusive and inclusive CPU per frame.

The one report dotnet-trace offers mixes every thread together, so the eight mostly idle chunk workers drown the
render thread and the top of the list comes back as semaphore and socket waits. This splits the profile by thread so
the render thread can be looked at on its own.

Handles the evented profile shape, which is what dotnet-trace emits: open/close events carrying timestamps rather than
pre-sampled stacks.
"""

import json
import sys
from collections import defaultdict

path = sys.argv[1]
with open(path, encoding="utf-8") as handle:
    data = json.load(handle)

frames = [frame.get("name", "?") for frame in data["shared"]["frames"]]

print(f"profiles: {len(data['profiles'])}")

summaries = []
for profile in data["profiles"]:
    events = profile.get("events") or []
    if not events:
        continue

    exclusive = defaultdict(float)
    inclusive = defaultdict(float)
    stack = []
    total = 0.0

    for event in events:
        kind = event.get("type")
        if kind == "O":
            stack.append((event.get("frame"), event.get("at", 0.0)))
        elif kind == "C" and stack:
            index, opened = stack.pop()
            span = event.get("at", 0.0) - opened
            if span < 0:
                span = 0.0
            total += span
            name = frames[index]
            exclusive[name] += span
            for openIndex, _ in stack:
                inclusive[frames[openIndex]] += span

    if total <= 0:
        continue
    summaries.append((total, profile.get("name", "?"), exclusive))

summaries.sort(key=lambda row: -row[0])

for total, name, exclusive in summaries[:3]:
    print()
    print(f"=== {name}  busy={total:.0f} ms ===")
    for function, value in sorted(exclusive.items(), key=lambda item: -item[1])[:16]:
        print(f"  {100.0 * value / total:6.2f}%  {value:9.2f} ms  {function[:84]}")
