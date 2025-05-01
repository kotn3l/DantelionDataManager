using DantelionDataManager.Log;
using Serilog;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SoulsFormats.MSB3.Region;

namespace DantelionDataManager.DictionaryManager
{
    public abstract class BaseDictionaryManager
    {
        protected readonly Dictionary<string, BHD5> _master;
        protected readonly IFileHash _hashCalc;
        public readonly Dictionary<string, HashSet<string>> FileDictionary;

        protected BaseDictionaryManager(Dictionary<string, BHD5> master, IFileHash hashCalc)
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

    public class FileDictionaryManager : BaseDictionaryManager
    {

        public FileDictionaryManager(string dictPath, Dictionary<string, BHD5> master, IFileHash hashCalc) : base(master, hashCalc)
        {
            foreach (var kvp in master)
            {
                FileDictionary.Add(kvp.Key, new HashSet<string>());
            }

            ReadFileDictionaryCombined(dictPath);
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
            return false;
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
            return FileDictionary.Where(x => x.Value.Contains(relativePath)).Select(x => x.Key).FirstOrDefault();
        }
    }

    public class PreDictionaryManager : FileDictionaryManager
    {
        public PreDictionaryManager(string genericPath, Dictionary<string, BHD5> master, IFileHash hashCalc) : base(genericPath, master, hashCalc)
        {
            
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

        public override bool Exists(string relativePath)
        {
            ulong hash = _hashCalc.GetFilePathHash(relativePath);
            foreach (var kvp in _master)
            {
                if (kvp.Value.MasterBucket.Any(x => x.FileNameHash == hash))
                {
                    FileDictionary[kvp.Key].Add(relativePath);
                    return true;
                }
            }
            return false;
        }

        public override IEnumerable<string> GetMatchedFiles(string relativePath, string data, Regex regex)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<string> WhichArchive(string relativePath, Regex pattern)
        {
            return base.WhichArchive(relativePath, pattern); //cant search if we dont have dictionary
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
    }
}
