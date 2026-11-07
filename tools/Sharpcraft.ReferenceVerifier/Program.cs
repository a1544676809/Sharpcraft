// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.ReferenceVerifier;

internal static class Program
{
    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "--check";
        var output = Option(args, "--output") ?? "artifacts/reference/vanilla-model-facts.json";
        var models = ReferenceVerifier.ResolveModels(Option(args, "--models"));
        var root = Option(args, "--root") ?? FindRepositoryRoot();
        var analysisRoot = Option(args, "--analysis-root")
            ?? Environment.GetEnvironmentVariable("MC_ANALYSIS_ROOT")
            ?? FindAnalysisRoot(root);

        switch (command)
        {
            case "--write":
                if (!Directory.Exists(analysisRoot))
                {
                    Console.Error.WriteLine($"reference-verifier: analysis root not found: {analysisRoot}");
                    return 2;
                }

                var document = ReferenceVerifier.Build(analysisRoot, models);
                var outputPath = Path.IsPathRooted(output) ? output : Path.Combine(root, output);
                ReferenceVerifier.Write(analysisRoot, outputPath, models);
                Console.WriteLine($"reference-verifier: wrote {document.Models.Count} model(s) to {outputPath}");
                return 0;

            case "--check":
                if (!Directory.Exists(analysisRoot))
                {
                    Console.WriteLine($"reference-verifier: skipped (analysis root not found: {analysisRoot})");
                    return 0;
                }

                var checkPath = Path.IsPathRooted(output) ? output : Path.Combine(root, output);
                return ReferenceVerifier.Check(analysisRoot, checkPath, models) ? 0 : 1;

            default:
                Console.Error.WriteLine("usage: Sharpcraft.ReferenceVerifier --write|--check [--output path] [--models a,b] [--root path] [--analysis-root path]");
                return 2;
        }
    }

    private static string? Option(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sharpcraft.slnx"))
                && File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"repository root not found above {AppContext.BaseDirectory}");
    }

    private static string FindAnalysisRoot(string root)
    {
        var directory = new DirectoryInfo(root);
        while (directory is not null)
        {
            var sibling = Path.Combine(directory.Parent?.FullName ?? directory.FullName, "mc-26.3-analysis");
            if (Directory.Exists(sibling))
            {
                return sibling;
            }

            directory = directory.Parent;
        }

        return Path.Combine(Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar))!, "mc-26.3-analysis");
    }
}
