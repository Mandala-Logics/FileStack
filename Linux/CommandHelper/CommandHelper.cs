using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MandalaLogics.Command
{
    public readonly struct ShellOutput
    {
        //PUBLIC PROPERTIES
        public string StandardOutput { get; }
        public string StandardError { get; }
        public int ExitCode { get; }
        public bool HasOutput {get;}
        public bool Sucess => ExitCode == 0;
        public bool Failed => ExitCode != 0;
        public bool HasError => !string.IsNullOrEmpty(StandardError);

        //CONSTRCUTORS  
        public ShellOutput(int exitCode, string stdOutput, string stdError) 
        {
            StandardOutput = stdOutput;
            StandardError = stdError;
            ExitCode = exitCode;
            HasOutput = true;
        }
        public ShellOutput(int exitCode, string stdOutput) 
        {
            StandardOutput = stdOutput;
            StandardError = string.Empty;
            ExitCode = exitCode;
            HasOutput = true;
        }
        public ShellOutput(int exitCode) 
        {
            StandardOutput = string.Empty;
            StandardError = string.Empty;
            ExitCode = exitCode;
            HasOutput = false;
        }
        public ShellOutput(int exitCode, string stdOutput, Exception e) 
        {
            StandardOutput = stdOutput;
            StandardError = e.Message;
            ExitCode = exitCode;
            HasOutput = true;
        }
    }

    public static partial class CommandHelper
    {
        public static bool IsSingleton(out Process process)
        {
            if (Debugger.IsAttached)
            {
                process = default;
                return true;
            }

            Process cur = Process.GetCurrentProcess();

            foreach (Process pr in Process.GetProcesses())
            {
                if (pr.ProcessName.Equals(cur.ProcessName) && pr.Id != cur.Id)
                {
                    process = pr;
                    return false;
                }
            }

            process = null;
            return true;
        }

        public static void DisplayAssemblyFile(string path)
        {
            using (var sr = DoGetAssemblyStreamReader(Assembly.GetCallingAssembly(), path))
            {
                while (!sr.EndOfStream) { Console.WriteLine(sr.ReadLine()?? string.Empty); }
            }
        }

        public static ShellOutput OutputAssemblyFile(string path)
        {
            var ls = new List<string>();

            using (var sr = DoGetAssemblyStreamReader(Assembly.GetCallingAssembly(), path))
            {
                while (!sr.EndOfStream) { ls.Add(sr.ReadLine()?? string.Empty); }
            }

            return new ShellOutput(0, string.Join('\n', ls));
        }

        public static void DoDisplayAssemblyFile(Assembly ass, string path)
        {
            using (var sr = DoGetAssemblyStreamReader(ass, path))
            {
                while (!sr.EndOfStream) { Console.WriteLine(sr.ReadLine()?? string.Empty); }
            }
        }

        public static ShellOutput DoOutputAssemblyFile(Assembly ass, string path)
        {
            var ls = new List<string>();

            using (var sr = DoGetAssemblyStreamReader(ass, path))
            {
                while (!sr.EndOfStream) { ls.Add(sr.ReadLine()?? string.Empty); }
            }

            return new ShellOutput(0, string.Join('\n', ls));
        }

        public static Regex GetWildcardRegex(string pattern)
        {
            pattern = pattern.Replace("*", ".+");

            return new Regex(pattern);
        }

        public static StreamReader GetAssemblyStreamReader(string path) => new StreamReader(DoGetAssemblyStream(Assembly.GetCallingAssembly(), path));

        public static StreamReader DoGetAssemblyStreamReader(Assembly ass, string path) => new StreamReader(DoGetAssemblyStream(ass, path));

        public static Stream GetAssemblyStream(string path) => DoGetAssemblyStream(Assembly.GetCallingAssembly(), path);

        public static Stream DoGetAssemblyStream(Assembly ass, string path)
        {
            string s = string.Join('.', ass.GetName().Name, path.Replace('/', '.').Replace('\\', '.'));

            return ass.GetManifestResourceStream(s) ??
                throw new FileNotFoundException($"Cannot find the assembly file: {s}");
        } 
    
        public static ShellOutput Bash(string cmd)
        {
            cmd = cmd.Replace("\"", "\\\"");
    
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{cmd}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
    
            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.Start();
                string stdOutput = process.StandardOutput.ReadToEnd();
                string stdError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                int exitCode = process.ExitCode;
                
                return new ShellOutput(exitCode, stdOutput, stdError);
            }
        }

        //EXTENSIONS
        public static bool MustOpen(this FileMode mode)
        {
            return mode switch
            {
                FileMode.Append => true,
                FileMode.Create => false,
                FileMode.CreateNew => false,
                FileMode.Open => true,
                FileMode.OpenOrCreate => false,
                FileMode.Truncate => true,
                _ => throw new ArgumentException("Invalid file mode: " + mode)
            };
        }

        public static bool CanOpen(this FileMode mode)
        {
            return mode switch
            {
                FileMode.Append => true,
                FileMode.Create => true,
                FileMode.CreateNew => false,
                FileMode.Open => true,
                FileMode.OpenOrCreate => true,
                FileMode.Truncate => true,
                _ => throw new ArgumentException("Invalid file mode: " + mode)
            };
        }

        public static bool MustCreate(this FileMode mode)
        {
            return mode switch
            {
                FileMode.Append => false,
                FileMode.Create => false,
                FileMode.CreateNew => true,
                FileMode.Open => false,
                FileMode.OpenOrCreate => false,
                FileMode.Truncate => false,
                _ => throw new ArgumentException("Invalid file mode: " + mode)
            };
        }

        public static bool CanCreate(this FileMode mode)
        {
            return mode switch
            {
                FileMode.Append => false,
                FileMode.Create => true,
                FileMode.CreateNew => true,
                FileMode.Open => false,
                FileMode.OpenOrCreate => true,
                FileMode.Truncate => false,
                _ => throw new ArgumentException("Invalid file mode: " + mode)
            };
        }
    }
}