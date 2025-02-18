using System.Text.RegularExpressions;

namespace DantelionDataManager.Extensions
{
    public static partial class IOExtensions
    {
        public static void WriteText(this string s, string path)
        {
            using StreamWriter writer = new StreamWriter(path);
            writer.Write(s);
            writer.Flush();
        }
        public static void WriteBytes(this Span<byte> data, string s)
        {
            using FileStream stream = File.Create(s);
            stream.Write(data);
            stream.Flush();
        }
        public static string WriteBytes(this byte[] data, string s)
        {
            using FileStream stream = File.Create(s);
            stream.Write(data, 0, data.Length);
            stream.Flush();
            return s;
        }
        public static string WriteBytes(this MemoryStream data, string s)
        {
            using FileStream stream = File.Create(s);
            data.WriteTo(stream);
            //stream.Write(data, 0, data.Length);
            stream.Flush();
            return s;
        }
        public static byte[] ParseHexString(string str)
        {
            string[] strings = str.Split(' ');
            byte[] bytes = new byte[strings.Length];
            for (int i = 0; i < strings.Length; i++)
                bytes[i] = Convert.ToByte(strings[i], 16);
            return bytes;
        }
        public static Stream ToStream(this byte[] bytes)
        {
            return new MemoryStream(bytes)
            {
                //Position = 0
            };
        }
        public static void WriteToStream(Stream s, byte[] bytes)
        {
            using (var writer = new BinaryWriter(s))
            {
                writer.Write(bytes);
            }
        }
        public static void CheckDir(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        public static string ReadEither(string uncompressedLocation)
        {
            string s = uncompressedLocation;
            if (!File.Exists(uncompressedLocation) && !uncompressedLocation.EndsWith(".dcx"))
            {
                s += ".dcx";
            }
            return s;
        }
        public static string GetFileNameWithoutExtensions(string path)
        {
            path ??= string.Empty;
            return FileNameWithoutExtensions().Match(path).Groups[1].Value; //"^.*[\\\\/]([^\\\\/.]+)"
        }
        public static string GetFileExtensions(string path)
        {
            return FileExtensions().Match(path).Value;
        }
        public static bool IsExtension(string path, string extension)
        {
            return path.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase);
        }

        [GeneratedRegex("^(?:.*[\\\\/])?([^\\\\/.]+)")]
        private static partial Regex FileNameWithoutExtensions();

        [GeneratedRegex(@"\..*")]
        private static partial Regex FileExtensions();
    }
}
