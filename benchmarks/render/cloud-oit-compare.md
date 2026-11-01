# Cloud OIT fixed-camera comparison

- Scene: Fabric server, fixed player position/time/render distance; `MC_CLOUD_MODE=fancy`, `MC_CLOUD_RANGE=128`.
- A: `MC_CLOUD_OIT=1` (OIT path). B: `MC_CLOUD_OIT=0` (plain depth-alpha).
- Capture the same frame from each build/user screenshot; record: cloud edge coverage, terrain/cloud occlusion, water/cloud overlap, night cloud color.
- Future stage 4 adds `MC_CLOUD_DEPTH=1` (independent cloud depth + cloud OIT composite) and compares A/B/C at the same camera.
- Store images outside the repository; record findings in STATE/traceability.
