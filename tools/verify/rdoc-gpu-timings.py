# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
"""Reads GPU pass durations out of a RenderDoc capture.

The in-application path cannot report GPU time: the wgpu binding this project uses exposes no timestamp query, which
is why the GPU numbers have only ever come from RenderDoc by hand. This script automates that step.

Run it through RenderDoc's own interpreter, which is the only one the renderdoc module is built for:

    qrenderdoc.exe --py tools/verify/rdoc-gpu-timings.py

The capture path and the output path are read from %MC_RDOC_CAPTURE% and %MC_RDOC_OUTPUT%, because a script started
this way has no console: qrenderdoc is a windowed binary, so anything printed to stdout goes nowhere. The script ends
with sys.exit() so the process closes instead of leaving the UI running.
"""

import os
import sys

CAPTURE = os.environ.get("MC_RDOC_CAPTURE", "")
OUTPUT = os.environ.get("MC_RDOC_OUTPUT", "")

_lines = []


def log(message):
    _lines.append(message)


def flush():
    if not OUTPUT:
        return
    with open(OUTPUT, "w", encoding="utf-8") as handle:
        handle.write("\n".join(_lines) + "\n")


def to_float(value):
    """CounterValue is a union; which member holds the number depends on the counter's component type."""
    for member in ("d", "f", "u64", "u32", "s64", "s32"):
        part = getattr(value, member, None)
        if part is not None:
            return float(part)
    return float(value)


def classify(action):
    """RenderDoc flags an event by what it does; custom names are empty because this renderer pushes no markers."""
    flags = action.flags
    for name in ("Drawcall", "Dispatch", "Clear", "Copy", "Resolve", "Present", "BeginPass", "EndPass"):
        if getattr(flags, name, False):
            return name
    return "other"


def flatten(actions, depth=0):
    for action in actions:
        yield action, depth
        yield from flatten(action.children, depth + 1)


def main():
    if not CAPTURE:
        log("MC_RDOC_CAPTURE is not set")
        return 0

    import renderdoc as rd

    log("renderdoc %s" % rd.GetVersionString())

    initialised = False
    try:
        rd.InitialiseReplay(rd.GlobalEnvironment(), [])
        initialised = True
    except Exception as error:  # the UI does this normally; --py runs before it exists
        log("InitialiseReplay not needed or failed: %s" % error)

    capture = rd.OpenCaptureFile()
    opened = capture.OpenFile(CAPTURE, "", None)
    if opened != rd.ResultCode.Succeeded:
        log("could not open %s: %s" % (CAPTURE, opened))
        return 1

    if capture.LocalReplaySupport() != rd.ReplaySupport.Supported:
        log("capture cannot be replayed locally: %s" % capture.LocalReplaySupport())
        return 1

    # OpenCapture takes a progress callback that is optional in the C++ API but not exposed consistently by the
    # bindings, so try the shapes this version might have rather than pinning one.
    status, controller = rd.ResultCode.Succeeded, None
    for attempt in ("two", "one", "none"):
        try:
            if attempt == "two":
                status, controller = capture.OpenCapture(rd.ReplayOptions(), None)
            elif attempt == "one":
                status, controller = capture.OpenCapture(rd.ReplayOptions())
            else:
                status, controller = capture.OpenCapture()
            log("OpenCapture accepted the %s shape" % attempt)
            break
        except TypeError as error:
            log("OpenCapture %s shape rejected: %s" % (attempt, error))

    if status != rd.ResultCode.Succeeded or controller is None:
        log("replay failed: %s" % status)
        return 1

    try:
        counters = {c: controller.DescribeCounter(c) for c in controller.EnumerateCounters()}
        if rd.GPUCounter.EventGPUDuration not in counters:
            log("this capture exposes no GPU duration counter")
            return 1

        # The counter reports seconds; everything below is in microseconds because that is the scale the frame budget
        # is discussed in.
        results = controller.FetchCounters([rd.GPUCounter.EventGPUDuration])
        duration = {}
        for result in results:
            duration[result.eventId] = to_float(result.value) * 1e6

        actions = list(flatten(controller.GetRootActions()))
        by_event = {action.eventId: action for action, _ in actions}
        depth_of = {action.eventId: depth for action, depth in actions}

        # Our renderer does not push debug markers, so the capture is a long flat list of draws and copies rather
        # than named passes. Aggregate by what each event is instead, which is enough to compare two builds and to
        # see whether time sits in draws or in the copies around them.
        categories = {}
        detail = {}
        for action, _ in actions:
            value = duration.get(action.eventId, 0.0)
            if value <= 0.0:
                continue
            category = classify(action)
            categories[category] = categories.get(category, 0.0) + value
            detail.setdefault(category, []).append((value, action))

        total = sum(categories.values())

        # Two captures never hold the same amount of geometry, because chunks stream in at different moments. The
        # only comparable number across builds is cost per triangle, so report that alongside the total.
        total_indices = sum(action.numIndices * max(action.numInstances, 1) for action, _ in actions)
        triangles = total_indices / 3.0

        log("")
        log("=== GPU duration by kind of event (microseconds) ===")
        for category in sorted(categories, key=lambda c: -categories[c]):
            log("%-24s %9.2f us" % (category, categories[category]))

        log("")
        log("frame total: %.2f us" % total)
        log("indexed draws: %d indices = %.0f triangles" % (total_indices, triangles))
        if triangles > 0:
            log("cost per triangle: %.2f ns" % (total * 1000.0 / triangles))

        log("")
        log("=== biggest single events ===")
        flat = [(value, action) for category in detail for value, action in detail[category]]
        flat.sort(key=lambda row: -row[0])
        for value, action in flat[:15]:
            log("%-24s event %-6s %9.2f us  idx=%s inst=%s" % (
                classify(action), action.eventId, value, action.numIndices, action.numInstances))

        log("")
        log("=== events slower than 20 us ===")
        rows = [(value, by_event[event].customName or str(event)) for event, value in duration.items()
                if event in by_event and value >= 20.0]
        rows.sort(reverse=True)
        for value, name in rows[:40]:
            log("%-52s %9.2f us" % (name, value))
    finally:
        controller.Shutdown()
        capture.Shutdown()

    if initialised:
        try:
            rd.ShutdownReplay()
        except Exception:
            pass

    return 0


try:
    code = main()
except Exception as error:  # noqa: BLE001 - the whole point is to leave a record instead of a silent exit
    log("unhandled: %r" % error)
    code = 1

flush()
sys.exit(code)
