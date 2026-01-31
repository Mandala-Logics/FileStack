# MandalaLogics.FileStack

This project is built entirely from the ground up, with no reliance on off-the-shelf archiving or backup frameworks. Every layer — from file traversal and data layout to serialization, and I/O — is purpose-designed for performance and predictability. The result is an engine that handles thousands of tiny files and large binaries alike with extremely low overhead, achieving archive times measured in seconds rather than minutes. The focus throughout has been on removing unnecessary abstractions, minimizing allocations, and keeping hot paths brutally efficient — which is why it ends up being really, really fast in practice.

# Example Project

```csharp
using System.IO;
using MandalaLogics.Logging;
using MandalaLogics.Path;
using MandalaLogics.Stacking;

internal static class Program
{
    public static void Main(string[] args)
    {
        var dir = LinuxPath.Home.Append("repos", DestType.Dir);
        
        var stackFile = LinuxPath.Home.Append("test.stack", DestType.File);

        var stack = new FileStack(stackFile.OpenStream(FileMode.Open, FileAccess.ReadWrite, FileShare.None));
        
        stack.CreateLevelFromFolder(dir, new Logger(LogLevel.Verbose));
        
        stack.Dispose();
    }
}
```
