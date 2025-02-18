using DotNext.IO.MemoryMappedFiles;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.OpenSsl;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;

namespace DantelionDataManager
{
    public class BHDCache : IDisposable
    {
        private const string _extension = ".bhdcache";

        public string OriginalPath { get; private set; }
        public string CachePath { get; private set; }
        public byte[] OriginalMD5 { get; private set; }
        public byte[] ReadMD5 { get; private set; }
        public bool IsValid { get; private set; }
        public Memory<byte> EncryptedBHD { get; private set; }
        public Memory<byte> DecryptedBHD { get; private set; }

        private byte[] _decrypt;
        private readonly MD5 _calc;
        private MemoryMappedFile _decryptedMMF;
        private IMappedMemory _decrpytedIMM;
        private readonly MemoryMappedFile _encryptedMMF;
        private readonly IMappedMemory _encryptedIMM;
        private readonly string _cacheDir;
        private readonly string _cacheName;

        public BHDCache(string encryptedLocation, string cacheLocation, string cacheName)
        {
            _calc = MD5.Create();
            OriginalPath = encryptedLocation;
            CachePath = Path.Combine(cacheLocation, cacheName + _extension);
            _cacheDir = cacheLocation;
            _cacheName = cacheName;
            _encryptedMMF = MemoryMappedFile.CreateFromFile(OriginalPath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            _encryptedIMM = _encryptedMMF.CreateMemoryAccessor(0, 0, MemoryMappedFileAccess.Read);
            EncryptedBHD = _encryptedIMM.Memory;
            OriginalMD5 = new byte[16];
            if (_calc.TryComputeHash(EncryptedBHD.Span, OriginalMD5, out _) && File.Exists(CachePath))
            {
                using var _md5MMF = MemoryMappedFile.CreateFromFile(CachePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                using var _md5IMM = _md5MMF.CreateMemoryAccessor(0, 16, MemoryMappedFileAccess.Read);
                ReadMD5 = _md5IMM.Memory.ToArray();
                IsValid = ReadMD5.SequenceEqual(OriginalMD5);

                if (IsValid)
                {
                    ReadDecrypted();
                    _encryptedMMF.Dispose();
                    _encryptedIMM.Dispose();
                }
            }
        }

        public void OverwriteCache(string key, bool keepOld = false)
        {
            if (!IsValid)
            {
                var BHDbytes = DecryptRsa(EncryptedBHD.Span, key);
                var bytes = BHDbytes.GetBuffer();
                if (File.Exists(CachePath))
                {
                    if (!keepOld)
                    {
                        File.Delete(CachePath);
                    }
                    else
                    {
                        var oldCache = Path.Combine(_cacheDir, _cacheName + "_" + string.Concat(ReadMD5[..4].Select(b => b.ToString("X2"))) + _extension);
                        File.Move(CachePath, oldCache);
                    }
                }
                Write(bytes);
            }
        }

        private void Write(byte[] decryptedBytes)
        {
            _decrypt = decryptedBytes;
            byte[] master = new byte[_decrypt.Length + OriginalMD5.Length];
            OriginalMD5.CopyTo(master, 0);
            _decrypt.CopyTo(master, OriginalMD5.Length);
            using FileStream stream = File.Create(CachePath);
            stream.Write(master, 0, master.Length);
            stream.Flush();
            stream.Dispose();
            ReadDecrypted();
            IsValid = true;
        }

        private static MemoryStream DecryptRsa(Span<byte> fileData, string key)
        {
            PemReader pemReader = new PemReader(new StringReader(key));
            AsymmetricKeyParameter keyParameter = (AsymmetricKeyParameter)pemReader.ReadObject();
            RsaEngine engine = new RsaEngine(); engine.Init(false, keyParameter);
            MemoryStream outputStream = new MemoryStream();
            int inputBlockSize = engine.GetInputBlockSize();
            int outputBlockSize = engine.GetOutputBlockSize();
            byte[] inputBlock = new byte[inputBlockSize];
            for (int i = 0; i < fileData.Length; i += inputBlockSize)
            {
                int remainingBytes = fileData.Length - i;
                int currentBlockSize = Math.Min(remainingBytes, inputBlockSize);
                ReadOnlySpan<byte> currentSpan = fileData.Slice(i, currentBlockSize);
                currentSpan.CopyTo(inputBlock);
                byte[] outputBlock = engine.ProcessBlock(inputBlock, 0, currentBlockSize);
                int requiredPadding = outputBlockSize - outputBlock.Length;
                if (requiredPadding > 0)
                {
                    byte[] paddedOutputBlock = new byte[outputBlockSize];
                    outputBlock.CopyTo(paddedOutputBlock, requiredPadding);
                    outputBlock = paddedOutputBlock;
                }
                outputStream.Write(outputBlock, 0, outputBlock.Length);
            }
            outputStream.Seek(0, SeekOrigin.Begin);
            return outputStream;
        }

        private void ReadDecrypted()
        {
            _decryptedMMF = MemoryMappedFile.CreateFromFile(CachePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            _decrpytedIMM = _decryptedMMF.CreateMemoryAccessor(16, 0, MemoryMappedFileAccess.Read);
            DecryptedBHD = _decrpytedIMM.Memory;
        }

        public void Dispose()
        {
            _calc.Dispose();
            _decrpytedIMM?.Dispose();
            _decryptedMMF?.Dispose();
            EncryptedBHD = null; OriginalMD5 = null; //_readMD5 = null;
            OriginalPath = null; CachePath = null;
            DecryptedBHD = null;
        }
    }
}
