# Sharpcraft.ReferenceVerifier

Extracts pure numeric model facts from the local `mc-26.3-analysis` workspace and writes them outside the repository:

```text
artifacts/reference/vanilla-model-facts.json
```

The output contains only model/part names, `PartPose` numbers, `CubeDeformation` grow values and `texOffs`/`addBox` numbers. It never contains Java source, comments or decompiled code.

Usage:

```text
dotnet run --project tools/Sharpcraft.ReferenceVerifier -- --write
dotnet run --project tools/Sharpcraft.ReferenceVerifier -- --check
```

Optional:

```text
--output <path>          output JSON path, relative to the repository root
--models a,b             model source paths relative to the analysis root
--root <path>            repository root
--analysis-root <path>   analysis workspace root
```

The default model set is intentionally small and grows only after the parser/formats are validated. `--check` skips successfully when the analysis workspace is absent; CI without the local reference workspace must not fail on it.
