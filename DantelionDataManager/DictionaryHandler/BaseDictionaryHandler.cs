using SoulsFormats;
using System.Text.RegularExpressions;

namespace DantelionDataManager.DictionaryHandler
{
    public abstract class BaseDictionaryHandler
    {
        protected readonly Dictionary<string, BHD5> _master;
        protected readonly IFileHash _hashCalc;
        public readonly Dictionary<string, HashSet<string>> FileDictionary;

        protected BaseDictionaryHandler(Dictionary<string, BHD5> master, IFileHash hashCalc)
        {
            _master = master;
            _hashCalc = hashCalc;
            FileDictionary = new Dictionary<string, HashSet<string>>();
        }

        public abstract IEnumerable<string> WhichArchive(string relativePath, Regex pattern);
        public abstract string WhichArchive(string relativePath);
        public abstract bool Exists(string relativePath);
        public abstract IEnumerable<string> GetMatchedFiles(string relativePath, string data, Regex regex);
    }
}
