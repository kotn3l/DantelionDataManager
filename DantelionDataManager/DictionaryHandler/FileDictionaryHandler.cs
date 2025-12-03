using SoulsFormats;
using System.Text.RegularExpressions;

namespace DantelionDataManager.DictionaryHandler
{
    public class FileDictionaryHandler : BaseDictionaryHandler
    {
        protected string _dictPath;
        public FileDictionaryHandler(string dictPath, Dictionary<string, BHD5> master, IFileHash hashCalc) : base(master, hashCalc)
        {
            foreach (var kvp in master)
            {
                FileDictionary.Add(kvp.Key, new HashSet<string>());
            }

            ReadFileDictionaryCombined(dictPath);

            _dictPath = dictPath;
        }

        public void SaveDictionary(string dictPath)
        {
            using (var sw = new StreamWriter(dictPath))
            {
                foreach (var kvp in FileDictionary)
                {
                    sw.WriteLine($"#{kvp.Key}");
                    foreach (var line in kvp.Value)
                    {
                        sw.WriteLine(line);
                    }
                }
            }
        }

        protected virtual void ReadFileDictionaryCombined(string dictPath)
        {
            using (StreamReader sr = new StreamReader(dictPath))
            {
                string line = sr.ReadLine();
                while (line != null)
                {
                    while (line == "" || line[0] != '#')
                    {
                        line = sr.ReadLine();
                    }
                    string k = line[1..].ToLowerInvariant();
                    while ((line = sr.ReadLine()) != null && line != "" && line[0] != '#')
                    {
                        FileDictionary[k].Add(line.Trim());
                    }
                }
            }
        }

        public override bool Exists(string relativePath)
        {
            foreach (var kvp in FileDictionary)
            {
                if (kvp.Value.Contains(relativePath))
                {
                    return true;
                }
            }
            return ExistsInMaster(relativePath);
        }

        public override IEnumerable<string> GetMatchedFiles(string relativePath, string data, Regex regex)
        {
            return FileDictionary[data].Where(s => s.StartsWith(relativePath) && regex.IsMatch(Path.GetFileName(s)));
        }

        public override IEnumerable<string> WhichArchive(string relativePath, Regex pattern)
        {
            foreach (var kvp in FileDictionary)
            {
                if (kvp.Value.Any(x => x.StartsWith(relativePath) && pattern.IsMatch(Path.GetFileName(x))))
                {
                    yield return kvp.Key;
                }
            }
        }

        public override string WhichArchive(string relativePath)
        {
            var key = FileDictionary.Where(x => x.Value.Contains(relativePath)).Select(x => x.Key).FirstOrDefault();
            if (key == null)
            {
                return ExistsInWhichMaster(relativePath);
            }
            return key;
        }
    }
}
