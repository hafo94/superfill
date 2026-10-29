using SuperFill.Core.Debug;
using SuperFill.Core.Geometry;
using SuperFill.Core.Serialization;

namespace SuperFill.Cli;

public static class CliApp
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.WriteLine("Usage: sfcli render --boundary <file.json|dir> --out <path-or-dir> [--preset <file>]");
            return args.Length == 0 ? 1 : 0;
        }
        return args[0] switch
        {
            "render" => Render(ParseOptions(args.Skip(1))),
            _ => Fail($"Unknown command '{args[0]}'."),
        };
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var d = new Dictionary<string, string>();
        var list = args.ToList();
        for (int i = 0; i < list.Count; i++)
            if (list[i].StartsWith("--") && i + 1 < list.Count) d[list[i][2..]] = list[++i];
        return d;
    }

    private static int Render(Dictionary<string, string> o)
    {
        if (!o.TryGetValue("boundary", out var boundary) || !o.TryGetValue("out", out var outPath))
            return Fail("render needs --boundary and --out.");

        // A directory renders every boundary into <out>/<name>.png|svg|stats.json.
        var files = Directory.Exists(boundary)
            ? Directory.GetFiles(boundary, "*.json").OrderBy(f => f).ToList()
            : new List<string> { boundary };
        bool batch = Directory.Exists(boundary);
        foreach (var file in files)
        {
            string target = batch ? Path.Combine(outPath, Path.GetFileNameWithoutExtension(file)) : outPath;
            RenderOne(file, target);
        }
        return 0;
    }

    private static void RenderOne(string file, string target)
    {
        var region = BoundaryJson.Parse(File.ReadAllText(file));
        var tol = Tolerances.Metres;
        var scene = new DebugScene();
        scene.AddRegion(region, tol, DebugColor.Blue, DebugColor.LightBlue, DebugColor.Red);
        scene.AddLabel(region.Faces[0].Outer.Curves[0].Start, $"{Path.GetFileNameWithoutExtension(file)}  area={region.Area:0.###} m²", DebugColor.Black);

        string dir = Path.GetDirectoryName(Path.GetFullPath(target))!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(target + ".svg", SvgWriter.Render(scene));
        PngRenderer.Render(scene, target + ".png");
        File.WriteAllText(target + ".stats.json",
            System.Text.Json.JsonSerializer.Serialize(new { boundary = Path.GetFileName(file), area = region.Area, faces = region.Faces.Count, holes = region.Faces.Sum(f => f.Holes.Count) }));
        Console.WriteLine($"{Path.GetFileName(file)} -> {target}.png (area {region.Area:0.###})");
    }

    private static int Fail(string msg)
    {
        Console.Error.WriteLine(msg);
        return 1;
    }
}
