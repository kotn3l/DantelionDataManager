using DantelionDataManager.Log;
using Serilog;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace DantelionDataManager.Network
{
    public class RemoteDataManager
    {
        private const string BASE_URL = "https://raw.githubusercontent.com/JKAnderson/BinderKeys/refs/heads/main";
        private const string HASH_FOLDER = "Hash";
        private const string KEY_FOLDER = "Key";
        private readonly HttpClient _httpClient;
        private static readonly HttpClient _githubApi;

        private static ALogWrapper _log;
        private static readonly Dictionary<BHD5.Game, string> _gameAlias = new()
        {
            { BHD5.Game.EldenRing, $"EldenRing_PC" },
            { BHD5.Game.Nightreign, $"EldenRingNightreign_PC" },
            { BHD5.Game.DarkSouls3, $"DarkSouls3_PC" },
            { BHD5.Game.Sekiro, $"Sekiro_PC" },
            { BHD5.Game.ArmoredCore6, $"ArmoredCore6_PC" },
        };

        private readonly BHD5.Game _g;
        private readonly HashSet<string> _master;

        public string GameFolder => _gameAlias[_g];
        public string GameHashFolder => $"{BASE_URL}/{GameFolder}/{HASH_FOLDER}";
        public string GameKeyFolder => $"{BASE_URL}/{GameFolder}/{KEY_FOLDER}";

        public RemoteDataManager(BHD5.Game g, Dictionary<string, BHD5> master)
        {
            _log = LogWrapper.Get();
            _httpClient = new HttpClient();
            _g = g;
            _master = new HashSet<string>(master.Keys);
        }

        static RemoteDataManager()
        {
            _githubApi = new HttpClient
            {
                BaseAddress = new Uri("https://api.github.com/repos/JKAnderson/BinderKeys/contents/")
            };
            _githubApi.DefaultRequestHeaders.UserAgent.ParseAdd("DantelionDataManager");
        }

        public HashSet<string> GetAvailableDictionaries()
        {
            var list = GetRepoContentsAsync($"{GameFolder}/{HASH_FOLDER}").GetAwaiter().GetResult();
            return list.Where(x => x.type == "file").Select(x => x.name[..^4]).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        }

        private async Task<HashSet<GitHubItem>> GetRepoContentsAsync(string path = "", string branch = "main")
        {
            var url = $"{path}?ref={branch}";
            using var response = await _githubApi.GetAsync(url);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<HashSet<GitHubItem>>(json);
        }

        private async IAsyncEnumerable<string> GetContentFromUrl(string url)
        {
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                yield break;

            using var sr = new StringReader(await response.Content.ReadAsStringAsync());

            string line;
            while ((line = sr.ReadLine()) != null)
                yield return line;
        }

        public HashSet<string> GetRemoteDictionary(string key)
        {
            return GetContentFromUrl($"{GameHashFolder}/{key}.txt").ToBlockingEnumerable().ToHashSet();
        }

        public IEnumerable<string> GetRemoteKey(string key)
        {
            return GetContentFromUrl($"{GameKeyFolder}/{key}.txt").ToBlockingEnumerable();
        }

        public string GetMasterSimplified(string key)
        {
            return key.Split('\\')[^1];
        }
    }

    public class GitHubItem
    {
        public string name { get; set; }
        public string path { get; set; }
        public string type { get; set; } // "file" or "dir"
        public string download_url { get; set; }
    }
}
