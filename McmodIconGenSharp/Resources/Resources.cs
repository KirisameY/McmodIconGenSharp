using System.Reflection;

namespace McmodIconGenSharp.Resources;

internal static class Resources
{
    private static Assembly Assembly => Assembly.GetExecutingAssembly();

    public static string? GetShader(string path)
    {
        var fullPath = $"McmodIconGenSharp.Resources.Shaders.{path}.glsl";
        using var stream = Assembly.GetManifestResourceStream(fullPath);
        if (stream is null) return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}