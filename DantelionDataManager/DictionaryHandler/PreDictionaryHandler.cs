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

        public void GuessChrs()
        {
            for (int i = 0; i <= 9999; i++)
            {
                string chrId = $"c{i:D4}";
                if (ExistsInMaster($"/chr/{chrId}.chrbnd.dcx"))
                {
                    ExistsInMaster($"/chr/{chrId}_h.texbnd.dcx");
                    ExistsInMaster($"/chr/{chrId}_l.texbnd.dcx");
                    ExistsInMaster($"/chr/{chrId}.texbnd.dcx");
                    ExistsInMaster($"/chr/{chrId}.behbnd.dcx");
                    ExistsInMaster($"/chr/{chrId}.anibnd.dcx");
                }
            }

            for (int i = 0; i <= 9999; i++)
            {
                ExistsInMaster($"/parts/am_m_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/am_f_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/am_a_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/bd_m_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/bd_f_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/bd_a_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/hd_m_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/hd_f_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/hd_a_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/lg_m_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/lg_f_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/lg_a_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/wp_m_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/wp_f_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/wp_a_{i:D4}.partsbnd.dcx");
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
