// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.ClassMap;

internal static class Program
{
    private static int Main(string[] args)
    {
        var root = FindRoot();
        var command = args.Length > 0 ? args[0] : "--check";

        switch (command)
        {
            case "--write":
            {
                var (annotated, injected, unannotated) = ClassMapTool.Write(root, updateBaseline: false);
                Console.WriteLine($"class-map: wrote ({annotated} annotated, {injected} module docs injected, {unannotated} unannotated).");
                return 0;
            }

            case "--update-baseline":
            {
                var (annotated, injected, unannotated) = ClassMapTool.Write(root, updateBaseline: true);
                Console.WriteLine($"class-map: updated baseline ({annotated} annotated, {injected} module docs injected, {unannotated} unannotated).");
                return 0;
            }

            case "--check":
            default:
            {
                var violations = ClassMapTool.Check(root);
                if (violations.Count == 0)
                {
                    Console.WriteLine("class-map: ok");
                    return 0;
                }

                Console.Error.WriteLine("class-map violations:");
                foreach (var v in violations)
                {
                    Console.Error.WriteLine("  " + v);
                }

                return 1;
            }
        }
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Sharpcraft.slnx")) &&
                File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException($"repository root not found above {AppContext.BaseDirectory}");
    }
}

