// RenderDoc Python analysis: per-action GPU duration for a Sharpcraft capture.
// Run with: qrenderdoc.exe --python this_script.py capture.rdc
// Prints the slowest actions by GPU time, then a rolled up summary by top level marker.

import sys
import os

try:
    import renderdoc as rd
except Exception as error:  # pragma: no cover - environment guard
    print("IMPORT-FAIL %s" % error)
    os._exit(2)


def open_capture(path):
    capture = rd.OpenCaptureFile()
    status = capture.OpenFile(path, "", None)
    if status != rd.ResultCode.Succeeded:
        print("OPEN-FAIL %s" % status)
        os._exit(3)
    if not capture.LocalReplaySupport():
        print("NO-LOCAL-REPLAY")
        os._exit(4)
    result, controller = capture.OpenCapture(rd.ReplayOptions(), None)
    if result != rd.ResultCode.Succeeded:
        print("REPLAY-FAIL %s" % result)
        os._exit(5)
    return capture, controller


def flat_actions(actions, out):
    for action in actions:
        out.append(action)
        flat_actions(action.children, out)


def main():
    path = sys.argv[-1]
    capture, controller = open_capture(path)

    actions = []
    flat_actions(controller.GetRootActions(), actions)

    # GPU duration per event. The counter gives nanoseconds for a single event.
    counters = controller.FetchCounters([rd.GPUCounter.EventGPUDuration])
    duration = {}
    for counter in counters:
        duration[counter.eventId] = counter.value

    draws = [a for a in actions if a.drawcall]
    marked = [a for a in actions if not a.drawcall and a.customName]
    print("SUMMARY actions=%d draws=%d markerGroups=%d counters=%d"
          % (len(actions), len(draws), len(marked), len(counters)))

    total = sum(duration.get(a.eventId, 0) for a in actions if a.drawcall)
    print("SUMMARY totalDrawGpuMs=%.3f" % (total / 1_000_000.0))

    top = sorted(
        ((duration.get(a.eventId, 0), a) for a in draws),
        key=lambda pair: pair[0],
        reverse=True,
    )
    print("--- slowest draws ---")
    for value, action in top[:25]:
        print("DRAW ms=%.4f name=%s" % (value / 1_000_000.0, action.customName or action.eventId))

    # Roll draws up to the nearest enclosing marker so pass totals are readable.
    print("--- by top level group ---")
    groups = {}

    def walk(action, current):
        label = current
        if action.customName:
            label = action.customName
        if action.drawcall:
            groups[label] = groups.get(label, 0) + duration.get(action.eventId, 0)
        for child in action.children:
            walk(child, label)

    for root in controller.GetRootActions():
        walk(root, "(root)")

    for label, value in sorted(groups.items(), key=lambda pair: pair[1], reverse=True)[:20]:
        print("GROUP ms=%.4f name=%s" % (value / 1_000_000.0, label))

    controller.Shutdown()
    capture.Shutdown()
    os._exit(0)


main()
