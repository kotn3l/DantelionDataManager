using SoulsFormats;

namespace DantelionDataManager
{
    public class GameFile
    {
        public string Path { get; private set; }
        public Memory<byte> Bytes { get; private set; }
        public ISoulsFile Data { get; set; }

        public GameFile(string path, Memory<byte> bytes)
        {
            Path = path;
            Bytes = bytes;
        }

        public GameFile(string path, Memory<byte> bytes, Func<Memory<byte>, ISoulsFile> load) : this(path, bytes)
        {
            Load(load);
        }

        public void Load(Func<Memory<byte>, ISoulsFile> load)
        {
            Data = load(Bytes);
        }

        public void Write(GameData data)
        {
            Bytes = Data?.Write();
            data.SetMem(Path, Bytes);
        }
    }
}
