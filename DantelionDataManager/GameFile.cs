using DotNext.IO.MemoryMappedFiles;
using SoulsFormats;

namespace DantelionDataManager
{
    public class GameFile : IDisposable
    {
        public readonly IMappedMemory MappedMemory;
        public string Path { get; private set; }
        public Memory<byte> Bytes { get; private set; }
        public ISoulsFile Data { get; set; }

        public GameFile(string path, IMappedMemory bytes)
        {
            Path = path;
            MappedMemory = bytes;
            Bytes = bytes.Memory;
        }

        public GameFile(string path, Memory<byte> bytes)
        {
            Path = path;
            Bytes = bytes;
        }

        public GameFile(string path, IMappedMemory bytes, Func<IMappedMemory, ISoulsFile> load) : this(path, bytes)
        {
            Load(load);
        }

        public void Load(Func<IMappedMemory, ISoulsFile> load)
        {
            Data = load(MappedMemory);
            MappedMemory.Dispose();
            Bytes = null;
        }

        public void Load(Func<Memory<byte>, ISoulsFile> load)
        {
            Data = load(Bytes);
            MappedMemory.Dispose();
            Bytes = null;
        }

        public void Write(GameData data)
        {
            data.Set(Path, Data?.Write());
        }

        public void Dispose()
        {
            MappedMemory?.Dispose();
            Bytes = null;
            Data = null;
            GC.SuppressFinalize(this);
        }
    }
}
