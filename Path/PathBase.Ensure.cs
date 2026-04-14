using System.Collections.Generic;
using System.IO;

namespace MandalaLogics.Path
{
    public abstract partial class PathBase
    {
        public List<PathBase> GetAncestry()
        {
            if (IsRootPath) throw new PathException("Cannot get ancestry for a root path.");

            var ls = new List<PathBase>(_pathElements.Count - 1);

            var pb = this;

            do
            {
                pb = pb.GetContainingDir();
                
                ls.Add(pb);

            } while (!pb.IsRootPath);

            return ls;
        }
        
        public void EnsureDir()
        {
            if (!IsDir) throw new PathException("Path points to a file, not a dir.");

            if (Exists) return;

            var ans = GetAncestry();

            for (var x = ans.Count - 1; x >= 0; x--)
            {
                if (!ans[x].Exists)
                {
                    ans[x].CreateDirectory();
                }
            }

            CreateDirectory();
        }

        public Stream EnsureFile(FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            if (!IsFile) throw new PathException("Path points to a dir, not a file.");

            GetContainingDir().EnsureDir();

            return OpenStream(fileMode, fileAccess, fileShare);
        }

        public bool IsAccessOk(AccessLevel requiredAccess, AccessLevel calculatedAccess)
        {
            if (requiredAccess.HasFlag(AccessLevel.Write) && !calculatedAccess.HasFlag(AccessLevel.Write)) return false;
            
            if (requiredAccess.HasFlag(AccessLevel.Read) && !calculatedAccess.HasFlag(AccessLevel.Read)) return false;
            
            if (requiredAccess.HasFlag(AccessLevel.Delete) && !calculatedAccess.HasFlag(AccessLevel.Delete)) return false;

            return true;
        }

        public bool EnsureDirAccess(AccessLevel reqAccess)
        {
            if (!IsDir) throw new PathException("Path points to a file, not a dir.");

            var tree = Tree();

            foreach (var node in tree)
            {
                var treePath = node.Value;

                if (!IsAccessOk(reqAccess, treePath.Access)) return false;
            }

            return true;
        }
    }
}