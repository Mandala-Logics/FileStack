using System;

namespace MandalaLogics.Path
{
    public delegate void PathChangedEventHandler(object sender, PathChangedEventArgs args);

    public enum PathChangeType
    {
        None = 0,
        ChildMask = 0b10_1000,
        ChildRenamed = ChildMask | Renamed,
        ChildDeleted = ChildMask | Deleted,
        ChildCreated = ChildMask | Created,
        ChildChanged = ChildMask | Changed,
        ChildDirectoryChanged = ChildMask | DirectoryChanged,        
        Renamed = 0b1,
        Deleted = 0b10,
        Created = 0b100,
        Changed = 0b1000,
        DirectoryChanged = 0b1_0000
    }

    public sealed class PathChangedEventArgs : EventArgs
    {
        public PathChangeType Type { get; }
        public PathBase Path { get; }

        public PathChangedEventArgs(PathChangeType type, PathBase path)
        {
            Type = type;
            Path = path ?? throw new ArgumentNullException("path");
        }
    }
}
