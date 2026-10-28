// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Docs.Indexer;

/// <summary>
/// Command-line entry point. <c>--check</c> and <c>--check-coverage</c> are the CI gates;
/// <c>--write</c> and <c>--update-baseline</c> are the developer actions (docs/modules/doc-indexer.md).
/// </summary>
internal static class Program
{
    private const int Success = 0;
    private const int Failure = 1;
    private const int UsageError = 2;

    private static int Main(string[] args)
    {
        var options = Options.Parse(args);
        if (options.Error is { } error)
        {
            Console.Error.WriteLine($"error: {error}");
            PrintUsage();
            return UsageError;
        }

        if (options.Help)
        {
            PrintUsage();
            return Success;
        }

        var root = options.Root ?? FindRepositoryRoot();
        if (root is null)
        {
            Console.Error.WriteLine("error: repository root not found; pass --root <path>");
            return UsageError;
        }

        var failed = false;

        if (options.UpdateBaseline)
        {
            var undocumented = SourceModules.UpdateBaseline(root);
            Console.WriteLine($"coverage baseline: {undocumented.Count} module(s) still undocumented");
        }

        if (options.CheckCoverage)
        {
            var violations = SourceModules.Check(root);
            if (violations.Count == 0)
            {
                Console.WriteLine("source coverage: ok");
            }
            else
            {
                failed = true;
                Console.Error.WriteLine($"source coverage: {violations.Count} violation(s)");
                foreach (var violation in violations)
                {
                    Console.Error.WriteLine("  " + violation);
                }
            }
        }

        if (options.Check)
        {
            var stale = DocIndexer.Check(root);
            if (stale.Count == 0)
            {
                Console.WriteLine("generated indexes: ok");
            }
            else
            {
                failed = true;
                Console.Error.WriteLine($"generated indexes: {stale.Count} stale or missing");
                foreach (var file in stale)
                {
                    Console.Error.WriteLine("  " + file);
                }

                Console.Error.WriteLine("run: dotnet run --project tools/Sharpcraft.Docs.Indexer -- --write");
            }
        }
        else if (options.Write)
        {
            var changed = DocIndexer.Write(root);
            Console.WriteLine(changed.Count == 0
                ? "generated indexes: already current"
                : $"generated indexes: {changed.Count} file(s) updated");
            foreach (var file in changed)
            {
                Console.WriteLine("  " + file);
            }
        }

        return failed ? Failure : Success;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Sharpcraft documentation indexer

              --write              regenerate every directory index (docs/**/README.md)
              --check              fail when a generated index is missing or stale (CI)
              --check-coverage     fail when a source module has no doc and is not baselined (CI)
              --update-baseline    rewrite docs/src/.coverage-baseline.txt
              --root <path>        repository root (default: walk up from this executable)
              --help               show this text
            """);
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sharpcraft.slnx")) &&
                File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record Options(
        bool Write,
        bool Check,
        bool CheckCoverage,
        bool UpdateBaseline,
        bool Help,
        string? Root,
        string? Error)
    {
        public static Options Parse(IReadOnlyList<string> args)
        {
            var write = false;
            var check = false;
            var checkCoverage = false;
            var updateBaseline = false;
            var help = false;
            string? root = null;
            string? error = null;

            for (var index = 0; index < args.Count; index++)
            {
                var argument = args[index];
                switch (argument)
                {
                    case "--write":
                        write = true;
                        break;
                    case "--check":
                        check = true;
                        break;
                    case "--check-coverage":
                        checkCoverage = true;
                        break;
                    case "--update-baseline":
                        updateBaseline = true;
                        break;
                    case "--help" or "-h":
                        help = true;
                        break;
                    case "--root":
                        if (index + 1 >= args.Count)
                        {
                            error = "--root needs a path";
                            break;
                        }

                        root = args[++index];
                        break;
                    default:
                        error = $"unknown argument: {argument}";
                        break;
                }
            }

            if (error is null && write && check)
            {
                error = "--write and --check are mutually exclusive";
            }

            if (error is null && !help && !write && !check && !checkCoverage && !updateBaseline)
            {
                error = "nothing to do";
            }

            return new Options(write, check, checkCoverage, updateBaseline, help, root, error);
        }
    }
}
