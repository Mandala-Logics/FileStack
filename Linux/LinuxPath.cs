using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using MandalaLogics.Command;
using MandalaLogics.Encoding;

namespace MandalaLogics.Path
{

    public class LinuxPath : PathBase, IEncodable
    {
        public static PathStructure PathStructure { get; } = new PathStructure("LinuxPath", typeof(LinuxPath), "/", 0, "/", null, true, 0, PathProximityType.Local);
        public static LinuxPath Root { get; } = new LinuxPath(@"/");
        public static LinuxPath Home { get; }
        public static string UserName => CommandHelper.Bash($"id -nu {UID}").StandardOutput.Trim();
        public static int UID { get; }
        public static int GID { get; }
        public static int[] GIDs { get; }
        public static string[] UserGroups { get; }
        public static string Host { get; }
        public static bool AmIRoot => UID == 0;
        public static LinuxPath TemporaryFolder = new LinuxPath("/tmp", DestType.Dir);
        
        public override string HostName => Host;
        public override bool WatchingPath => Watcher is object;
        
        private FileSystemWatcher Watcher { get; set; } = null;


        public LinuxPath(string path) : base(PathStructure, CleanLinuxPath(path)) { }
        public LinuxPath(string path, DestType mode) : base(PathStructure, CleanLinuxPath(path), mode) { }
        private LinuxPath(LinuxPath path) : base(path) { }
        
        public LinuxPath(DecodingHandle handle) : base(PathStructure, handle) { }
        void IEncodable.DoEncode(EncodingHandle handle) => base.DoEncode(handle);

        static LinuxPath()
        {
            UID = int.Parse(CommandHelper.Bash(@"id -u").StandardOutput.Trim());
            GID = int.Parse(CommandHelper.Bash(@"id -g").StandardOutput.Trim());

            var s = CommandHelper.Bash(@"id -G").StandardOutput.Trim().Split(' ');

            GIDs = new int[s.Length];

            for (int i = 0; i < s.Length; i++) { GIDs[i] = int.Parse(s[i]); }

            UserGroups = CommandHelper.Bash(@"groups").StandardOutput.Trim().Split(' ');

            Host = CommandHelper.Bash(@"hostname").StandardOutput;

            Home = new LinuxPath(CommandHelper.Bash(@"echo ~").StandardOutput.Trim(), DestType.Dir);

            EncodedObject.RegisterTypes(typeof(LinuxPath));
        }
        
        public override AccessLevel CheckAccess()
        {
            if (!CheckExists()) throw new PathAccessException(this, $"Cannot check access; path does not exist: {Path}");
            else if (AmIRoot)
            {
                if (IsRootPath) { return AccessLevel.ReadWrite; }
                else { return AccessLevel.FullAccess; }
            }

            AccessLevel ret;

            if (IsRootPath)
            {
                return GetLinuxDir().First((lfi) => { return lfi.Name.Equals("."); } ).CalculateAccessLevel(UID, GIDs);
            }
            else
            {
                var parent = (LinuxPath)GetContainingDir();
                var parentAcccess = parent.GetLinuxDir().First((lfi) => { return lfi.Name.Equals("."); } ).CalculateAccessLevel(UID, GIDs);

                ret = parent.GetLinuxDir().First((lfi) => { return lfi.Name.Equals(EndPointName); }).CalculateAccessLevel(UID, GIDs);

                return ret | (parentAcccess.HasFlag(AccessLevel.Write) ? AccessLevel.Delete : AccessLevel.None);
            }
        }

        public override bool CheckExists()
        {
            if (File.Exists(Path))
            {
                SetExists(true);
            }
            else if (Directory.Exists(Path))
            {
                SetExists(true);
            }
            else
            {
                SetExists(false);
            }

            return exists ?? false;
        }

        public override DestType CheckType()
        {
            if (File.Exists(Path))
            {
                SetExists(true);
                return DestType.File;
            }
            else if (Directory.Exists(Path))
            {
                SetExists(true);
                return DestType.Dir;
            }

            SetExists(false);
            return DestType.Unknown;
        }

        public override PathBase GetRootPath() => Root;

        public override PathBase Clone() => new LinuxPath(this);

        public override void CreateDirectory()
        {
            if (EndType == DestType.File) throw new PathException($"Cannot create a directory, this is a file path: {Path}");

            if (Directory.Exists(Path))
            {
                SetExists(true);
                SetEndType(DestType.Dir);
                throw new PathException($"Cannot create directory, directory already exists: {Path}");
            }
            else if (File.Exists(Path))
            {
                SetExists(true);
                SetEndType(DestType.File);
                throw new PathException($"Cannot create directory, file already exists: {Path}");
            }

            try { Directory.CreateDirectory(Path); }
            catch (UnauthorizedAccessException e) { throw new PathAccessException(this, $"Cannot create dir, permission denied: {Path}", e); }
            catch (DirectoryNotFoundException e) { throw new PathAccessException(this, $"Cannot create dir, invalid path: {Path}", e); }

            SetEndType(DestType.Dir);
            SetExists(true);
        }

        public override void Delete(bool recursive = false)
        {
            if (EndType == DestType.Dir)
            {
                try { Directory.Delete(Path, recursive); }
                catch (DirectoryNotFoundException e) { throw new PathAccessException(this, $"Cannot delete, path does not exist: {Path}", e); }
                catch (UnauthorizedAccessException e) { throw new PathAccessException(this, $"Cannot delete, permission denied: {Path}", e); }
            }
            else if (EndType == DestType.Unknown)
            {
                var check = CheckType();

                if (check != DestType.Unknown)
                {
                    SetEndType(check);
                    Delete(recursive);
                }
                else
                {
                    throw new PathAccessException(this, $"Cannot delete, path does not exist: {Path}");
                }
            }
            else
            {
                try { File.Delete(Path); }
                catch (DirectoryNotFoundException e) { throw new PathAccessException(this, $"Cannot delete, path does not exist: {Path}", e); }
                catch (UnauthorizedAccessException e) { throw new PathAccessException(this, $"Cannot delete, permission denied: {Path}", e); }
            }
        }

        public override IEnumerable<PathBase> EnumerateDirs()
        {
            IEnumerable<string> dirs;

            try { dirs = Directory.EnumerateDirectories(Path); }
            catch (DirectoryNotFoundException e) { throw new PathAccessException(this, $"Cannot get dirs, path does not exist: {Path}", e); }
            catch (UnauthorizedAccessException e) { throw new PathAccessException(this, $"Cannot get dirs, permission denied: {Path}", e); }
            catch (Exception e)
            {
                if (EndType == DestType.File) { throw new PathException($"Cannot get dirs, this is a file path: {Path}", e); }
                else { throw new PathException($"Unable to get dirs: {Path}", e); }
            }

            if (dirs.Count() > 0)
            {
                var paths = new List<PathBase>(dirs.Count());

                foreach (string s in dirs)
                {
                    paths.Add(new LinuxPath(s, DestType.Dir));
                }

                return paths;
            }
            else
            {
                return new PathBase[0];
            }
        }

        public override IEnumerable<PathBase> EnumerateFiles()
        {
            IEnumerable<string> files;

            try { files = Directory.EnumerateFiles(Path); }
            catch (DirectoryNotFoundException e) { throw new PathAccessException(this, $"Cannot get dirs, path does not exist: {Path}", e); }
            catch (UnauthorizedAccessException e) { throw new PathAccessException(this, $"Cannot get dirs, permission denied: {Path}", e); }
            catch (Exception e)
            {
                if (EndType == DestType.File) { throw new PathException($"Cannot get dirs, this is a file path: {Path}", e); }
                else { throw new PathException($"Unable to get dirs: {Path}", e); }
            }

            if (files.Count() > 0)
            {
                var paths = new List<PathBase>(files.Count());

                foreach (string s in files)
                {
                    paths.Add(new LinuxPath(s, DestType.File));
                }

                return paths;
            }
            else
            {
                return new PathBase[0];
            }
        }

        public override Stream OpenStream(FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            FileStream fs;

            try { fs = new FileStream(Path, fileMode, fileAccess, fileShare); }
            catch (FileNotFoundException e)
            {
                SetExists(false);

                throw new PathAccessException(this, $"File does not exist and FileMode was *not* set to CreateNew or Create: {Path}", e);
            }
            catch (DirectoryNotFoundException e)
            {
                SetExists(false);

                throw new PathAccessException(this, $"Parent directory does not exist, cannot create/open file: {Path}", e);
            }
            catch (IOException e)
            {
                SetExists(true);

                var res = (IOHResultValues)(e.HResult & 0xFFFF);

                var msg = res switch
                {
                    IOHResultValues.FileSharingViolation => "This file is already open by another process.",
                    IOHResultValues.FileAlreadyExists => "The file already exists and CreateNew was used.",
                    _ => "File cannot be opened, unknown reason."
                };

                throw new PathAccessException(this, $"{Path}: {msg}", e);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new PathAccessException(this, $"Cannot open/create file, permission denied: {Path}", e);
            }
            catch (SecurityException e)
            {
                SetExists(true);

                throw new PathAccessException(this, $"Cannot open/create file, permission denied: {Path}", e);
            }

            SetExists(true);
            SetEndType(DestType.File);

            return fs;
        }

        public override void StartWatchingPath()
        {
            try
            {
                Watcher = new FileSystemWatcher(Path) { EnableRaisingEvents = true };
            }
            catch (ArgumentException e)
            {
                if (IsFile)
                {
                    throw new PathAccessException(this, $"Cannot watch a file path, try watching the parent dir: {Path}");
                }
                else
                {
                    throw new PathAccessException(this, $"Cannot watch a dir path: {Path}", e);
                }
            }

            Watcher.Changed += (sender, e) =>
            {
                var args = new PathChangedEventArgs(PathChangeType.Changed, new LinuxPath(e.FullPath));
                OnPathChanged(args);
            };

            Watcher.Deleted += (sender, e) =>
            {
                var args = new PathChangedEventArgs(PathChangeType.Deleted, new LinuxPath(e.FullPath));
                OnPathChanged(args);
            };

            Watcher.Created += (sender, e) =>
            {
                var args = new PathChangedEventArgs(PathChangeType.Created, new LinuxPath(e.FullPath));
                OnPathChanged(args);
            };

            Watcher.Renamed += (sender, e) =>
            {
                var args = new PathChangedEventArgs(PathChangeType.Renamed, new LinuxPath(e.FullPath));
                OnPathChanged(args);
            };

            SetExists(true);
            SetEndType(DestType.Dir);
        }

        public override void StopWatchingPath()
        {
            Watcher?.Dispose();
            Watcher = null;
        }
        public override PathBase GetWorkingDirectory() => new LinuxPath(Directory.GetCurrentDirectory(), DestType.Dir);

        public IEnumerable<LinuxFileInfo> GetLinuxDir()
        {
            LinuxPath path;

            if (EndType == DestType.File || EndType == DestType.SymLink)
            {
                path = (LinuxPath)GetContainingDir();
            }
            else { path = this; }

            string ret = CommandHelper.Bash($"ls -lna '{path.Path}'").StandardOutput.Replace("'", string.Empty);

            var m = LinuxFileInfo.lsRegex.Matches(ret);

            if (m.Count > 0)
            {
                var info = new LinuxFileInfo[m.Count];

                for (int c = 0; c < m.Count; c++)
                {
                    info[c] = new LinuxFileInfo(m[c]);
                }

                return info;
            }
            else
            {
                if (EndType == DestType.File || EndType == DestType.SymLink)
                {
                    ret = CommandHelper.Bash($"ls -lna '{Path}'").StandardOutput.Replace("'", string.Empty);

                    m = LinuxFileInfo.lsRegex.Matches(ret);

                    if (m.Count > 0)
                    {
                        var info = new LinuxFileInfo[m.Count];

                        for (int c = 0; c < m.Count; c++)
                        {
                            info[c] = new LinuxFileInfo(m[c]);
                        }

                        return info;
                    }
                    else
                    {
                        return new LinuxFileInfo[0];
                    }
                }
                else
                {
                    return new LinuxFileInfo[0];
                }
            }

        }
        public override long FileLength()
        {
            var res = CommandHelper.Bash($"stat -c %s '{Path}'");

            if (res.ExitCode != 0)
            {
                SetExists(false);
                throw new PathAccessException(this, $"Cannot get length, file/dir does not exist or cannot be accessed: {Path}");
            }
            else
            {
                SetExists(true);
            }

            return long.Parse(res.StandardOutput);
        }

        public static LinuxPath GetWorkingDir()
        {
            var ret = CommandHelper.Bash("pwd");

            return new LinuxPath(ret.StandardOutput, DestType.Dir);
        }
        public static LinuxPath GetTemporaryFile()
        {
            var ret = CommandHelper.Bash("mktemp");

            return new LinuxPath(ret.StandardOutput, DestType.File);

        }
        public static void gUnZip(PathBase inputFile, PathBase outputFile)
        {
            if (!inputFile.IsFile)
            {
                throw new PathTypeException($"Not a file: {inputFile}");
            }
            else if (outputFile.Exists && !outputFile.Access.HasFlag(AccessLevel.Write))
            {
                throw new PathTypeException($"output file is not writeable: {outputFile}");
            }

            var cmd = $"gzip -dc \"{inputFile}\" > \"{outputFile}\"";
            var ret = CommandHelper.Bash(cmd);

            if (ret.ExitCode != 0)
            {
                throw new Exception($"Uncompress command ({cmd}) failed:\n{ret.StandardError}");
            }
        }
        public static string CleanLinuxPath(string path)
        {
            var ret = path.Replace('\\', '/').Replace(@"//", "/").Trim();

            if (ret.StartsWith(@"~/")) { ret = Home.Append(ret[2..]).ToString(); }
            else if (ret == "~") { ret = Home.ToString(); }

            if (ret.StartsWith(".."))
            {
                var x = new LinuxPath(Directory.GetCurrentDirectory());

                if (x.Count > 1)
                {
                    ret = x.GetContainingDir() + ret[2..];
                }
                else
                {
                    throw new PathParsingException("The '..' operator cannot be used at the start of a path if the working path is root.");
                }
            }
            else if (ret.StartsWith('.'))
            {
                ret = Directory.GetCurrentDirectory() + ret[1..];
            }

            return ret;
        }
    }
}