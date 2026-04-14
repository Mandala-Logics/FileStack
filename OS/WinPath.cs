using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using MandalaLogics.Encoding;

namespace MandalaLogics.Path
{
    [Encodable("win_path")]
    public class WinPath : PathBase, IEncodable
    {
        public static PathStructure PathStructure { get; } =
            new PathStructure(
                "WinPath",
                typeof(WinPath),
                "\\",
                firstElementLen: 2,
                startToken: null,
                firstSeperator: "\\",
                allowJustStartToken: false,
                rootLength: 1,
                proximity: PathProximityType.Local);

        public static string Host { get; } = Environment.MachineName;
        public static WinPath? SystemDrive { get; }
        public static WinPath TempDirectory { get; }

        public override string HostName => Host;
        public override bool WatchingPath => Watcher is object;

        private FileSystemWatcher? Watcher { get; set; }

        public WinPath(string path) : base(PathStructure, CleanWinPath(path)) { }
        public WinPath(string path, DestType mode) : base(PathStructure, CleanWinPath(path), mode) { }
        private WinPath(WinPath path) : base(path) { }

        public WinPath(DecodingHandle handle) : base(PathStructure, handle) { }
        void IEncodable.DoEncode(EncodingHandle handle) => base.DoEncode(handle);

        static WinPath()
        {
            var sysDrive = Environment.GetEnvironmentVariable("SystemDrive");

            if (!string.IsNullOrWhiteSpace(sysDrive))
            {
                try { SystemDrive = new WinPath(sysDrive + "\\", DestType.Dir); }
                catch { SystemDrive = null; }
            }

            TempDirectory = new WinPath(System.IO.Path.GetTempPath(), DestType.Dir);

            EncodingRegister.RegisterTypes(typeof(WinPath));
        }

        public override AccessLevel CheckAccess()
        {
            if (!CheckExists())
                throw new PathAccessException(this, $"Cannot check access; path does not exist: {Path}");

            AccessLevel ret = AccessLevel.None;

            if (CanRead()) ret |= AccessLevel.Read;
            if (CanWrite()) ret |= AccessLevel.Write;
            if (CanDelete()) ret |= AccessLevel.Delete;

            return ret;
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

        public override PathBase Clone() => new WinPath(this);

        public override void CreateDirectory()
        {
            if (EndType == DestType.File)
                throw new PathException($"Cannot create a directory, this is a file path: {Path}");

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

            try
            {
                Directory.CreateDirectory(Path);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new PathAccessException(this, $"Cannot create dir, permission denied: {Path}", e);
            }
            catch (DirectoryNotFoundException e)
            {
                throw new PathAccessException(this, $"Cannot create dir, invalid path: {Path}", e);
            }
            catch (IOException e)
            {
                throw new PathAccessException(this, $"Cannot create dir: {Path}", e);
            }

            SetEndType(DestType.Dir);
            SetExists(true);
        }

        public override void Delete(bool recursive = false)
        {
            if (EndType == DestType.Dir)
            {
                try
                {
                    Directory.Delete(Path, recursive);
                    SetExists(false);
                }
                catch (DirectoryNotFoundException e)
                {
                    SetExists(false);
                    throw new PathAccessException(this, $"Cannot delete, path does not exist: {Path}", e);
                }
                catch (UnauthorizedAccessException e)
                {
                    throw new PathAccessException(this, $"Cannot delete, permission denied: {Path}", e);
                }
                catch (IOException e)
                {
                    throw new PathAccessException(this, $"Cannot delete dir: {Path}", e);
                }
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
                    SetExists(false);
                    throw new PathAccessException(this, $"Cannot delete, path does not exist: {Path}");
                }
            }
            else
            {
                try
                {
                    File.Delete(Path);
                    SetExists(false);
                }
                catch (DirectoryNotFoundException e)
                {
                    SetExists(false);
                    throw new PathAccessException(this, $"Cannot delete, path does not exist: {Path}", e);
                }
                catch (UnauthorizedAccessException e)
                {
                    throw new PathAccessException(this, $"Cannot delete, permission denied: {Path}", e);
                }
                catch (IOException e)
                {
                    throw new PathAccessException(this, $"Cannot delete file: {Path}", e);
                }
            }
        }

        public override IEnumerable<PathBase> EnumerateDirs()
        {
            IEnumerable<string> dirs;

            try
            {
                dirs = Directory.EnumerateDirectories(Path);
            }
            catch (DirectoryNotFoundException e)
            {
                throw new PathAccessException(this, $"Cannot get dirs, path does not exist: {Path}", e);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new PathAccessException(this, $"Cannot get dirs, permission denied: {Path}", e);
            }
            catch (Exception e)
            {
                if (EndType == DestType.File)
                    throw new PathException($"Cannot get dirs, this is a file path: {Path}", e);
                else
                    throw new PathException($"Unable to get dirs: {Path}", e);
            }

            return dirs.Select(s => (PathBase)new WinPath(s, DestType.Dir)).ToArray();
        }

        public override IEnumerable<PathBase> EnumerateFiles()
        {
            IEnumerable<string> files;

            try
            {
                files = Directory.EnumerateFiles(Path);
            }
            catch (DirectoryNotFoundException e)
            {
                throw new PathAccessException(this, $"Cannot get files, path does not exist: {Path}", e);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new PathAccessException(this, $"Cannot get files, permission denied: {Path}", e);
            }
            catch (Exception e)
            {
                if (EndType == DestType.File)
                    throw new PathException($"Cannot get files, this is a file path: {Path}", e);
                else
                    throw new PathException($"Unable to get files: {Path}", e);
            }

            return files.Select(s => (PathBase)new WinPath(s, DestType.File)).ToArray();
        }

        public override Stream OpenStream(FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            FileStream fs;

            try
            {
                fs = new FileStream(Path, fileMode, fileAccess, fileShare);
            }
            catch (FileNotFoundException e)
            {
                SetExists(false);
                throw new PathException($"File does not exist and FileMode was not set to CreateNew/Create/OpenOrCreate: {Path}", e);
            }
            catch (DirectoryNotFoundException e)
            {
                SetExists(false);
                throw new PathException($"Parent directory does not exist, cannot create/open file: {Path}", e);
            }
            catch (IOException e)
            {
                SetExists(CheckExists());

                var res = (IOHResultValues)(e.HResult & 0xFFFF);

                var msg = res switch
                {
                    IOHResultValues.FileSharingViolation => "This file is already open by another process.",
                    IOHResultValues.FileAlreadyExists => "The file already exists and CreateNew was used.",
                    IOHResultValues.FileOrDirectoryAlreadyExists => "The file or directory already exists.",
                    IOHResultValues.InvalidParameter => "Invalid parameter when opening file.",
                    _ => "File cannot be opened, unknown reason."
                };

                throw new PathException($"{Path}: {msg}", e);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new PathAccessException(this, $"Cannot open/create file, permission denied: {Path}", e);
            }
            catch (SecurityException e)
            {
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
                if (IsFile)
                {
                    var parent = GetContainingDir();
                    Watcher = new FileSystemWatcher(parent.Path)
                    {
                        Filter = EndPointName,
                        EnableRaisingEvents = true
                    };
                }
                else
                {
                    Watcher = new FileSystemWatcher(Path)
                    {
                        EnableRaisingEvents = true
                    };
                }
            }
            catch (ArgumentException e)
            {
                throw new PathAccessException(this, $"Cannot watch path: {Path}", e);
            }
            catch (Exception e)
            {
                throw new PathAccessException(this, $"Cannot watch path: {Path}", e);
            }

            Watcher.Changed += (sender, e) =>
            {
                OnPathChanged(new PathChangedEventArgs(PathChangeType.Changed, new WinPath(e.FullPath)));
            };

            Watcher.Deleted += (sender, e) =>
            {
                OnPathChanged(new PathChangedEventArgs(PathChangeType.Deleted, new WinPath(e.FullPath)));
            };

            Watcher.Created += (sender, e) =>
            {
                OnPathChanged(new PathChangedEventArgs(PathChangeType.Created, new WinPath(e.FullPath)));
            };

            Watcher.Renamed += (sender, e) =>
            {
                OnPathChanged(new PathChangedEventArgs(PathChangeType.Renamed, new WinPath(e.FullPath)));
            };

            if (!IsFile)
            {
                SetExists(true);
                SetEndType(DestType.Dir);
            }
        }

        public override void StopWatchingPath()
        {
            Watcher?.Dispose();
            Watcher = null;
        }

        public override PathBase GetWorkingDirectory() =>
            new WinPath(Directory.GetCurrentDirectory(), DestType.Dir);

        public static WinPath GetWorkingDir() =>
            new WinPath(Directory.GetCurrentDirectory(), DestType.Dir);

        public static WinPath GetTemporaryFile()
        {
            string file = System.IO.Path.GetTempFileName();
            return new WinPath(file, DestType.File);
        }

        public static string CleanWinPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path string cannot be null or empty.", nameof(path));

            string ret = path.Trim();

            ret = Environment.ExpandEnvironmentVariables(ret);
            ret = ret.Replace('/', '\\');

            while (ret.Contains(@"\\"))
            {
                if (ret.StartsWith(@"\\"))
                {
                    ret = @"\" + ret.TrimStart('\\');
                    break;
                }

                ret = ret.Replace(@"\\", @"\");
            }

            if (ret == ".")
            {
                ret = Directory.GetCurrentDirectory();
            }
            else if (ret.StartsWith(@".\"))
            {
                ret = Directory.GetCurrentDirectory().TrimEnd('\\') + ret[1..];
            }
            else if (ret == "..")
            {
                var cwd = new WinPath(Directory.GetCurrentDirectory(), DestType.Dir);

                if (cwd.Count <= 1)
                    throw new PathParsingException("The '..' operator cannot be used at the start of a path if the working path is root.");

                ret = cwd.GetContainingDir().ToString();
            }
            else if (ret.StartsWith(@"..\"))
            {
                var cwd = new WinPath(Directory.GetCurrentDirectory(), DestType.Dir);

                if (cwd.Count <= 1)
                    throw new PathParsingException("The '..' operator cannot be used at the start of a path if the working path is root.");

                ret = cwd.GetContainingDir().ToString().TrimEnd('\\') + @"\" + ret[3..];
            }

            if (ret.Length == 2 && char.IsLetter(ret[0]) && ret[1] == ':')
            {
                ret += @"\";
            }

            return ret;
        }

        private bool CanRead()
        {
            try
            {
                if (IsFile)
                {
                    using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                else if (IsDir)
                {
                    _ = Directory.EnumerateFileSystemEntries(Path).FirstOrDefault();
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool CanWrite()
        {
            try
            {
                if (IsFile)
                {
                    using var stream = new FileStream(Path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                    return true;
                }
                else if (IsDir)
                {
                    string probe = System.IO.Path.Combine(Path, Guid.NewGuid().ToString("N") + ".tmp");

                    using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                    }

                    File.Delete(probe);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool CanDelete()
        {
            try
            {
                if (IsRootPath) return false;

                var parent = GetContainingDir();

                if (!parent.Exists || !parent.IsDir)
                    return false;

                if (parent.Access.HasFlag(AccessLevel.Write))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}