# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
"""Dumps pipeline details for the most expensive events in a capture.

The renderer pushes no debug markers, so a capture is a flat list of draws with nothing to say which pass each one
belongs to. This reads the pipeline object behind the slowest events instead: the shader resource names, the render
target format and size, and the fixed function state, which together are usually enough to tell terrain from the
fullscreen passes.
"""

import os
import sys

import renderdoc as rd

CAPTURE = os.environ.get("MC_RDOC_CAPTURE", "")
OUTPUT = os.environ.get("MC_RDOC_OUTPUT", "")
TOP = int(os.environ.get("MC_RDOC_TOP", "12"))

lines = []


def log(message):
    lines.append(str(message))


def to_float(value):
    for member in ("d", "f", "u64", "u32", "i64", "i32"):
        if hasattr(value, member):
            return float(getattr(value, member))
    return float(value)


def flatten(actions):
    for action in actions:
        yield action
        for child in flatten(action.children):
            yield child


def resource_name(controller, resource_id):
    if resource_id == rd.ResourceId.Null():
        return "-"
    try:
        description = controller.GetResourceDescription(resource_id)
        name = getattr(description, "name", "") if description is not None else ""
    except Exception:
        name = ""
    return name or str(resource_id)


def main():
    if not CAPTURE:
        print("MC_RDOC_CAPTURE is not set")
        return 1

    rd.InitialiseReplay(rd.GlobalEnvironment(), [])
    capture_file = rd.OpenCaptureFile()
    opened = capture_file.OpenFile(CAPTURE, "", None)
    if opened != rd.ResultCode.Succeeded:
        log("could not open %s: %s" % (CAPTURE, opened))
        return 1

    # OpenCapture takes a progress callback that is optional in the C++ API but not exposed consistently by the
    # bindings, so try the shapes this version might have rather than pinning one.
    status, controller = rd.ResultCode.Succeeded, None
    for attempt in ("two", "one", "none"):
        try:
            if attempt == "two":
                status, controller = capture_file.OpenCapture(rd.ReplayOptions(), None)
            elif attempt == "one":
                status, controller = capture_file.OpenCapture(rd.ReplayOptions())
            else:
                status, controller = capture_file.OpenCapture()
            break
        except TypeError:
            continue

    if status != rd.ResultCode.Succeeded or controller is None:
        log("replay failed: %s" % status)
        return 1

    try:
        results = controller.FetchCounters([rd.GPUCounter.EventGPUDuration])
        duration = {r.eventId: to_float(r.value) * 1e6 for r in results}

        actions = list(flatten(controller.GetRootActions()))
        draws = [(duration.get(a.eventId, 0.0), a) for a in actions if duration.get(a.eventId, 0.0) > 0.0]
        draws.sort(key=lambda row: -row[0])

        log("%d timed events, %d of them draws" % (len(duration), len(draws)))

        # Group by the shader pair behind each draw. Resource names are empty in this capture, so the pipeline's
        # shaders are the only stable identity a draw carries: it separates terrain from the fullscreen passes and
        # from the UI without the renderer having to push markers.
        groups = {}
        for microseconds, action in draws:
            controller.SetFrameEvent(action.eventId, True)
            state = controller.GetPipelineState()
            pair = []
            for stage in (rd.ShaderStage.Vertex, rd.ShaderStage.Fragment):
                reflection = state.GetShaderReflection(stage)
                pair.append(str(reflection.resourceId) if reflection is not None else "-")
            key = " | ".join(pair)
            entry = groups.setdefault(key, [0.0, 0, 0, 0, 0])
            entry[0] += microseconds
            entry[1] += 1
            entry[2] = max(entry[2], action.numIndices)
            # Counts are deterministic where the gpu duration counter is not, so they are worth carrying alongside it.
            # For indexed draws the number of vertices fetched is the index count: one vertex per index.
            entry[3] += action.numIndices
            entry[4] += action.numInstances

        total = sum(value[0] for value in groups.values())
        log("")
        log("=== gpu time by shader pair (total %.1f us) ===" % total)
        log("  %-9s %-6s %-5s %-12s %-9s %s" % ("time", "share", "draws", "indices", "instances", "shaders"))
        for key, entry in sorted(groups.items(), key=lambda item: -item[1][0])[:16]:
            microseconds, count, biggest, indices, instances = entry
            log("  %8.2f us %5.1f%% %5d %12d %9d  %s" % (
                microseconds, 100.0 * microseconds / total, count, indices, instances, key))

        all_indices = sum(value[3] for value in groups.values())
        all_draws = sum(value[1] for value in groups.values())
        log("")
        log("total draws=%d indices=%d triangles=%d" % (all_draws, all_indices, all_indices // 3))

        # Name the passes: the entry points are all called main, but the original source is kept in the capture when
        # the module was compiled with debug info, and its first lines distinguish the pipelines.
        log("")
        log("=== shader identity ===")
        seen = {}
        for microseconds, action in draws:
            controller.SetFrameEvent(action.eventId, True)
            state = controller.GetPipelineState()
            reflection = state.GetShaderReflection(rd.ShaderStage.Vertex)
            if reflection is None:
                continue
            key = str(reflection.resourceId)
            if key in seen:
                continue
            label = reflection.entryPoint or "?"
            try:
                shader = controller.GetShader(reflection.resourceId)
                text = getattr(shader, "source", "") or ""
                for line in text.splitlines():
                    stripped = line.strip()
                    if stripped and not stripped.startswith("//") and not stripped.startswith("enable"):
                        label = "%s | %s" % (label, stripped[:70])
                        break
            except Exception:
                pass
            seen[key] = label
            if len(seen) >= 24:
                break

        for key, label in seen.items():
            log("  %-14s %s" % (key, label))
        for microseconds, action in draws[:TOP]:
            controller.SetFrameEvent(action.eventId, True)
            state = controller.GetPipelineState()
            stages = []
            for stage in (rd.ShaderStage.Vertex, rd.ShaderStage.Fragment):
                reflection = state.GetShaderReflection(stage)
                if reflection is None:
                    continue
                stages.append(resource_name(controller, reflection.resourceId))
            targets = []
            for bound in state.GetOutputTargets():
                resource = getattr(bound, "resource", None)
                if resource is None or resource == rd.ResourceId.Null():
                    continue
                try:
                    description = controller.GetResourceDescription(resource)
                    targets.append("%s %dx%d" % (description.format.Name(), description.width, description.height))
                except Exception:
                    targets.append(str(resource))
            log("")
            log("event %-6s %8.2f us  idx=%-8s inst=%s" % (
                action.eventId, microseconds, action.numIndices, action.numInstances))
            log("  shaders: %s" % " | ".join(stages))
            log("  targets: %s" % (" | ".join(targets) or "-"))
    finally:
        controller.Shutdown()

    text = "\n".join(lines)
    if OUTPUT:
        with open(OUTPUT, "w", encoding="utf-8") as handle:
            handle.write(text + "\n")
    else:
        print(text)
    return 0


# Written from a finally style guard so a failing replay still leaves a file behind: qrenderdoc runs this before the
# window exists, so a traceback on the console is invisible and an empty output looks like the script never ran.
try:
    _status = main()
except Exception:
    import traceback

    lines.append("EXCEPTION:")
    lines.append(traceback.format_exc())
    _status = 1
    if OUTPUT:
        with open(OUTPUT, "w", encoding="utf-8") as _handle:
            _handle.write("\n".join(lines) + "\n")

sys.exit(_status)
