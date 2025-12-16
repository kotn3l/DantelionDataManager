using DantelionDataManager.Log;
using DantelionDataManager.Network;
using SoulsFormats;

namespace DantelionDataManager.DictionaryHandler
{
    public class NetworkFileDictionaryHandler : FileDictionaryHandler
    {
        private readonly BHD5.Game _g;
        private readonly RemoteDataManager _remote;
        private bool _updated = false;

        public NetworkFileDictionaryHandler(string dictPath, BHD5.Game game, Dictionary<string, BHD5> master, IFileHash hashCalc, RemoteDataManager remote) : base(dictPath, master, hashCalc)
        {
            _remote = remote;
            _g = game;
            GetRemoteDictionary();
        }

        private void GetRemoteDictionary()
        {
            CalculateHashes();
            var dicts = _remote.GetAvailableDictionaries();

            foreach (var kvp in _master)
            {
                var array = new HashSet<ulong>(_master[kvp.Key].MasterBucket.Select(y => y.FileNameHash));
                int actual = array.Count(_calculatedHashes[kvp.Key].Contains);
                if (array.Count <= actual)
                {
                    _log.LogDebug(this, kvp.Key, "Dictionary already up-to-date.");
                    continue;
                }

                string key = _remote.GetMasterSimplified(kvp.Key);
                if (dicts.TryGetValue(key, out string dictKey))
                {
                    continue;
                }

                var tempSet = _remote.GetRemoteDictionary(dictKey);
                if (FileDictionary[kvp.Key].Count < tempSet.Count)
                {
                    //log
                    _log.LogInfo(this, key, AnsiColor.Green("Updated dictionary for {a} with +{c} entries."), key, tempSet.Count - FileDictionary[kvp.Key].Count);
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
