// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.ReferenceVerifier;

using System.Text.Json;
using System.Text.RegularExpressions;

internal sealed record ModelFactDocument(
    int SchemaVersion,
    string Baseline,
    IReadOnlyList<ModelFact> Models);

internal sealed record ModelFact(
    string Class,
    string Source,
    int TextureWidth,
    int TextureHeight,
    IReadOnlyList<PartFact> Parts);

internal sealed record PartFact(
    string Name,
    string? Parent,
    PoseFact Pose,
    IReadOnlyList<CubeFact> Cubes);

internal sealed record PoseFact(
    double X,
    double Y,
    double Z,
    double RotationX,
    double RotationY,
    double RotationZ,
    double ScaleX,
    double ScaleY,
    double ScaleZ);

internal sealed record CubeFact(
    int TextureX,
    int TextureY,
    double MinX,
    double MinY,
    double MinZ,
    double Width,
    double Height,
    double Depth,
    double GrowX,
    double GrowY,
    double GrowZ,
    bool Mirror);

internal static class ReferenceVerifier
{
    public const int SchemaVersion = 1;
    public const string Baseline = "26.3";

    private static readonly string[] DefaultModels =
    [
        "src/net/minecraft/client/model/object/equipment/ElytraModel.java",
        "src/net/minecraft/client/model/monster/warden/WardenModel.java",
        "src/net/minecraft/client/model/monster/breeze/BreezeModel.java",
        "src/net/minecraft/client/model/animal/rabbit/AdultRabbitModel.java",
        "src/net/minecraft/client/model/animal/fox/AdultFoxModel.java"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static ModelFactDocument Build(string analysisRoot, IReadOnlyList<string> modelPaths)
    {
        var models = new List<ModelFact>();
        foreach (var relative in modelPaths)
        {
            var path = Path.Combine(analysisRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"model source not found: {relative}", path);
            }

            var fact = JavaModelParser.Parse(path, relative);
            if (fact is not null)
            {
                models.Add(fact);
            }
        }

        return new ModelFactDocument(SchemaVersion, Baseline, models.OrderBy(m => m.Source, StringComparer.Ordinal).ToArray());
    }

    public static string Serialize(ModelFactDocument document) => JsonSerializer.Serialize(document, JsonOptions);

    public static string Write(string analysisRoot, string outputPath, IReadOnlyList<string> modelPaths)
    {
        var document = Build(analysisRoot, modelPaths);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = Serialize(document);
        File.WriteAllText(outputPath, json + Environment.NewLine, new System.Text.UTF8Encoding(false));
        return json;
    }

    public static bool Check(string analysisRoot, string outputPath, IReadOnlyList<string> modelPaths)
    {
        if (!File.Exists(outputPath))
        {
            Console.WriteLine($"reference-verifier: skipped (generated facts not found: {outputPath})");
            return true;
        }

        var expected = Serialize(Build(analysisRoot, modelPaths)) + Environment.NewLine;
        var actual = File.ReadAllText(outputPath);
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            Console.WriteLine("reference-verifier: ok");
            return true;
        }

        Console.Error.WriteLine("reference-verifier: generated facts are stale; run --write");
        return false;
    }

    public static IReadOnlyList<string> ResolveModels(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return DefaultModels;
        }

        return argument.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

internal static class JavaModelParser
{
    private static readonly Regex ClassPattern = new(@"public\s+(?:final\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex TextureSizePattern = new(@"LayerDefinition\.create\([^,]+,\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.Compiled);
    private static readonly Regex BuilderVariablePattern = new(@"CubeListBuilder\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([^;]+);", RegexOptions.Compiled);
    private static readonly Regex DeformationVariablePattern = new(@"CubeDeformation\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([^;]+);", RegexOptions.Compiled);

    public static ModelFact? Parse(string path, string relativePath)
    {
        var raw = File.ReadAllText(path);
        var text = StripComments(raw);
        text = text.Replace("mesh.getRoot()", "root", StringComparison.Ordinal)
            .Replace("meshdefinition.getRoot()", "root", StringComparison.Ordinal)
            .Replace("meshDefinition.getRoot()", "root", StringComparison.Ordinal);
        var classMatch = ClassPattern.Match(text);
        if (!classMatch.Success)
        {
            return null;
        }

        var builders = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in BuilderVariablePattern.Matches(text))
        {
            builders[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        }

        var deformations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in DeformationVariablePattern.Matches(text))
        {
            deformations[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        }

        var parts = new List<PartFact>();
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        var cursor = 0;
        while (true)
        {
            var call = FindCall(text, "addOrReplaceChild", cursor);
            if (call is null)
            {
                break;
            }

            var callSpan = call.Value;
            var arguments = SplitArguments(callSpan.Inner);
            if (arguments.Count < 3 || !TryReadString(arguments[0], out var name))
            {
                cursor = callSpan.End + 1;
                continue;
            }

            var receiver = FindReceiver(text, callSpan.Start);
            var parent = receiver is not null && variables.TryGetValue(receiver, out var parentName)
                ? parentName
                : null;

            var cubeExpression = arguments[1].Trim();
            if (builders.TryGetValue(cubeExpression, out var resolvedBuilder))
            {
                cubeExpression = resolvedBuilder;
            }

            var cubes = ParseCubeList(cubeExpression, deformations);
            var pose = ParsePose(arguments[2]);
            parts.Add(new PartFact(name, parent, pose, cubes));

            var assignment = FindAssignedVariable(text, callSpan.Start);
            if (assignment is not null)
            {
                variables[assignment] = name;
            }

            cursor = callSpan.End + 1;
        }

        var texture = TextureSizePattern.Match(text);
        var textureWidth = texture.Success && int.TryParse(texture.Groups[1].Value, out var tw) ? tw : 0;
        var textureHeight = texture.Success && int.TryParse(texture.Groups[2].Value, out var th) ? th : 0;
        var source = "ref://mc-26.3-analysis/" + relativePath.Replace('\\', '/');
        return new ModelFact(classMatch.Groups[1].Value, source, textureWidth, textureHeight, parts);
    }

    private static List<CubeFact> ParseCubeList(string expression, IReadOnlyDictionary<string, string> deformations)
    {
        var cubes = new List<CubeFact>();
        var texX = 0;
        var texY = 0;
        var mirror = false;
        var cursor = 0;
        while (true)
        {
            var tex = FindCall(expression, "texOffs", cursor);
            var box = FindCall(expression, "addBox", cursor);
            var mirrorCall = FindCall(expression, "mirror", cursor);
            var next = FindNextCall(tex, box, mirrorCall);
            if (next is null)
            {
                break;
            }

            if (next.Value.Kind == "texOffs")
            {
                var args = SplitArguments(next.Value.Call.Inner);
                if (args.Count >= 2 && TryReadInt(args[0], out var x) && TryReadInt(args[1], out var y))
                {
                    texX = x;
                    texY = y;
                }
            }
            else if (next.Value.Kind == "mirror")
            {
                mirror = next.Value.Call.Inner.Trim() is not "false";
            }
            else
            {
                var args = SplitArguments(next.Value.Call.Inner);
                if (args.Count >= 6
                    && TryReadDouble(args[0], out var minX)
                    && TryReadDouble(args[1], out var minY)
                    && TryReadDouble(args[2], out var minZ)
                    && TryReadDouble(args[3], out var width)
                    && TryReadDouble(args[4], out var height)
                    && TryReadDouble(args[5], out var depth))
                {
                    var grow = args.Count >= 7
                        ? ParseDeformation(args[6], deformations)
                        : (0d, 0d, 0d);
                    cubes.Add(new CubeFact(texX, texY, minX, minY, minZ, width, height, depth, grow.Item1, grow.Item2, grow.Item3, mirror));
                }
            }

            cursor = next.Value.Call.End + 1;
        }

        return cubes;
    }

    private static (double X, double Y, double Z) ParseDeformation(string expression, IReadOnlyDictionary<string, string> deformations)
    {
        var text = expression.Trim();
        if (deformations.TryGetValue(text, out var resolved))
        {
            text = resolved;
        }

        if (text.Contains("CubeDeformation.NONE", StringComparison.Ordinal))
        {
            return (0d, 0d, 0d);
        }

        var call = FindNewCubeDeformation(text);
        if (call is null)
        {
            return (0d, 0d, 0d);
        }

        var args = SplitArguments(call.Value.Inner);
        var values = args.Select(ParseDouble).ToArray();
        return values.Length switch
        {
            1 => (values[0], values[0], values[0]),
            >= 3 => (values[0], values[1], values[2]),
            _ => (0d, 0d, 0d)
        };
    }

    private static PoseFact ParsePose(string expression)
    {
        var text = expression.Trim();
        if (text.Contains("PartPose.ZERO", StringComparison.Ordinal))
        {
            return new PoseFact(0, 0, 0, 0, 0, 0, 1, 1, 1);
        }

        var offset = FindNamedCall(text, "PartPose", "offset");
        if (offset is not null)
        {
            var args = SplitArguments(offset.Value.Inner);
            var values = args.Select(ParseDouble).ToArray();
            if (values.Length >= 3)
            {
                return new PoseFact(values[0], values[1], values[2], 0, 0, 0, 1, 1, 1);
            }
        }

        var rotation = FindNamedCall(text, "PartPose", "offsetAndRotation");
        if (rotation is not null)
        {
            var args = SplitArguments(rotation.Value.Inner);
            var values = args.Select(ParseDouble).ToArray();
            if (values.Length >= 6)
            {
                return new PoseFact(values[0], values[1], values[2], values[3], values[4], values[5], 1, 1, 1);
            }
        }

        throw new InvalidDataException($"unsupported PartPose expression: {expression}");
    }

    private static (string Kind, CallSpan Call)? FindNextCall(CallSpan? tex, CallSpan? box, CallSpan? mirror)
    {
        (string Kind, CallSpan Call)? best = null;
        if (tex is { } t && (best is null || t.Start < best.Value.Call.Start))
        {
            best = ("texOffs", t);
        }

        if (box is { } b && (best is null || b.Start < best.Value.Call.Start))
        {
            best = ("addBox", b);
        }

        if (mirror is { } m && (best is null || m.Start < best.Value.Call.Start))
        {
            best = ("mirror", m);
        }

        return best;
    }

    private static CallSpan? FindCall(string text, string name, int start)
    {
        var index = text.IndexOf("." + name + "(", start, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var open = index + name.Length + 1;
        var close = FindMatching(text, open, '(', ')');
        return close < 0 ? null : new CallSpan(index, open, close, text[(open + 1)..close]);
    }

    private static CallSpan? FindNewCubeDeformation(string text)
    {
        var marker = "new CubeDeformation(";
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var open = index + marker.Length - 1;
        var close = FindMatching(text, open, '(', ')');
        return close < 0 ? null : new CallSpan(index, open, close, text[(open + 1)..close]);
    }

    private static string? FindAssignedVariable(string text, int callStart)
    {
        var start = callStart;
        while (start > 0 && text[start - 1] is not ';' and not '{' and not '}') start--;
        var statement = text[start..callStart];
        var match = Regex.Match(statement, @"(?:PartDefinition|var)\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*[A-Za-z_][A-Za-z0-9_.]*\s*$");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static CallSpan? FindNamedCall(string text, string type, string name)
    {
        var token = type + "." + name + "(";
        var index = text.IndexOf(token, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var open = index + token.Length - 1;
        var close = FindMatching(text, open, '(', ')');
        return close < 0 ? null : new CallSpan(index, open, close, text[(open + 1)..close]);
    }

    private static string? FindReceiver(string text, int callStart)
    {
        var prefix = text[..callStart];
        var match = Regex.Match(prefix, @"([A-Za-z_][A-Za-z0-9_]*)\s*$");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int FindMatching(string text, int open, char openChar, char closeChar)
    {
        var depth = 0;
        var inString = false;
        var inChar = false;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') inString = false;
                continue;
            }

            if (inChar)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') inChar = false;
                continue;
            }

            if (c == '"') { inString = true; continue; }
            if (c == '\'') { inChar = true; continue; }
            if (c == openChar) depth++;
            else if (c == closeChar && --depth == 0) return i;
        }

        return -1;
    }

    private static List<string> SplitArguments(string inner)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        var inString = false;
        var inChar = false;
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') inString = false;
                continue;
            }

            if (inChar)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') inChar = false;
                continue;
            }

            if (c == '"') { inString = true; continue; }
            if (c == '\'') { inChar = true; continue; }
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(inner[start..i].Trim());
                start = i + 1;
            }
        }

        result.Add(inner[start..].Trim());
        return result;
    }

    private static bool TryReadString(string expression, out string value)
    {
        var text = expression.Trim();
        value = string.Empty;
        if (text.Length < 2 || text[0] != '"' || text[^1] != '"')
        {
            return false;
        }

        value = Regex.Unescape(text[1..^1]);
        return true;
    }

    private static bool TryReadInt(string expression, out int value) => int.TryParse(expression.Trim(), out value);

    private static double ParseDouble(string expression) => ExpressionEvaluator.Evaluate(expression);

    private static bool TryReadDouble(string expression, out double value)
    {
        try
        {
            value = ParseDouble(expression);
            return true;
        }
        catch (FormatException)
        {
            value = 0;
            return false;
        }
    }

    private static string StripComments(string text)
    {
        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i += 2;
                while (i < text.Length && text[i] != '\n') i++;
                result.Append('\n');
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i++;
            }
            else
            {
                result.Append(text[i]);
            }
        }

        return result.ToString();
    }

    private readonly record struct CallSpan(int Start, int Open, int End, string Inner);
}

internal static class ExpressionEvaluator
{
    public static double Evaluate(string expression)
    {
        expression = Regex.Replace(expression, @"\((?:float|double)\)", string.Empty, RegexOptions.IgnoreCase);
        var parser = new Parser(expression);
        var value = parser.ParseExpression();
        parser.SkipWhitespace();
        if (!parser.AtEnd)
        {
            throw new FormatException($"unexpected token in expression: {expression}");
        }

        return value;
    }

    private sealed class Parser(string text)
    {
        private int _index;

        public bool AtEnd => _index >= text.Length;

        public double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Match('+')) value += ParseTerm();
                else if (Match('-')) value -= ParseTerm();
                else return value;
            }
        }

        private double ParseTerm()
        {
            var value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (Match('*')) value *= ParseFactor();
                else if (Match('/')) value /= ParseFactor();
                else return value;
            }
        }

        private double ParseFactor()
        {
            SkipWhitespace();
            if (Match('+')) return ParseFactor();
            if (Match('-')) return -ParseFactor();
            if (Match('('))
            {
                var value = ParseExpression();
                Expect(')');
                return value;
            }

            if (StartsWith("Math.toRadians"))
            {
                _index += "Math.toRadians".Length;
                Expect('(');
                var value = ParseExpression();
                Expect(')');
                return value * Math.PI / 180d;
            }

            if (StartsWith("Math.PI"))
            {
                _index += "Math.PI".Length;
                return Math.PI;
            }

            if (StartsWith("Math.E"))
            {
                _index += "Math.E".Length;
                return Math.E;
            }

            var start = _index;
            while (_index < text.Length && (char.IsDigit(text[_index]) || text[_index] is '.' or 'e' or 'E' or '+' or '-' or 'f' or 'F' or 'd' or 'D'))
            {
                if ((text[_index] is '+' or '-') && _index > start && text[_index - 1] is not 'e' and not 'E')
                {
                    break;
                }

                _index++;
            }

            var token = text[start.._index].TrimEnd('f', 'F', 'd', 'D');
            if (token.Length == 0 || !double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                throw new FormatException($"invalid numeric token in expression: {text}");
            }

            return number;
        }

        public void SkipWhitespace()
        {
            while (_index < text.Length && char.IsWhiteSpace(text[_index])) _index++;
        }

        private bool Match(char c)
        {
            if (_index < text.Length && text[_index] == c)
            {
                _index++;
                return true;
            }

            return false;
        }

        private void Expect(char c)
        {
            SkipWhitespace();
            if (!Match(c)) throw new FormatException($"expected '{c}' in expression: {text}");
        }

        private bool StartsWith(string value) => string.CompareOrdinal(text, _index, value, 0, value.Length) == 0;
    }
}
