namespace DantelionDataManager
{
    public interface IFileHash
    {
        ulong GetFilePathHash(string path);
    }

    public class FileHash32 : IFileHash
    {
        public ulong GetFilePathHash(string path)
        {
            const uint prime = 37u;
            uint hash = 0u;
            unchecked
            {
                for (int i = 0; i < path.Length; i++)
                {
                    hash = hash * prime + path[i];
                }
            }
            return hash;
        }
    }

    public class FileHash64 : IFileHash
    {
        public ulong GetFilePathHash(string path)
        {
            const ulong prime = 0x85ul;
            ulong hash = 0u;
            unchecked
            {
                for (int i = 0; i < path.Length; i++)
                {
                    hash = hash * prime + (ulong)path[i];
                }
            }
            return hash;
        }
    }
}
