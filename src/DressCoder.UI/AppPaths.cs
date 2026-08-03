using System.IO;

namespace DressCoder.UI;

/// <summary>Well-known filesystem locations for this portable (no-installer) app.</summary>
public static class AppPaths
{
    /// <summary>Where the app's rolling file logger writes, next to the executable.</summary>
    public static string LogDirectory => Path.Combine(AppContext.BaseDirectory, "logs");
}
