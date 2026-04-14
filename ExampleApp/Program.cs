using System;
using System.IO;
using System.Reflection;
using System.Threading;
using MandalaLogics.Encoding;
using MandalaLogics.Path;
using MandalaLogics.Path.Hashing;
using MandalaLogics.Stacking;

internal static class Program
{
    static Program()
    {
        EncodingRegister.RegisterAll(Assembly.GetAssembly(typeof(FileFingerprint))!);
    }
    
    public static void Main(string[] args)
    {
        var dir = LinuxPath.Home.Append("repos/v7", DestType.Dir);
        
        var stackFile = LinuxPath.Home.Append("test.stack", DestType.File);

        var stack = new FileStack(stackFile.OpenStream(FileMode.Create, FileAccess.ReadWrite, FileShare.None));
        
        var thread = stack.CreateLevelFromFolder(dir, CancellationToken.None, null);

        thread.State.Progress.ProgressUpdated += (sender, e) => Console.WriteLine(thread.State.Progress.Text);

        thread.Join();
        
        stack.Dispose();
    }
}