using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PCManager.IconGen;

/// <summary>
/// Generates the Icon3D geometry dictionaries from the hand-drawn SVGs in tools/icons.
///
/// Every SVG uses one 24x24 design grid. A &lt;g id="Name"&gt; group is one icon; its paths carry
/// data-role="face" (default) or data-role="neutral". The generator merges the paths of each
/// role into one frozen PathGeometry, because the Icon3D control adds the shadow, extrusion,
/// gradient, highlight and rim at render time (five tints x two themes would otherwise be
/// thousands of pre-baked images).
///
/// Usage: dotnet run --project tools/IconGen [-- --check]
/// </summary>
internal static class Program
{
    private const string DesignGrid = "0 0 24 24";
    private static readonly Regex AllowedPath = new("^[MmZzLlHhVvCcSsQqTtAa0-9eE+\\-.,\\s]+$", RegexOptions.Compiled);

    private static int Main(string[] args)
    {
        string root = FindRepositoryRoot();
        bool check = args.Contains("--check", StringComparer.OrdinalIgnoreCase);

        List<IconSource> icons;
        try
        {
            icons = ReadIcons(root);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException)
        {
            Console.Error.WriteLine($"IconGen: {ex.Message}");
            return 2;
        }

        var outputs = BuildOutputs(icons);
        int stale = 0;

        foreach ((string relative, string content) in outputs)
        {
            string path = Path.Combine(root, relative);
            string normalized = content.ReplaceLineEndings("\r\n");

            if (check)
            {
                string existing = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
                if (!string.Equals(existing, normalized, StringComparison.Ordinal))
                {
                    Console.Error.WriteLine($"STALE: {relative}");
                    stale++;
                }

                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine($"wrote {relative}");
        }

        if (check)
        {
            if (stale > 0)
            {
                Console.Error.WriteLine($"IconGen: {stale} generated file(s) are out of date. Run: dotnet run --project tools/IconGen");
                return 1;
            }

            Console.WriteLine($"IconGen: {icons.Count} icons, {icons.Sum(i => i.Roles.Count)} geometries, up to date.");
        }
        else
        {
            Console.WriteLine($"IconGen: {icons.Count} icons in {icons.Select(i => i.Category).Distinct().Count()} categories.");
        }

        return 0;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "app", "src", "PCManager", "PCManager.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Run IconGen from inside the FPM repository (app/src/PCManager/PCManager.csproj not found).");
    }

    private static List<IconSource> ReadIcons(string root)
    {
        string iconDirectory = Path.Combine(root, "tools", "icons");

        if (!Directory.Exists(iconDirectory))
        {
            throw new InvalidDataException($"Missing icon source directory: {iconDirectory}");
        }

        var icons = new List<IconSource>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string file in Directory.GetFiles(iconDirectory, "*.svg").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            string category = ToPascalCase(Path.GetFileNameWithoutExtension(file));
            XDocument document = XDocument.Load(file);
            XElement rootElement = document.Root ?? throw new InvalidDataException($"{file}: no root element");

            if (rootElement.Name.LocalName != "svg")
            {
                throw new InvalidDataException($"{file}: root element must be <svg>");
            }

            if ((string?)rootElement.Attribute("viewBox") is not DesignGrid)
            {
                throw new InvalidDataException($"{file}: viewBox must be \"{DesignGrid}\"");
            }

            foreach (XElement group in rootElement.Elements().Where(e => e.Name.LocalName == "g"))
            {
                string name = (string?)group.Attribute("id") ?? throw new InvalidDataException($"{file}: <g> without id");

                if (!seen.Add(name))
                {
                    throw new InvalidDataException($"{file}: duplicate icon id \"{name}\" (keys must be unique)");
                }

                var roles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (XElement path in group.Elements().Where(e => e.Name.LocalName == "path"))
                {
                    string data = ((string?)path.Attribute("d"))?.Trim() ?? string.Empty;
                    string role = (string?)path.Attribute("data-role") ?? "face";

                    if (role is not ("face" or "neutral"))
                    {
                        throw new InvalidDataException($"{file}: icon \"{name}\" has unknown data-role \"{role}\" (use face or neutral)");
                    }

                    if (data.Length == 0 || !data.StartsWith('M') || !AllowedPath.IsMatch(data))
                    {
                        throw new InvalidDataException($"{file}: icon \"{name}\" has an invalid path: {data}");
                    }

                    if (!roles.TryGetValue(role, out List<string>? list))
                    {
                        roles[role] = list = new List<string>();
                    }

                    list.Add(data);
                }

                if (roles.Count == 0)
                {
                    throw new InvalidDataException($"{file}: icon \"{name}\" has no paths");
                }

                icons.Add(new IconSource(category, name, roles.OrderBy(r => r.Key, StringComparer.Ordinal)
                    .ToDictionary(r => r.Key, r => string.Join(" ", r.Value))));
            }
        }

        if (icons.Count == 0)
        {
            throw new InvalidDataException($"{iconDirectory}: no icons found");
        }

        return icons;
    }

    private static List<(string RelativePath, string Content)> BuildOutputs(List<IconSource> icons)
    {
        var outputs = new List<(string, string)>();
        var categories = icons.GroupBy(i => i.Category).OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var category in categories)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"");
            builder.AppendLine("                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
            builder.AppendLine("                    xmlns:po=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation/options\">");
            builder.AppendLine();
            builder.AppendLine($"    <!-- Generated by tools/IconGen from tools/icons/{category.Key.ToLowerInvariant()}.svg. Do not edit. -->");
            builder.AppendLine();

            foreach (var icon in category.OrderBy(i => i.Name, StringComparer.Ordinal))
            {
                foreach ((string role, string data) in icon.Roles)
                {
                    builder.Append("    <PathGeometry x:Key=\"")
                        .Append(GeometryKey(icon.Name, role))
                        .Append("\" po:Freeze=\"True\" Figures=\"")
                        .Append(data)
                        .AppendLine("\" />");
                }
            }

            builder.AppendLine();
            builder.AppendLine("</ResourceDictionary>");
            outputs.Add(($"app/src/PCManager/Assets/Icons3D/{category.Key}.xaml", builder.ToString()));
        }

        outputs.Add(("app/src/PCManager/Assets/Icons3D.xaml", BuildMergeDictionary(categories.Select(c => c.Key))));
        outputs.Add(("app/src/PCManager/Assets/Icons3D.g.cs", BuildCatalog(icons, categories.Select(c => (c.Key, c.Select(i => i.Name).ToList())))));
        return outputs;
    }

    private static string BuildMergeDictionary(IEnumerable<string> categories)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"");
        builder.AppendLine("                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        builder.AppendLine();
        builder.AppendLine("    <!-- Generated by tools/IconGen. Do not edit. One dictionary per icon category, so no single");
        builder.AppendLine("         file has to hold the whole inventory. -->");
        builder.AppendLine("    <ResourceDictionary.MergedDictionaries>");

        foreach (string category in categories)
        {
            builder.Append("        <ResourceDictionary Source=\"Icons3D/")
                .Append(category)
                .AppendLine(".xaml\" />");
        }

        builder.AppendLine("    </ResourceDictionary.MergedDictionaries>");
        builder.AppendLine("</ResourceDictionary>");
        return builder.ToString();
    }

    private static string BuildCatalog(List<IconSource> icons, IEnumerable<(string Name, List<string> Icons)> categories)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated>");
        builder.AppendLine("// Generated by tools/IconGen from tools/icons/*.svg. Do not edit by hand.");
        builder.AppendLine("// </auto-generated>");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine("namespace PCManager.Assets;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>One icon family in the Icon3D inventory, in plan order.</summary>");
        builder.AppendLine("public sealed record IconCategory(string Name, IReadOnlyList<string> Icons);");
        builder.AppendLine();
        builder.AppendLine("/// <summary>The complete Icon3D inventory, used by the --icon-gallery review window.</summary>");
        builder.AppendLine("public static class IconCatalog");
        builder.AppendLine("{");
        builder.AppendLine("    public const string KeyPrefix = \"Icon3D.\";");
        builder.AppendLine();
        builder.AppendLine($"    public const int IconCount = {icons.Count};");
        builder.AppendLine();
        builder.AppendLine("    public static IReadOnlyList<IconCategory> Categories { get; } = new IconCategory[]");
        builder.AppendLine("    {");

        foreach ((string name, List<string> names) in categories)
        {
            builder.Append("        new(\"")
                .Append(name)
                .Append("\", new[] { ")
                .Append(string.Join(", ", names.Select(n => $"\"{n}\"")))
                .AppendLine(" }),");
        }

        builder.AppendLine("    };");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string GeometryKey(string name, string role) => $"Icon3D.{name}.{char.ToUpperInvariant(role[0])}{role[1..]}";

    private static string ToPascalCase(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private sealed record IconSource(string Category, string Name, Dictionary<string, string> Roles);
}
