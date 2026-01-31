using System.IO;

namespace MandalaLogics.Stacking
{
    public class FileStackEntry
    {
        internal uint BulkId => _info.BulkId;
        public uint LevelId => _info.LevelId;
        public string FileId => _info.FileId;
        
        private readonly StackedFileInfo _info;
        private readonly FileStack.LevelHandle _owner;

        internal FileStackEntry(StackedFileInfo info, FileStack.LevelHandle owner)
        {
            _info = info;
            _owner = owner;
        }

        public void RecoverFile(Stream stream)
        {
            _owner.RecoverFile(BulkId, stream);
        }
    }
}