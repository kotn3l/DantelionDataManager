using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DantelionDataManager
{
    public interface IFileHash
    {
        ulong GetFilePathHash(string path);
    }

    public class OldFileHash : IFileHash
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

    public class NewFileHash : IFileHash
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
