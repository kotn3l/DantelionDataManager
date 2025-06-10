using SoulsFormats;

namespace DantelionDataManager.DictionaryHandler
{
    public class PreDictionaryHandler : FileDictionaryHandler
    {
        private readonly string _dictPath; 
        public PreDictionaryHandler(string genericPath, string dictPath, Dictionary<string, BHD5> master, IFileHash hashCalc) : base(genericPath, master, hashCalc)
        {
            _dictPath = dictPath;
        }

        protected override void ReadFileDictionaryCombined(string dictPath)
        {
            using (StreamReader sr = new StreamReader(dictPath))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Exists(line.Trim());
                }
            }
        }

        public override string WhichArchive(string relativePath)
        {
            string d = base.WhichArchive(relativePath);
            if (string.IsNullOrWhiteSpace(d))
            {
                ulong hash = _hashCalc.GetFilePathHash(relativePath);
                foreach (var kvp in _master)
                {
                    if (kvp.Value.MasterBucket.Any(x => x.FileNameHash == hash))
                    {
                        FileDictionary[kvp.Key].Add(relativePath);
                        return kvp.Key;
                    }
                }
                return string.Empty;
            }
            return d;
        }

        public override void Dispose()
        {
            SaveDictionary(_dictPath);
        }
    }
}
