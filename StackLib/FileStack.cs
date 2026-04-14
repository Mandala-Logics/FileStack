using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MandalaLogics.Packing;
using MandalaLogics.Encoding;
using MandalaLogics.Database;
using MandalaLogics.Path;
using System.Threading;
using MandalaLogics.Threading;

namespace MandalaLogics.Stacking
{
    public partial class FileStack
    {
        private const uint BulkDbStrand = 2U;
        private const uint LevelDbStrand = 3U;
        
        private readonly Braid _data;
        private readonly Splice<BulkDataInfo> _bulkDb;
        private readonly Splice<LevelInfo> _levelDb;
        private readonly HeaderFile<StackHeader> _header;

        private readonly BulkHandler _bulkHandler;
        private readonly LevelHandler _levelHandler;

        public int LevelCount => _levelDb.Count;

        public bool Disposed => _data.Disposed;

        static FileStack()
        {
            EncodingRegister.RegisterAll(Assembly.GetAssembly(typeof(FileStack)));
        }

        public FileStack(Stream stream)
        {
            var newStack = false;

            try
            {
                _data = new Braid(stream);

                Braid.BraidStrand strand;

                if (_data.Count == 0)
                {
                    newStack = true;

                    strand = _data.CreateStrand(new StackHeader(2));
                    strand.Seek(0L, SeekOrigin.Begin);
                }
                else
                {
                    strand = _data.GetStrand(1);
                }

                _header = new HeaderFile<StackHeader>(strand);

                if (newStack)
                {
                    _bulkDb = new Splice<BulkDataInfo>(_data.CreateStrand());
                    _levelDb = new Splice<LevelInfo>(_data.CreateStrand());
                }
                else
                {
                    _bulkDb = new Splice<BulkDataInfo>(_data.GetStrand(BulkDbStrand));
                    _levelDb = new Splice<LevelInfo>(_data.GetStrand(LevelDbStrand));
                }
            }
            catch (Exception e) when (e is BraidNotValidException || e is HeaderFileNotValidException)
            {
                throw new StackNotValidException("Header could not be read.", e);
            }
            catch (SpliceNotValidException e)
            {
                throw new StackNotValidException("Data tables could not be read.", e);
            }

            _bulkHandler = new BulkHandler(this);
            _levelHandler = new LevelHandler(this);
        }

        public uint CreateLevel(EncodedValue? metadata)
        {
            return _levelHandler.CreateLevel(metadata);
        }

        public LevelInfo[] GetLevels() => _levelDb.ToArray();

        public LevelHandle GetLevel(uint id)
        {
            return _levelHandler.GetLevel(id);
        }

        public ThreadBase CreateLevelFromFolder(PathBase root, CancellationToken cancellationToken, EncodedValue? metadata)
        {
            var thread = new TaskThread<PathBase, EncodedValue?>(DoCreateLevelFromFolder, root, metadata);
            thread.Start(cancellationToken);

            return thread;
        }

        public void DoCreateLevelFromFolder(ThreadController tc, PathBase root, EncodedValue? metadata)
        {
            tc.Progress.Report("Enumerating files.");
            
            var tree = root.Tree();
            
            tc.Progress.Report("Stacking files.");
            
            var files = new List<PathBase>();

            foreach (var otn in tree)
            {
                var path = otn.Value;

                if (tc.IsAbortRequested) return;

                if (path.IsFile)
                {
                    files.Add(path);
                }
            }

            if (files.Count == 0)
            {
                tc.Progress.Report("No files in this dir, level not created.");
                tc.Return(0);
                return;
            }
            
            tc.Progress.SetMax(files.Count);

            var levelId = _levelHandler.CreateLevel(metadata);
            using var handle = _levelHandler.GetLevel(levelId);

            var n = 0;

            foreach (var file in files)
            {
                if (tc.IsAbortRequested) return;

                n++;
                
                try
                {
                    using var stream = file.OpenStream(FileMode.Open, FileAccess.Read, FileShare.None);

                    if (stream.Length == 0)
                    {
                        tc.Progress.Report($"Unable to stack file {file.Path}: file is empty.", n);
                        continue;
                    }
                    else
                    {
                        handle.Add(file.Path, stream);
                    }
                }
                catch (PathException e)
                {
                    tc.Progress.Report($"Unable to stack file {file.Path}: {e.Message}", n);
                    continue;
                }
                
                tc.Progress.Report($"Stacked file {file.Path}", n);
            }
            
            tc.Return(levelId);
        }

        public void Dispose()
        {
            _bulkDb.Dispose();
            _levelDb.Dispose();
            _header.Dispose();
            
            _data.Dispose();
        }
    }
}