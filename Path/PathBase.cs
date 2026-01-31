using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MandalaLogics.Encoding;
using MandalaLogics.StringValidation;

namespace MandalaLogics.Path
{
    public enum DestType : byte { Unknown = 0, File = 1, Dir = 2, SymLink = 3 }
    public enum PathType : byte { Unknown = 0, Absolute = 1, Relative = 2 }   
    [Flags]
    public enum AccessLevel
    {
        None = 0, 
        Write = 0b1,
        Read = 0b10,     
        Delete = 0b100,
        FullAccess = 0b11_1111,
        ReadWrite = Write | Read
    }

    public abstract partial class PathBase : IReadOnlyList<string>, IEquatable<PathBase>, IEncodable
    {
        public event PathChangedEventHandler? PathChanged;

        static readonly Regex extRegex = new Regex(@"(?<name>.+)\.(?<ext>[^\.]+)$", RegexOptions.Compiled);
        public static StringTemplate ExtTemplate => StringTemplate.FileExtension;
        public static StringTemplate NameTemplate => StringTemplate.FileName;
        public static string[] LengthUnits { get; } = new string[] { "B", "kB", "MB", "GB", "TB", "YB", "PB" };

        public string this[int index] => pathElements[index];        
        public int Count => pathElements.Count;
        public DestType EndType { get; private set; } = DestType.Unknown;
        public PathType Type { get; private set; } = PathType.Unknown;
        public string Extension { get; private set; } = string.Empty;
        public string Path
        {
            get
            {
                if (path is null) { return path = GetPath(); }
                else { return path; }
            }
        }
        public string PathTypeName => Structure.PathTypeName;
        public string EndPointName
        {
            get
            {
                if (HasExtension) return string.Join(".", pathElements[pathElements.Count - 1], Extension);
                else return pathElements[^1];
            }
        }
        public PathStructure Structure { get; }
        public virtual AccessLevel Access
        {
            get
            {
                if (access is null) return (AccessLevel)(access = CheckAccess());
                else return (AccessLevel)access;
            }
        }
        public PathProximityType PathProximity => Structure.PathProximity;
        public abstract string HostName {get;}

        public bool HasExtension => !string.IsNullOrEmpty(Extension);
        public bool IsFile
        {
            get
            {
                if (EndType == DestType.Unknown)
                {
                    EndType = CheckType();
                }

                return EndType.HasFlag(DestType.File);
            }
        }
        public bool IsDir
        {
            get
            {
                if (EndType == DestType.Unknown)
                {
                    EndType = CheckType();
                }

                return EndType.HasFlag(DestType.Dir);
            }
        }
        public bool IsSymLink => EndType.HasFlag(DestType.SymLink);
        public bool IsAbsolutePath => Type.HasFlag(PathType.Absolute);
        public bool IsRelativePath => Type.HasFlag(PathType.Relative);
        public virtual bool WatchingPath {get;protected set;}
        public virtual bool Exists
        {
            get
            {
                if (exists is null)
                {
                    return (bool)(exists = CheckExists());
                }
                else
                {
                    return (bool)exists;
                }
            }
        }
        public bool IsRootPath 
        {
            get
            {
                if (Structure.HasStartToken) 
                {
                    return pathElements.Count == 0 && Type == PathType.Absolute;
                }
                else 
                {
                    return pathElements.Count == 1 && Type == PathType.Absolute;
                }
            }            
        }

        protected bool? exists { get; private set; }
        protected AccessLevel? access { get; private set; }
        protected string path { get; private set; } = null;    

        private List<string> pathElements;

        /// <summary>
        /// The basic constructor for turning a string into a path object. Reccomend cleaning/checking the string before passing.
        /// </summary>
        /// <param name="ps">The structure which this class will use to create a path object.</param>
        /// <param name="path">The string represention of the path to parsed.</param>
        public PathBase(PathStructure ps, string path) : this(ps, path, DestType.Unknown) { }
        /// <summary>
        /// The basic constructor for turning a string into a path object. Reccomend cleaning/checking the string before passing.
        /// </summary>
        /// <param name="ps">The structure which this class will use to create a path object.</param>
        /// <param name="path">The string represention of the path to parsed.</param>
        /// <param name="mode">The type of path being processed, if known.</param>
        public PathBase(PathStructure ps, string path, DestType mode)
        {
            Structure = ps;

            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path string cannot be empty.");

            bool done = false;

            path = path.Trim();

            if (ps.HasStartToken)
            {
                if (path.StartsWith(ps.StartToken))
                {
                    if (ps.AllowOnlyStartToken && path.Length == ps.StartToken.Length)
                    {
                        done = true;
                        Type = PathType.Absolute;
                        EndType = DestType.Dir;
                    }
                    else
                    {
                        path = path.Substring(ps.StartToken.Length, path.Length - ps.StartToken.Length);
                        Type = PathType.Absolute;
                    }                    
                }
                else
                {
                    Type = PathType.Relative;
                }
            }

            List<string> ls = new List<string>();
            string[] s;

            if (ps.HasFirstSeperator && !done)
            {
                s = ps.SplitFirstSeperator(path);

                if (s.Length > 2) throw new PathParsingException($"Invalid path ({path}); cannot have two or more of the initial seperator ({ps.FirstSeperator}).");

                if (s.Length > 1) //split into two parts
                {                    
                    path = s[1];
                    Type = PathType.Absolute;
                    ls.Add(s[0].Trim());
                }
                else if (path.EndsWith(ps.FirstSeperator))
                {
                    EndType = DestType.Dir;
                    Type = PathType.Absolute;
                    done = true;
                    ls.Add(s[0].Trim());
                }
                else
                {
                    Type = PathType.Relative;
                }                
            }            

            bool checkType = EndType == DestType.Unknown;

            if (!done)
            {
                s = ps.SplitSeperator(path);

                if (mode != DestType.Dir)
                {
                    var m = extRegex.Match(s[^1]);

                    if (m.Success)
                    {
                        for (int x = 0; x < s.Length - 1; x++) { ls.Add(s[x].Trim()); }

                        ls.Add(m.Groups["name"].Value);

                        Extension = m.Groups["ext"].Value;

                        EndType = DestType.File;

                        if (mode == DestType.Unknown) { checkType = true; }
                    }
                    else
                    {
                        foreach (string ele in s) { ls.Add(ele.Trim()); }
                    }
                }
                else
                {
                    foreach (string ele in s) { ls.Add(ele.Trim()); }
                }
            }

            if (Type == PathType.Absolute && ps.HasFixedFirstElementLen && ls.Count > 0 && ls[0].Length != ps.FirstElementLength)
            {
                throw new PathParsingException($"Invalid path ({path}); first element of path must be {ps.FirstElementLength} charachters long.");
            }

            pathElements = ls;

            if (pathElements.Count < Structure.RootLength) throw new PathParsingException($"Path element length is less than root length({Structure.RootLength}): {path}");        

            if (checkType && Type == PathType.Absolute)
            {
                DestType checkedType;

                try { checkedType = CheckType(); }
                catch (Exception e)
                {
                    throw new PathImplimentationException("Calling CheckType() failed. To implement the PathBase class CheckType must return the type of object " +
                        "pointed to by the path, where possible.", e);
                }

                if (checkedType == DestType.Unknown)
                {
                    exists = false;
                }
                else
                {
                    if (checkedType == DestType.Dir && HasExtension)
                    {
                        pathElements.RemoveAt(pathElements.Count - 1);
                        pathElements.Add(string.Join(".", pathElements.Last(), Extension));
                        Extension = string.Empty;
                        exists = true;
                    }
                    else
                    {
                        EndType = checkedType;
                    }

                    exists = true;
                }                
            }             
            else if (!done)
            {
                EndType = mode;
            }
        }
        /// <summary>
        /// Provides a shallow copy of the given path.
        /// </summary>
        /// <param name="path">The path to copy.</param>
        public PathBase(PathBase path)
        {
            Structure = path.Structure;
            Type = path.Type;
            EndType = path.EndType;
            pathElements = path.pathElements.AsEnumerable().ToList();
            Extension = path.Extension;
        }

        //ENCODING
        protected PathBase(PathStructure pathStructure, DecodingHandle handle)
        {
            Structure = pathStructure;
            
            Type = handle.Next<PathType>();
            Extension = handle.Next<string>();
            pathElements = new List<string>(handle.Next<string[]>());
        }
        public virtual void DoEncode(EncodingHandle encodedObj)
        {
            encodedObj.Append(Type);
            encodedObj.Append(Extension);
            encodedObj.Append(pathElements);
        }

        /// <summary>
        /// Opens a stream to this file.
        /// </summary>
        /// <returns>A stream to this file.</returns>
        public abstract Stream OpenStream(FileMode fileMode, FileAccess fileAccess, FileShare fileShare);
        /// <summary>
        /// Performs a check which verifies if the file/dir exists and wither is is a file/dir.
        /// </summary>
        /// <returns></returns>
        public abstract DestType CheckType();
        /// <summary>
        /// Checks if the file or dir exists.
        /// </summary>
        public abstract bool CheckExists();
        /// <summary>
        /// Deletes this file or dir.
        /// </summary>
        public abstract void Delete(bool recursive = false);
        /// <summary>
        /// If the dir does not exist then it is created, otherwise throws a PathException.
        /// </summary>
        public abstract void CreateDirectory();
        /// <summary>
        /// Gets all dirs in the directory.
        /// </summary>
        /// <param name="pattern">String to match with * being the wildcard.</param>
        public abstract IEnumerable<PathBase> EnumerateDirs();
        /// <summary>
        /// Gets all files in the directory.
        /// </summary>
        /// <param name="pattern">String to match with * being the wildcard.</param>
        public abstract IEnumerable<PathBase> EnumerateFiles();
        /// <summary>
        /// Creates a copy of this path which is of the same derived type of class.
        /// </summary>
        public abstract PathBase Clone();
        /// <summary>
        /// Checks what kind of access is avalible to the file or dir.
        /// </summary>
        /// <returns>An enum which specifies if this program is able to read and/or write to the file/dir. If a dir cannot be read from it will be assumed that it cannot be written to.</returns>
        public abstract AccessLevel CheckAccess();
        /// <summary>
        /// Starts watching this path and fires the PathChanged event when the file or dir is modified.
        /// </summary>
        public abstract void StartWatchingPath();
        /// <summary>
        /// Stops watching the path and stops firing the PathChanged event.
        /// </summary>
        public abstract void StopWatchingPath();
        public abstract long FileLength();
        public abstract PathBase GetWorkingDirectory();

        /// <summary>
        /// Allows the implimenting class to directly set the existance of the file/dir, for example: if the file is deleted.
        /// </summary>
        /// <param name="value">A nullable bool. Null indicates that the existance of the file/dir is unknown.</param>
        protected void SetExists(bool? value) { exists = value; }
        protected void SetAccess(AccessLevel? value) { access = value; }
        protected void SetEndType(DestType value) { EndType = value; }
        protected void OnPathChanged(PathChangedEventArgs args)
        {
            PathChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Returns true if this path could be created if it does not exist, i.e. if the path is absolute and the containting dir exists.
        /// Use the Exists property to confirm if the path exists already.
        /// </summary>
        /// <returns>If this path exists, or could be created if it does not.</returns>
        public bool IsValidPath()
        {
            if (IsAbsolutePath && Exists) { return true; }

            if (IsFile)
            {
                if (IsAbsolutePath)
                {
                    return GetContainingDir().Exists;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (IsRootPath)
                {
                    return true;
                }
                else if (IsAbsolutePath)
                {
                    return GetContainingDir().Exists;
                }
                else
                {
                    return false;
                }
            }
        }
        public virtual ObjectTreeNode<PathBase> Tree()
        {
            if (!IsDir) { throw new PathException("Cannot get a path tree for a file path."); }
            else if (!Exists) { throw new PathException("Specifidied path does not exist."); }

            var root = ObjectTreeNode<PathBase>.CreateRoot(this);

            var dir = Dir();
            ObjectTreeNode<PathBase> node;
            Stack<ObjectTreeNode<PathBase>> folders = new Stack<ObjectTreeNode<PathBase>>();

            foreach (PathBase pb in dir)
            {
                node = root.AddChild(pb);

                if (pb.IsDir) { folders.Push(node); }
            }

            while (folders.TryPop(out node))
            {
                dir = node.Value.Dir();

                foreach (PathBase pb in dir)
                {
                    node = node.AddChild(pb);

                    if (pb.IsDir) { folders.Push(node); }
                }
            }

            return root;
        }
        /// <summary>
        /// Copies this file path to the distination path.
        /// </summary>
        /// <param name="dest">The destination path at which to create the copy.</param>
        /// <param name="overWrite">Indicates that if the file exists then it should be overwritten.</param>
        public virtual void CopyFile(PathBase dest, bool overWrite = false)
        {
            if (dest.Exists && !overWrite) throw new PathException($"Destination path already exists: {dest}");
            else if (!dest.IsFile || !dest.IsAbsolutePath) throw new PathTypeException($"Cannot copy to this path because it is not an absolute file path: {dest}");
            else if (!IsFile || !IsAbsolutePath) throw new PathTypeException($"Cannot copy from this path because it is not an absolute file path: {dest}");

            Stream src;
            Stream dst = null;

            try
            {
                src = OpenStream(FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (PathAccessException e) { throw new PathAccessException(this, $"Failed to open stream for copying.", e); }

            try
            {
                dst = dest.OpenStream(overWrite ? FileMode.OpenOrCreate : FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                dst.SetLength(0);
            }
            catch (PathAccessException e)
            {
                src.Dispose();
                dst?.Dispose();
                throw new PathAccessException(this, $"Failed to open stream for copying.", e);
            }

            try
            {
                src.CopyTo(dst);
            }
            catch (Exception e)
            {
                throw new IOException("Failed to copy file: " + Path, e);
            }
            finally
            {
                dst.Dispose();
                src.Dispose();
            }
        }
        /// <summary>
        /// Copies this file path to the destination path.
        /// </summary>
        /// <param name="dest">The destination path at which to create the copy.</param>
        /// <param name="overWrite">Indicates that if the file exists then it should be overwritten.</param>
        public virtual async Task CopyFileAsync(PathBase dest, CancellationToken cancellationToken, bool overWrite = false)
        {
            if (dest.Exists && !overWrite) throw new PathException($"Destination path already exists: {dest}");
            else if (!dest.IsFile || !dest.IsAbsolutePath) throw new PathTypeException($"Cannot copy to this path because it is not an absolute file path: {dest}");
            else if (!IsFile || !IsAbsolutePath) throw new PathTypeException($"Cannot copy from this path because it is not an absolute file path: {dest}");

            Stream src;
            Stream dst = null;

            try
            {
                src = OpenStream(FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (PathAccessException e) { throw new PathAccessException(this, $"Failed to open stream for copying.", e); }

            try
            {
                dst = dest.OpenStream(overWrite ? FileMode.OpenOrCreate : FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                dst.SetLength(0);
            }
            catch (PathAccessException e)
            {
                src.Dispose();
                dst?.Dispose();
                throw new PathAccessException(this, $"Failed to open stream for copying.", e);
            }

            try 
            {
                await src.CopyToAsync(dst, 10240, cancellationToken);
                await dst.FlushAsync();                
            }
            catch (Exception e) { throw new IOException("Failed to copy file.", e); }
            finally
            {
                dst.Dispose();
                src.Dispose();
            }
        }
        /// <summary>
        /// Copies a directory to a destination location.
        /// </summary>
        /// <param name="dst">The path at which to create the copied directory.</param>
        /// <param name="overWrite">Indicates that if the destination path exists that it should be overwritten.</param>
        public virtual void CopyDir(PathBase dst, bool overWrite = false, bool copyFolders = true)
        {
            if (dst.Exists && !overWrite) throw new PathException($"Destination path already exists: {dst}");
            else if (!dst.IsDir || !dst.IsAbsolutePath) throw new PathTypeException($"Cannot copy to this path because it is not an absolute dir path: {dst}");
            else if (!IsDir || !IsAbsolutePath) throw new PathTypeException($"Cannot copy from this path because it is not an absolute dir path: {dst}");
            else if (dst.IsRootPath) throw new PathTypeException($"Cannot copy a dir if the destination is a root path: {dst}");

            IEnumerable<PathBase> srcPaths = Dir();

            if (dst.Exists)
            {
                if (!dst.Access.HasFlag(AccessLevel.Delete))
                {
                    if (dst.Access.HasFlag(AccessLevel.Write))
                    {
                        IEnumerable<PathBase> deletePaths = dst.Dir();

                        foreach (PathBase path in deletePaths)
                        {
                            if (!path.Access.HasFlag(AccessLevel.Delete)) throw new PathAccessException(dst, "Cannot copy to this dir because it already exists and/or one or more file/folders cannot be deleted.");
                        }

                        foreach (PathBase path in deletePaths)
                        {
                            try { path.Delete(); }
                            catch (PathException)
                            {
                                throw new PathAccessException(dst, "Cannot clear all the files in this directory.");
                            }
                        }
                    }
                    else
                    {
                        throw new PathAccessException(dst, $"Cannot write to this dir ({dst}) or delete this dir.");
                    }
                }
                else
                {
                    dst.Delete(true);
                    dst.CreateDirectory();
                }
            }
            else
            {
                dst.CreateDirectory();
            }

            foreach (PathBase path in srcPaths)
            {
                if (path.IsFile) { path.CopyFile(dst.Add(path.TakeEnd(1))); }
                else if (copyFolders) { path.CopyDir(dst.Add(path.TakeEnd(1))); }
            }
        }
        /// <summary>
        /// Copies a directory to a destination location.
        /// </summary>
        /// <param name="dst">The path at which to create the copied directory.</param>
        /// <param name="overWrite">Indicates that if the destination path exists that it should be overwritten.</param>
        public async Task CopyDirAsync(PathBase dst, CancellationToken cancellationToken, bool overWrite = false, bool copyFolders = true)
        {
            if (dst.Exists && !overWrite) throw new InvalidOperationException($"Destination path already exists: {dst}");
            else if (!dst.IsDir || !dst.IsAbsolutePath) throw new PathTypeException($"Cannot copy to this path because it is not an absolute dir path: {dst}");
            else if (!IsDir || !IsAbsolutePath) throw new PathTypeException($"Cannot copy from this path because it is not an absolute dir path: {dst}");
            else if (dst.IsRootPath) throw new PathTypeException($"Cannot copy a dir if the destination is a root path: {dst}");

            IEnumerable<PathBase> srcPaths = Dir();

            if (dst.Exists)
            {
                if (!dst.Access.HasFlag(AccessLevel.Delete))
                {
                    if (dst.Access.HasFlag(AccessLevel.Write))
                    {
                        IEnumerable<PathBase> deletePaths = dst.Dir();

                        foreach (PathBase path in deletePaths)
                        {
                            if (!path.Access.HasFlag(AccessLevel.Delete)) throw new PathAccessException(dst, "Cannot copy to this dir because it already exists and/or one or more file/folders cannot be deleted.");
                        }

                        foreach (PathBase path in deletePaths)
                        {
                            try { path.Delete(); }
                            catch (PathException)
                            {
                                throw new PathAccessException(dst, "Cannot clear all the files in this directory.");
                            }
                        }
                    }
                    else
                    {
                        throw new PathAccessException(dst, $"Cannot write to this dir ({dst}) or delete this dir.");
                    }
                }
                else
                {
                    dst.Delete(true);
                    dst.CreateDirectory();
                }                
            }      
            else
            {
                dst.CreateDirectory();
            }

            foreach (PathBase path in srcPaths)
            {
                if (path.IsFile) { await path.CopyFileAsync(dst.Add(path.TakeEnd(1)), cancellationToken); }
                else if (copyFolders) { await path.CopyDirAsync(dst.Add(path.TakeEnd(1)), cancellationToken); }
            }
        }
        public virtual void MergeDirInto(PathBase dst, bool recursive)
        {
            if (!dst.Exists) throw new PathException($"Destination path does not exist: {dst}");
            else if (!dst.IsDir || !dst.IsAbsolutePath) throw new PathTypeException($"Cannot copy to this path because it is not an absolute dir path: {dst}");
            else if (!IsDir || !IsAbsolutePath) throw new PathTypeException($"Cannot copy from this path because it is not an absolute dir path: {dst}");

            var srcPaths = Dir().ToList();
            var dstPaths = srcPaths.ConvertAll((path) => dst.Append(path.EndPointName));

            for (int x = 0; x < srcPaths.Count; x++)
            {
                if (dstPaths[x].Exists) continue;

                if (srcPaths[x].IsFile)
                {
                    srcPaths[x].CopyFile(dstPaths[x], false);
                }
                else if (recursive)
                {
                    srcPaths[x].MergeDirInto(dstPaths[x], true);
                }
            }
        }
        public virtual async Task MergeDirIntoAsync(PathBase dst, CancellationToken cancellationToken, bool recursive)
        {
            if (!dst.Exists) throw new PathException($"Destination path does not exist: {dst}");
            else if (!dst.IsDir || !dst.IsAbsolutePath) throw new PathTypeException($"Cannot copy to this path because it is not an absolute dir path: {dst}");
            else if (!IsDir || !IsAbsolutePath) throw new PathTypeException($"Cannot copy from this path because it is not an absolute dir path: {dst}");

            var srcPaths = Dir().ToList();
            var dstPaths = srcPaths.ConvertAll((path) => dst.Append(path.EndPointName));

            for (int x = 0; x < srcPaths.Count; x++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (dstPaths[x].Exists) continue;

                if (srcPaths[x].IsFile)
                {
                    await srcPaths[x].CopyFileAsync(dstPaths[x], cancellationToken, false);
                }
                else if (recursive)
                {
                    await srcPaths[x].MergeDirIntoAsync(dstPaths[x], cancellationToken, true);
                }
            }
        }
        /// <summary>
        /// Gets the root path, e.g. the drive-letter path in a windows path. Throws if there is not root path or if this path is root.
        /// </summary>
        public virtual PathBase GetRootPath()
        {
            if (Type != PathType.Absolute) throw new PathTypeException("Cannot get the root path from a non-abolsute path.");
            else 
            {
                if (Structure.AllowOnlyStartToken)
                {
                    var ret = Clone();

                    ret.pathElements = new List<string>();
                    ret.EndType = DestType.Dir;
                    ret.Extension = null;

                    return ret;

                }
                else { return TakeStart(Structure.RootLength); }                
            }
        }
        /// <summary>
        /// Returns a list of all files and folders within the dir.
        /// </summary>
        /// <summary>
        /// Returns a list of all files and folders in the dir.
        /// </summary>
        public virtual IEnumerable<PathBase> Dir() => EnumerateDirs().Concat(EnumerateFiles());
        /// <summary>
        /// Creates a sub-directory by appending a name to this path.
        /// </summary>
        /// <param name="name">The directory name to append.</param>
        /// <returns>The path object relating to the newly-created directory.</returns>
        public virtual PathBase CreateDirectory(string name)
        {
            var pb = PathAppend(this, name, DestType.Dir);

            pb.CreateDirectory();

            return pb;
        }        
        /// <summary>
        /// Gets a string represention of the path. Reccomended to use Path property instead.
        /// </summary>
        /// /// <summary>
        /// Creates a new sub-directory with the given name. Thorws if the dir already exists.
        /// </summary>
        /// <param name="name">The name of the sub-directory to create.</param>
        /// <returns>A path object for the newly-created directory.</returns>
        public string GetPath() => GetPath(0, Count);
        /// <summary>
        /// Gets a string representation of the path starting at the specified element for the given number of elements.
        /// </summary>
        public virtual string GetPath(int start, int count)
        {
            int n = start + count;

            if (count == 0)
            {
                if (Structure.HasStartToken) { return Structure.StartToken; }
                else { return string.Empty; }
            }
            else if (count < 0) throw new ArgumentException("count cannot be less zero.");
            else if (start < 0) throw new ArgumentException("start cannot be less zero.");
            else if (start >= Count) throw new ArgumentException("start cannot be beyond the number of elements.");
            else if (n > Count) throw new ArgumentException("The sum of start and count cannot be more than the number of elements");

            StringBuilder sb = new StringBuilder();            

            if (start == 0 && IsAbsolutePath)
            {
                if (Structure.HasStartToken) { sb.Append(Structure.StartToken); }

                sb.Append(pathElements[0]);

                if (Structure.HasFirstSeperator) { sb.Append(Structure.FirstSeperator); }
                else
                { 
                    if (count > 1) { sb.Append(Structure.Seperator); }
                }

                start = 1;
            }

            for (int x = start; x < n; x++)
            {
                sb.Append(pathElements[x]);
                if (x < n - 1) sb.Append(Structure.Seperator);
            }

            if (HasExtension) { sb.Append("." + Extension); }

            return sb.ToString();
        }
        /// <summary>
        /// Adds one path to another.
        /// </summary>
        /// <param name="path">The path to be added to this one.</param>
        /// <returns>The sum of both paths.</returns>
        public virtual PathBase Add(PathBase path) => PathAddition(this, path);
        /// <summary>
        /// Subtracts another path from this one if this path contains the given path.
        /// </summary>
        /// <param name="path">The path to subtract.</param>
        /// <returns>The result of the subtration.</returns>
        public virtual PathBase Subract(PathBase path) => PathSubrtation(this, path);
        /// <summary>
        /// Returns true if the path starts with the given path.
        /// </summary>
        public virtual bool StartsWith(PathBase path) => PathStartsWith(this, path);
        /// <summary>
        /// Returns true if the path ends with the given path.
        /// </summary>
        public virtual bool EndsWith(PathBase path) => PathEndsWith(this, path);
        /// <summary>
        /// Takes the specified number of elements from the path starting at the specified position.
        /// </summary>
        public virtual PathBase SubPath(int start, int count) => PathSubpath(this, start, count);
        /// <summary>
        /// Gets the containing path for this path, if there is one.
        /// </summary>
        public virtual PathBase GetContainingDir() => PathContainingDir(this);
        /// <summary>
        /// Gets all the siblings for this file or dir, if there are any.
        /// </summary>
        public virtual IEnumerable<PathBase> Siblings() => PathSiblings(this);
        /// <summary>
        /// Returns and path with the specified number of elements taken from the start of this path.
        /// </summary>
        public virtual PathBase TakeStart(int count) => PathSubpath(this, 0, count);
        /// <summary>
        /// Returns and path with the specified number of elements taken from the end of this path.
        /// </summary>
        public virtual PathBase TakeEnd(int count) => PathSubpath(this, pathElements.Count - count, count);
        /// <summary>
        /// Intelligently appends a file/dir name or subpath to this path, determining if it is a file or dir.
        /// </summary>
        /// <param name="name">The name or subpath to append to this path.</param>
        /// <returns>The result of the addition.</returns>
        public virtual PathBase Append(string name) => PathAppend(this, name);
        /// <summary>
        /// Adds a definined file/dir name or subpath to this path.
        /// </summary>
        /// <param name="name">The name or subpath to append to this path.</param>
        /// <param name="type">The type of subpath or file/dir name which is being added. May not be unknown.</param>
        /// <returns>The result of the addition, which is of the type specified.</returns>
        public virtual PathBase Append(string name, DestType type) => PathAppend(this, name, type);
        /// <summary>
        /// Sets or chranges the extension of this file path. If path is not a file path it will be made into a file path.
        /// </summary>
        /// <param name="ext">The extension to appned/change, e.g. "txt".</param>
        /// <returns></returns>
        public PathBase AppendExtension(string ext)
        {
            var ret = Clone();

            if (string.IsNullOrWhiteSpace(ext))
            {
                throw new ArgumentNullException("ext");
            }
            else
            {
                if (!ExtTemplate.TryValidate(ext, out string ext2))
                {
                    throw new NameNotValidException($"Extension is not valid: '{ext}'.");
                }

                ret.EndType = DestType.File;
                ret.Extension = ext2;
            }            

            return ret;
        }
        /// <summary>
        /// Returns this path as a file path.
        /// </summary>
        public PathBase AsFile()
        {
            var ret = Clone();

            ret.EndType = DestType.File;

            return ret;
        }
        /// <summary>
        /// Returns this path as a file path while adding/setting the extension of the file.
        /// <param name="ext">The extension to appned/change, e.g. "txt".</param>
        /// </summary>
        public PathBase AsFile(string ext) => AppendExtension(ext);
        public PathBase AsRelaitve()
        {
            if (IsRelativePath) { throw new PathTypeException("Cannot get realitive path from path which is already realitive: " + Path); }
            else if (IsRootPath) { throw new PathTypeException("Cannot get realitive path from root path: " + Path); }

            var ret = Clone();
            ret.Type = PathType.Relative;

            if (Structure.HasFirstSeperator)
            {
                ret.pathElements = ret.pathElements.TakeLast(Count - 1).ToList();
            }

            return ret;
        }
        /// <summary>
        /// Returns this path as a dir path.
        /// </summary>
        public PathBase AsDir()
        {
            var ret = Clone();

            ret.EndType = DestType.Dir;

            if (ret.HasExtension)
            {
                ret.pathElements[ret.Count - 1] = string.Join(".", ret.pathElements[ret.Count - 1], ret.Extension);
                ret.Extension = null;
            }

            return ret;
        }
        /// <summary>
        /// Changes the file or dir name of this path.
        /// <param name="name">The new name for the dir/file, which may include file extension.</param>
        /// </summary>
        public PathBase ChangeName(string name)
        { 
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException("name");

            var ret = Clone();

            name = name.Trim();

            var m = extRegex.Match(name);

            if (m.Success && IsFile)
            {
                if (ExtTemplate.CatchValidate(m.Groups[2].Value, out string ext) is StringValidationException extException)
                {
                    throw new NameNotValidException($"File extension is not valid: '{ext}'. {extException.Message}");
                }
                else if (NameTemplate.CatchValidate(m.Groups[1].Value, out name) is StringValidationException nameException)
                {
                    throw new NameNotValidException($"File name is not valid: '{name}'. {nameException.Message}");
                }

                ret.pathElements[ret.Count - 1] = name;
                ret.Extension = ext;
            }
            else
            {
                if (NameTemplate.CatchValidate(name, out name) is StringValidationException nameException)
                {
                    throw new NameNotValidException($"File name is not valid: '{name}'. {nameException.Message}");
                }

                ret.pathElements[ret.Count - 1] = name;
                ret.Extension = null;
            }

            return ret;
        }
        /// <summary>
        /// Changes the name of the dir/file for this path.
        /// </summary>
        /// <param name="name">The new name of the file/dir for this path.</param>
        /// <param name="mode">If set to file then checks for extension, if set to dir then ignores extension.</param>
        /// <returns></returns>
        public PathBase ChangeName(string name, DestType mode)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException("name");
            else if (mode == DestType.Unknown) return ChangeName(name);

            var ret = Clone();

            name = name.Trim();

            if (mode == DestType.File)
            {
                var m = extRegex.Match(name);

                if (m.Success)
                {
                    if (!ExtTemplate.TryValidate(m.Groups[1].Value, out string ext))
                    {
                        throw new NameNotValidException($"Extension is not valid: '{ext}'.");
                    }

                    if (!ExtTemplate.TryValidate(m.Groups[2].Value, out name))
                    {
                        throw new NameNotValidException($"File/dir name is not valid: '{m.Groups[2].Value}'.");
                    }

                    ret.pathElements[ret.Count - 1] = name;
                    ret.Extension = ext;
                }
                else
                {
                    if (!ExtTemplate.TryValidate(name, out name))
                    {
                        throw new NameNotValidException($"File/dir name is not valid: '{name}'.");
                    }

                    ret.pathElements[ret.Count - 1] = name;
                    ret.Extension = null;
                }
            }
            else
            {
                if (!ExtTemplate.TryValidate(name, out name))
                {
                    throw new NameNotValidException($"File/dir name is not valid: '{name}'.");
                }

                ret.pathElements[ret.Count - 1] = name;
            }

            ret.EndType = mode;

            return ret;
        }
        /// <summary>
        /// Returns true if this path contains the given path as a descendant.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns></returns>
        public bool ContainsPath(PathBase path)
        {
            if (path is null) throw new ArgumentNullException("path");            
            else if (IsAbsolutePath != path.IsAbsolutePath) throw new PathTypeException($"Cannot say if an {Type}-type path contains a {path.Type}-type path.");

            return path.StartsWith(this);
        }
        /// <summary>
        /// Returns a valid dir new child dir path for this dir in the form "new folder {x}".
        /// </summary>
        public PathBase GetValidDirName()
        {
            var dirs = EnumerateDirs();

            string name = "New Folder";
            int x = 0;

            while (dirs.Any((path) => path.EndPointName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                name = $"New Folder {++x}";
            }

            return PathAppend(this, name, DestType.Dir);
        }
        /// <summary>
        /// Returns a valid dir new child file path for this dir in the form "new file {x}".
        /// </summary>
        public PathBase GetValidFileName(string extension)
        {
            if (extension is null) { extension = string.Empty; }
            else if (!string.IsNullOrWhiteSpace(extension) && !extension.StartsWith(".")) { extension = "." + extension; }

            var dirs = EnumerateFiles();

            string name = $"New File{extension}";
            int x = 0;

            while (dirs.Any((path) => path.EndPointName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                name = $"New File {++x}{extension}";
            }

            return PathAppend(this, name, DestType.File);
        }
        /// <summary>
        /// Returns true if the file or dir has the required access, otherwise false
        /// </summary>
        /// <param name="requiredAccess">The access level required.</param>
        public bool HasAccess(AccessLevel requiredAccess)
        {
            AccessLevel mask;
            AccessLevel acc = Access;

            for (int x = 0; x <= 5; x++)
            {
                mask = (AccessLevel)(1 << x);

                if ((requiredAccess & mask) == mask && (acc & mask) == 0)
                {
                    return false;
                }
            }

            return true;
        }
        /// <summary>
        /// Gets the end-point of the path e.g. 'C:\users\test.txt' returns 'test.txt', a realitive path which is marked as a file.
        /// </summary>
        public PathBase EndPointPath()
        {
            if (Count == 1) { return this; }
            else
            {
                var ret = Clone();

                ret.pathElements.RemoveRange(0, ret.pathElements.Count - 1);

                ret.Type = PathType.Relative;

                return ret;
            }
        }
        public IEnumerator<string> GetEnumerator() => ((IReadOnlyList<string>)pathElements).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IReadOnlyList<string>)pathElements).GetEnumerator();

        public static PathBase PathSubrtation(PathBase path1, PathBase path2)
        {
            if (path1 is null) throw new ArgumentNullException("path1");
            else if (path2 is null) throw new ArgumentNullException("path2");

            if (path2.Count >= path1.Count)
            {
                throw new PathException("Cannot subtract a path which has the same number of elements " +
                "(or more elements) than the first path.");
            }

            var ret = path1.Clone();

            if (path1.IsAbsolutePath)
            {
                if (path2.IsAbsolutePath)
                {
                    if (path1.StartsWith(path2))
                    {
                        ret.pathElements = ret.pathElements.TakeLast(path1.Count - path2.Count).ToList();
                    }
                    else if (path1.EndsWith(path2))
                    {
                        ret.pathElements = ret.pathElements.Take(path1.Count - path2.Count).ToList();
                    }
                    else
                    {
                        throw new PathException("Can only subtract a path from the begining or end of a path.");
                    }
                }
                else
                {
                    if (path1.EndsWith(path2))
                    {
                        ret.pathElements = ret.pathElements.Take(path1.Count - path2.Count).ToList();
                    }
                    else if (path1.StartsWith(path2))
                    {
                        ret.pathElements = ret.pathElements.TakeLast(path1.Count - path2.Count).ToList();
                    }
                    else
                    {
                        throw new PathException("Can only subtract a path from the begining or end of a path.");
                    }
                }
            }
            else
            {
                if (path1.EndsWith(path2))
                {
                    ret.pathElements = ret.pathElements.Take(path1.Count - path2.Count).ToList();
                }
                else if (path1.StartsWith(path2))
                {
                    ret.pathElements = ret.pathElements.TakeLast(path1.Count - path2.Count).ToList();
                }
                else
                {
                    throw new PathException("Can only subtract a path from the begining or end of a path.");
                }                
            }

            return ret;
        }
        public static PathBase PathAddition(PathBase path1, PathBase path2)
        {
            if (path1 is null) throw new ArgumentNullException("path1");
            else if (path2 is null) throw new ArgumentNullException("path2");

            if (path2.IsAbsolutePath) throw new PathTypeException("Cannot append an absolute path, must be a relitive path.");
            else if (path1.IsFile) throw new PathTypeException("Cannot append anything to a file path.");

            var ret = path1.Clone();

            ret.pathElements = ret.pathElements.Concat(path2.pathElements).ToList();
            ret.EndType = path2.EndType;

            if (path2.HasExtension)
            {
                ret.Extension = path2.Extension;
            }

            if (ret.EndType == DestType.Unknown) ret.EndType = ret.CheckType();

            return ret;
        }
        public static bool PathStartsWith(PathBase path1, PathBase path2)
        {
            if (path1 is null) throw new ArgumentNullException("path1");
            else if (path2 is null) throw new ArgumentNullException("path2");

            if (path1.Count < path2.Count) return false;

            if (path1.IsAbsolutePath == path2.IsAbsolutePath)
            {
                return path1.pathElements.Take(path2.Count).SequenceEqual(path2.pathElements, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                return false;
            }
        }
        public static bool PathEndsWith(PathBase path1, PathBase path2)
        {
            if (path1 is null) throw new ArgumentNullException("path1");
            else if (path2 is null) throw new ArgumentNullException("path2");

            if (path2.IsAbsolutePath) return false;
            else if (path1.Count < path2.Count) return false;

            return path1.pathElements.Skip(path1.Count - path2.Count).SequenceEqual(path2.pathElements, StringComparer.OrdinalIgnoreCase);
        }
        public static PathBase PathSubpath(PathBase path, int start, int count)
        {
            if (path is null) throw new ArgumentNullException("path");

            if (start < 0 || start >= path.Count) throw new ArgumentException($"Start index out of range: {start}");
            else if (count <= 0) throw new ArgumentException($"Count must be greater than zero: {count}");
            else if (count + start > path.Count) throw new ArgumentException($"The sume of count ({count}) and start ({start}) cannot be beyond the number of elements ({path.Count}).");

            int n = 0;
            int x = start;
            string[] s = new string[count];

            do
            {
                s[n] = path.pathElements[x]; 
                n++;
                x++;
            } while (n < count);

            var ret = path.Clone();

            ret.pathElements = s.ToList();

            if (start > 0) { ret.Type = PathType.Relative; }

            if (start + count < path.Count) 
            { 
                ret.EndType = DestType.Dir;
                ret.Extension = null;
            }

            return ret;
        }
        public static PathBase PathContainingDir(PathBase path)
        {
            if (path is null) throw new ArgumentNullException("path");

            if (path.Count <= 1) throw new PathException("Cannot get the containg path for a path with only one element.");

            var ret = path.Clone();

            ret.pathElements = ret.pathElements.Take(ret.Count - 1).ToList();
            ret.EndType = DestType.Dir;
            ret.Extension = null;

            return ret;
        }
        public static IEnumerable<PathBase> PathSiblings(PathBase path)
        {
            if (path is null) throw new ArgumentNullException("path");
            else if (path.Count <= 1) throw new PathException("This path does not have siblings because it is a root path.");

            return path.GetContainingDir().Dir().Where((p) => !p.Equals(path));
        }
        public static PathBase PathAppend(PathBase path, string name)
        {
            if (path is null) throw new ArgumentNullException("path");
            else if (path.IsFile) throw new PathTypeException("Cannot append anything to a file path.");
            else if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name to append cannot be null or empty.");

            name = name.Trim();

            string[] s = path.Structure.SplitSeperator(name);
            List<string> ls;
            var ret = path.Clone();

            if (s.Length > 1)
            {
                ls = s.Take(s.Length - 1).ToList();
            }
            else
            {
                ls = new List<string>();
            }

            string fileName;

            var m = extRegex.Match(s.Last());

            if (m.Success)
            {
                if (ExtTemplate.CatchValidate(m.Groups[2].Value, out string ext) is StringValidationException extException)
                {
                    throw new NameNotValidException($"File extension is not valid: '{ext}'. {extException.Message}");
                }
                else if (NameTemplate.CatchValidate(m.Groups[1].Value, out fileName) is StringValidationException nameException)
                {
                    throw new NameNotValidException($"File name is not valid: '{fileName}'. {nameException.Message}");
                }

                ls.Add(fileName);
                ret.Extension = ext;
                ret.EndType = DestType.File;
            }
            else
            {
                if (NameTemplate.CatchValidate(s.Last(), out fileName) is StringValidationException nameException)
                {
                    throw new NameNotValidException($"File name is not valid: '{fileName}'. {nameException.Message}");
                }

                ls.Add(fileName);
            }

            ret.pathElements = ret.pathElements.Concat(ls).ToList();

            if (ret.EndType == DestType.Unknown) { ret.EndType = ret.CheckType(); }

            ret.exists = null;

            return ret;
        }
        public static PathBase PathAppend(PathBase path, string name, DestType type)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            else if (path.IsFile) throw new PathTypeException("Cannot append anything to a file path.");
            else if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name to append cannot be null or empty.");
            else if (type == DestType.Unknown) throw new PathTypeException("Cannot append a string with a DestType of unknown.");

            name = name.Trim();

            string[] s = path.Structure.SplitSeperator(name);
            List<string> ls;
            var ret = path.Clone();
            string fileName;
            string last = s[^1];

            if (type == DestType.File)
            {
                ls = s[..^1].ToList();                

                var m = extRegex.Match(s.Last());

                if (m.Success)
                {
                    if (ExtTemplate.CatchValidate(m.Groups[2].Value, out string ext) is StringValidationException extException)
                    {
                        throw new NameNotValidException($"File extension is not valid: '{ext}'. {extException.Message}");
                    }
                    else if (NameTemplate.CatchValidate(m.Groups[1].Value, out fileName) is StringValidationException nameException)
                    {
                        throw new NameNotValidException($"File name is not valid: '{fileName}'. {nameException.Message}");
                    }

                    ls.Add(fileName);
                    ret.Extension = ext;
                }
                else
                {
                    if (NameTemplate.CatchValidate(last, out fileName) is StringValidationException nameException)
                    {
                        throw new NameNotValidException($"File name is not valid: '{fileName}'. {nameException.Message}");
                    }

                    ls.Add(fileName);
                }
            }
            else
            {
                if (NameTemplate.CatchValidate(last, out fileName) is StringValidationException nameException)
                {
                    throw new NameNotValidException($"File or folder name is not valid: '{fileName}'. {nameException.Message}");
                }

                ls = s.ToList();
            }

            ret.pathElements = ret.pathElements.Concat(ls).ToList();
            ret.EndType = type;

            return ret;
        }
        public static string ParseFileLength(long byteLength, int sigFigs = 3)
        {
            if (byteLength == 0L) return "0B";
            else if (byteLength < 0L) throw new ArgumentException("File length cannot be less than zero.");

            int pow = (int)Math.Log(byteLength, 1024d);

            double factor = byteLength / Math.Pow(1024d, pow);

            return GetSigFigs(factor, sigFigs) + LengthUnits[pow];
        }
        public static double GetSigFigs(double value, int sigFigs)
        {
            if (value == 0d) return 0d;
            else if (sigFigs <= 0) throw new ArgumentException("Number of significant figure cannot be less than or equal to zero.");
            else if (double.IsNaN(value)) throw new ArgumentException("value is NaN.");
            else if (double.IsInfinity(value)) throw new ArgumentException("value cannot be +/- infinity.");

            bool negative = value < 0d;

            if (negative) { value = -value; }

            double factor = Math.Floor(Math.Log10(value));

            value /= Math.Pow(10d, factor);

            return Math.Round(value, sigFigs - 1) * Math.Pow(10d, factor) * (negative ? -1d : 1d);
        }
        
        public override string ToString() => Path;
        public override bool Equals(object obj)
        {
            return Equals(obj as PathBase);
        }
        public bool Equals(PathBase other)
        {
            return other != null &&
                   Structure.Equals(other.Structure) &&
                   pathElements.SequenceEqual(other.pathElements, StringComparer.Ordinal);
        }
        public override int GetHashCode()
        {
            int hashCode = -1553999267;
            hashCode = hashCode * -1521134295 + Structure.GetHashCode();
            hashCode = hashCode * -1521134295 + pathElements.CaseInsensitiveHash();
            return hashCode;
        }
        
        public static bool operator ==(PathBase left, PathBase right)
        {
            return EqualityComparer<PathBase>.Default.Equals(left, right);
        }
        public static bool operator !=(PathBase left, PathBase right)
        {
            return !(left == right);
        }
        
        public static string CleanPath(string path)
        {
            path = path.Replace(@"/", @"\")
                       .Replace(@"\\", @"\");

            if (path.EndsWith(":")) { return path + @"\"; }
            else { return path; }

        }
        public static string GetAccessString(AccessLevel access)
        {
            if (access == AccessLevel.None) return "None";

            List<string> s = new List<string>(6);

            AccessLevel mask;

            for (int x = 0; x <= 5; x++)
            {
                mask = (AccessLevel)(1 << x);

                if ((access & mask) == mask)
                {
                    s.Add(mask.ToString());
                }
            }

            return string.Join(", ", s);
        }

        public static PathBase FindCommandPath(PathBase workingDir, string pattern)
        {
            PathBase? x = null;

            try
            {
                x = (PathBase)workingDir.Structure.DefaultConstructor.Invoke(new object[2] { pattern, DestType.Unknown });
            }
            catch (TargetInvocationException e)
            {
                if (!(e.InnerException is PathException)) { throw e; }
            }

            if (x is PathBase a)
            {
                if (a.IsAbsolutePath) { return a; }
                else
                {
                    return workingDir.Append(pattern);
                }
            }

            if (pattern.StartsWith("../"))
            {
                if (workingDir.IsRootPath) { throw new PathException("It is not valid to use '..' to take the owner of a root path."); }
                workingDir = workingDir.GetContainingDir();
                pattern = pattern[3..];
            }
            else if (pattern.StartsWith("./"))
            {
                pattern = pattern[2..];
            }
            else if (pattern.Equals("."))
            {
                return workingDir;
            }
            else if (pattern.Equals(".."))
            {
                if (workingDir.IsRootPath) { throw new PathException("It is not valid to use '..' to take the owner of a root path."); }
                return workingDir.GetContainingDir();
            }

            PathException? pe = null;

            try
            {
                x = (PathBase)workingDir.Structure.DefaultConstructor.Invoke(new object[2] { pattern, DestType.Unknown });
            }
            catch (TargetInvocationException e)
            {
                if (e.InnerException is PathException ex) { pe = ex; }
                else { throw e; }
            }

            if (x is PathBase b)
            {
                return b;
            }
            else
            {
                throw pe ?? throw new Exception();
            }
        }
    }
}
