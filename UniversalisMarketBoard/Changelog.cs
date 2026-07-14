using System;
using System.IO;

namespace UniversalisMarketBoard;

internal static class Changelog
{
    private const string ResourceName = "UniversalisMarketBoard.Changelog.md";

    public static string Content { get; } = LoadContent();

    private static string LoadContent()
    {
        using var stream = typeof(Changelog).Assembly.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            return "Changelog history is unavailable.";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
