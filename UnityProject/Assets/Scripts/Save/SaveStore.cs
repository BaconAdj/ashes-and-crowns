using System;
using System.IO;

namespace AshesAndCrowns.Save
{
    public enum LoadSource : byte { None = 0, Main = 1, Backup = 2 }

    public sealed class LoadResult
    {
        public RunState State;
        public LoadSource Source;
        public string MainError;
        public string BackupError;
        public bool Found => State != null;
    }

    /// <summary>
    /// One hidden save per run. Writes go to a temp file, are flushed to disk, then renamed over the
    /// main file while the previous main becomes the rolling backup. A crash at any point leaves
    /// either the old main, the new main, or the backup intact. Load prefers main and falls back
    /// to backup. Delete removes all three synchronously.
    /// </summary>
    public sealed class SaveStore
    {
        public string Directory { get; }
        public string MainPath { get; }
        public string BackupPath { get; }
        public string TempPath { get; }

        public SaveStore(string directory, string baseName = "run")
        {
            Directory = directory ?? throw new ArgumentNullException(nameof(directory));
            MainPath = Path.Combine(directory, baseName + ".sav");
            BackupPath = Path.Combine(directory, baseName + ".bak");
            TempPath = Path.Combine(directory, baseName + ".tmp");
        }

        public bool Exists => File.Exists(MainPath) || File.Exists(BackupPath);

        public void Save(RunState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            System.IO.Directory.CreateDirectory(Directory);
            byte[] bytes = SaveCodec.Encode(state);
            using (var fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
            if (File.Exists(MainPath))
            {
                // Rotate: main -> backup, temp -> main. Two renames; each is atomic on its own.
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                File.Move(MainPath, BackupPath);
            }
            File.Move(TempPath, MainPath);
        }

        public LoadResult Load()
        {
            var result = new LoadResult();
            // A leftover temp file means a crash mid-write; it is never trusted.
            TryDelete(TempPath);
            if (File.Exists(MainPath))
            {
                try { result.State = SaveCodec.Decode(File.ReadAllBytes(MainPath)); result.Source = LoadSource.Main; return result; }
                catch (Exception e) { result.MainError = e.Message; }
            }
            else result.MainError = "no main save";
            if (File.Exists(BackupPath))
            {
                try { result.State = SaveCodec.Decode(File.ReadAllBytes(BackupPath)); result.Source = LoadSource.Backup; return result; }
                catch (Exception e) { result.BackupError = e.Message; }
            }
            else result.BackupError = "no backup";
            return result;
        }

        /// <summary>Permadeath. Returns true when nothing is left on disk.</summary>
        public bool DeleteAll()
        {
            TryDelete(TempPath); TryDelete(MainPath); TryDelete(BackupPath);
            return !File.Exists(TempPath) && !File.Exists(MainPath) && !File.Exists(BackupPath);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
