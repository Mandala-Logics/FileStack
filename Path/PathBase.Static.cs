using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MandalaLogics.StringValidation;

namespace MandalaLogics.Path
{
    public abstract partial class PathBase
    {
        public static PathBase PathSubrtation(PathBase path1, PathBase path2)
        {
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
                        ret._pathElements = ret._pathElements.TakeLast(path1.Count - path2.Count).ToList();
                    }
                    else if (path1.EndsWith(path2))
                    {
                        ret._pathElements = ret._pathElements.Take(path1.Count - path2.Count).ToList();
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
                        ret._pathElements = ret._pathElements.Take(path1.Count - path2.Count).ToList();
                    }
                    else if (path1.StartsWith(path2))
                    {
                        ret._pathElements = ret._pathElements.TakeLast(path1.Count - path2.Count).ToList();
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
                    ret._pathElements = ret._pathElements.Take(path1.Count - path2.Count).ToList();
                }
                else if (path1.StartsWith(path2))
                {
                    ret._pathElements = ret._pathElements.TakeLast(path1.Count - path2.Count).ToList();
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
            if (path1 is null) throw new ArgumentNullException(nameof(path1));
            else if (path2 is null) throw new ArgumentNullException(nameof(path2));

            if (path2.IsAbsolutePath) throw new PathTypeException("Cannot append an absolute path, must be a relitive path.");
            else if (path1.IsFile) throw new PathTypeException("Cannot append anything to a file path.");

            var ret = path1.Clone();

            ret._pathElements = ret._pathElements.Concat(path2._pathElements).ToList();
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
            if (path1 is null) throw new ArgumentNullException(nameof(path1));
            else if (path2 is null) throw new ArgumentNullException(nameof(path2));

            if (path1.Count < path2.Count) return false;

            if (path1.IsAbsolutePath == path2.IsAbsolutePath)
            {
                return path1._pathElements.Take(path2.Count).SequenceEqual(path2._pathElements);
            }
            else
            {
                return false;
            }
        }
        
        public static bool PathEndsWith(PathBase path1, PathBase path2)
        {
            if (path1 is null) throw new ArgumentNullException(nameof(path1));
            else if (path2 is null) throw new ArgumentNullException(nameof(path2));

            if (path2.IsAbsolutePath) return false;
            else if (path1.Count < path2.Count) return false;

            return path1._pathElements.Skip(path1.Count - path2.Count).SequenceEqual(path2._pathElements);
        }
        public static PathBase PathSubpath(PathBase path, int start, int count)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));

            if (start < 0 || start >= path.Count) throw new ArgumentException($"Start index out of range: {start}");
            else if (count <= 0) throw new ArgumentException($"Count must be greater than zero: {count}");
            else if (count + start > path.Count) throw new ArgumentException($"The sume of count ({count}) and start ({start}) cannot be beyond the number of elements ({path.Count}).");

            int n = 0;
            int x = start;
            string[] s = new string[count];

            do
            {
                s[n] = path._pathElements[x]; 
                n++;
                x++;
            } while (n < count);

            var ret = path.Clone();

            ret._pathElements = s.ToList();

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
            if (path is null) throw new ArgumentNullException(nameof(path));

            if (path.Count == 1)
            {
                if (path.Structure.AllowOnlyStartToken && path.IsAbsolutePath)
                {
                    return path.GetRootPath();
                }
                else
                {
                    throw new PathException("Cannot get the containing path for a path with only one element.");
                }
            }

            var ret = path.Clone();

            ret._pathElements = ret._pathElements.Take(ret.Count - 1).ToList();
            ret.EndType = DestType.Dir;
            ret.Extension = null;

            return ret;
        }
        
        public static IEnumerable<PathBase> PathSiblings(PathBase path)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            else if (path.Count <= 1) throw new PathException("This path does not have siblings because it is a root path.");

            return path.GetContainingDir().Dir().Where((p) => !p.Equals(path));
        }
        
        public static PathBase PathAppend(PathBase path, string name)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
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

            var m = ExtRegex.Match(s.Last());

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

            ret._pathElements = ret._pathElements.Concat(ls).ToList();

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

                var m = ExtRegex.Match(s.Last());

                if (m.Success)
                {
                    if (ExtTemplate.CatchValidate(m.Groups[2].Value, out string ext) is { } extException)
                    {
                        throw new NameNotValidException($"File extension is not valid: '{ext}'. {extException.Message}");
                    }
                    else if (NameTemplate.CatchValidate(m.Groups[1].Value, out fileName) is { } nameException)
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

            ret._pathElements = ret._pathElements.Concat(ls).ToList();
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
        
        public static PathBase GetRandomFile(PathBase rootDir)
        {
            var dir = rootDir.Dir();

            var name = GetRandomHexString(16);

            while (dir.Any(pb => name.Equals(pb.EndPointName)))
            {
                name = GetRandomHexString(16);
            }

            return rootDir.Append(name, DestType.File);
        }

        public static PathBase CreateRandomFolder(PathBase rootDir)
        {
            var dir = rootDir.Dir();

            var name = GetRandomHexString(16);

            while (dir.Any(pb => name.Equals(pb.EndPointName)))
            {
                name = GetRandomHexString(16);
            }

            var ret = rootDir.Append(name, DestType.Dir);

            ret.CreateDirectory();

            return ret;
        }

        private static readonly List<char> HexChars = new List<char>()
            { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };

        public static string GetRandomHexString(int length)
        {
            if (length < 0) throw new ArgumentException("Length cannot be negative.");

            var sb = new StringBuilder();
            var rand = new Random();

            for (var x = 1; x <= length; x++)
            {
                sb.Append(HexChars[rand.Next(0, 15)]);
            }

            return sb.ToString();
        }
        
        public static bool operator ==(PathBase left, PathBase right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(PathBase left, PathBase right)
        {
            return !left.Equals(right);
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
                if (workingDir.IsRootPath) { throw new PathParsingException("It is not valid to use '..' to take the owner of a root path."); }
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
                if (workingDir.IsRootPath) { throw new PathParsingException("It is not valid to use '..' to take the owner of a root path."); }
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