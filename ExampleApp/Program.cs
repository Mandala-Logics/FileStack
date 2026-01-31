using System.IO;
using MandalaLogics.Logging;
using MandalaLogics.Path;
using MandalaLogics.Stacking;

internal static class Program
{
    public static void Main(string[] args)
    {
        var dir = LinuxPath.Home.Append("repos/mandala_logics", DestType.Dir);
        
        var stackFile = LinuxPath.Home.Append("test.stack", DestType.File);

        var stack = new FileStack(stackFile.OpenStream(FileMode.Open, FileAccess.ReadWrite, FileShare.None));
        
        stack.CreateLevelFromFolder(dir, new Logger(LogLevel.Verbose));
        
        stack.Dispose();
    }
}