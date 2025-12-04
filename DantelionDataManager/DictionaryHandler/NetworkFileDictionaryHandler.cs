using DantelionDataManager.Log;
using SoulsFormats;

namespace DantelionDataManager.DictionaryHandler
{
    public class NetworkFileDictionaryHandler : FileDictionaryHandler
    {
        private const string BASE_URL = "https://raw.githubusercontent.com/JKAnderson/BinderKeys/refs/heads/main";
        private const string FOLDER = "Hash";
        private static Dictionary<BHD5.Game, string> _gameAlias = new()
        {
            { BHD5.Game.EldenRing, $"EldenRing_PC" },
            { BHD5.Game.Nightreign, $"EldenRingNightreign_PC" },
            { BHD5.Game.DarkSouls3, $"DarkSouls3_PC" },
            { BHD5.Game.Sekiro, $"Sekiro_PC" },
        };
        private BHD5.Game _g;
        private bool _updated = false;
        public string GameFolder => _gameAlias[_g];
        public string GameHashFolder => $"{BASE_URL}/{GameFolder}/{FOLDER}";


        public NetworkFileDictionaryHandler(string dictPath, BHD5.Game game, Dictionary<string, BHD5> master, IFileHash hashCalc) : base(dictPath, master, hashCalc)
        {
            _g = game;
            GetRemoteDictionary();
        }

        private void GetRemoteDictionary()
        {
            foreach (var kvp in _master)
            {
                string key = kvp.Key.Split('\\')[^1];
                var tempSet = new HashSet<string>();
                try
                {
                    using (var client = new HttpClient())
                    {
                        var url = $"{GameHashFolder}/{key}.txt";
                        _log.LogDebug(this, key, "Fetching dictionary from {a}", url);
                        var response = client.GetAsync(url).Result;
                        response.EnsureSuccessStatusCode();
                        var content = response.Content.ReadAsStringAsync().Result;
                        using (var sr = new StringReader(content))
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                tempSet.Add(line);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    _log.LogWarning(this, key, "Failed to fetch dictionary from remote for {a}", key);
                    continue;
                }
                

                if (FileDictionary[kvp.Key].Count < tempSet.Count)
                {
                    //log
                    _log.LogInfo(this, key, AnsiColor.Green("Updated dictionary for {a} with {b} entries (was {c})"), key, tempSet.Count, FileDictionary[kvp.Key].Count);
                    FileDictionary[kvp.Key] = tempSet;
                    _updated = true;
                }
            }

            if (_updated)
            {
                //log
                _log.LogInfo(this, "NetworkFileDictionaryHandler", AnsiColor.Green("One or more dictionaries were updated from remote. Saving combined dictionary."));
                SaveDictionary(_dictPath);
            }
        }
    }
}
