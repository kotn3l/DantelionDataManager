using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace DantelionDataManager.Crypto
{
    public unsafe sealed class AesEcbDecryptor
    {
        // 10 rounds for AES-128 + 1 initial key
        private readonly Vector128<byte>* _roundKeys;
        // Memory to hold the aligned keys
        private readonly byte[] _keyScheduleStorage;

        public AesEcbDecryptor(ReadOnlySpan<byte> key)
        {
            if (!Aes.IsSupported || !Sse2.IsSupported)
            {
                throw new PlatformNotSupportedException("AES-NI and SSE2 are required for this implementation.");
            }

            if (key.Length != 16)
            {
                throw new ArgumentException("Key size must be 128-bit (16 bytes).", nameof(key));
            }

            // Allocate pinned memory for keys to ensure alignment and prevent GC movement issues if accessed via pointers
            _keyScheduleStorage = GC.AllocateArray<byte>(11 * 16, pinned: true);
            _roundKeys = (Vector128<byte>*)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(_keyScheduleStorage));

            ExpandKey(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DecryptInPlace(Span<byte> data)
        {
            fixed (byte* ptr = data)
            {
                DecryptBlocks(ptr, data.Length / 16);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void DecryptBlocks(byte* rawData, int blockCount)
        {
            var keys = _roundKeys; // Local copy for performance

            // Process 8 blocks at a time to maximize pipeline throughput
            while (blockCount >= 8)
            {
                var b0 = Sse2.LoadVector128(rawData);
                var b1 = Sse2.LoadVector128(rawData + 16);
                var b2 = Sse2.LoadVector128(rawData + 32);
                var b3 = Sse2.LoadVector128(rawData + 48);
                var b4 = Sse2.LoadVector128(rawData + 64);
                var b5 = Sse2.LoadVector128(rawData + 80);
                var b6 = Sse2.LoadVector128(rawData + 96);
                var b7 = Sse2.LoadVector128(rawData + 112);

                // Initial Round (XOR with first decryption key - which is last encryption key)
                var k = keys[0];
                b0 = Sse2.Xor(b0, k);
                b1 = Sse2.Xor(b1, k);
                b2 = Sse2.Xor(b2, k);
                b3 = Sse2.Xor(b3, k);
                b4 = Sse2.Xor(b4, k);
                b5 = Sse2.Xor(b5, k);
                b6 = Sse2.Xor(b6, k);
                b7 = Sse2.Xor(b7, k);

                // Main Rounds 1-9
                for (int i = 1; i < 10; i++)
                {
                    k = keys[i];
                    b0 = Aes.Decrypt(b0, k);
                    b1 = Aes.Decrypt(b1, k);
                    b2 = Aes.Decrypt(b2, k);
                    b3 = Aes.Decrypt(b3, k);
                    b4 = Aes.Decrypt(b4, k);
                    b5 = Aes.Decrypt(b5, k);
                    b6 = Aes.Decrypt(b6, k);
                    b7 = Aes.Decrypt(b7, k);
                }

                // Final Round
                k = keys[10];
                b0 = Aes.DecryptLast(b0, k);
                b1 = Aes.DecryptLast(b1, k);
                b2 = Aes.DecryptLast(b2, k);
                b3 = Aes.DecryptLast(b3, k);
                b4 = Aes.DecryptLast(b4, k);
                b5 = Aes.DecryptLast(b5, k);
                b6 = Aes.DecryptLast(b6, k);
                b7 = Aes.DecryptLast(b7, k);

                Sse2.Store(rawData, b0);
                Sse2.Store(rawData + 16, b1);
                Sse2.Store(rawData + 32, b2);
                Sse2.Store(rawData + 48, b3);
                Sse2.Store(rawData + 64, b4);
                Sse2.Store(rawData + 80, b5);
                Sse2.Store(rawData + 96, b6);
                Sse2.Store(rawData + 112, b7);

                rawData += 128;
                blockCount -= 8;
            }

            // Process remaining blocks individually
            while (blockCount > 0)
            {
                var b = Sse2.LoadVector128(rawData);

                b = Sse2.Xor(b, keys[0]);

                for (int i = 1; i < 10; i++)
                {
                    b = Aes.Decrypt(b, keys[i]);
                }

                b = Aes.DecryptLast(b, keys[10]);

                Sse2.Store(rawData, b);
                rawData += 16;
                blockCount--;
            }
        }

        private void ExpandKey(ReadOnlySpan<byte> key)
        {
            // AES-128 Key Expansion for Decryption (Equivalent Inverse Cipher)
            
            // 1. Generate Encryption Round Keys
            Vector128<byte>* encKeys = stackalloc Vector128<byte>[11];
            
            fixed (byte* kPtr = key)
            {
                encKeys[0] = Sse2.LoadVector128(kPtr);
            }

            encKeys[1] = ExpandStep(encKeys[0], Aes.KeygenAssist(encKeys[0], 0x01));
            encKeys[2] = ExpandStep(encKeys[1], Aes.KeygenAssist(encKeys[1], 0x02));
            encKeys[3] = ExpandStep(encKeys[2], Aes.KeygenAssist(encKeys[2], 0x04));
            encKeys[4] = ExpandStep(encKeys[3], Aes.KeygenAssist(encKeys[3], 0x08));
            encKeys[5] = ExpandStep(encKeys[4], Aes.KeygenAssist(encKeys[4], 0x10));
            encKeys[6] = ExpandStep(encKeys[5], Aes.KeygenAssist(encKeys[5], 0x20));
            encKeys[7] = ExpandStep(encKeys[6], Aes.KeygenAssist(encKeys[6], 0x40));
            encKeys[8] = ExpandStep(encKeys[7], Aes.KeygenAssist(encKeys[7], 0x80));
            encKeys[9] = ExpandStep(encKeys[8], Aes.KeygenAssist(encKeys[8], 0x1B));
            encKeys[10] = ExpandStep(encKeys[9], Aes.KeygenAssist(encKeys[9], 0x36));

            // 2. Convert to Decryption Round Keys
            // The first decryption key is the last encryption key
            _roundKeys[0] = encKeys[10];

            // The middle keys are the InverseMixColumns of the encryption keys (reverse order)
            for (int i = 1; i < 10; i++)
            {
                _roundKeys[i] = Aes.InverseMixColumns(encKeys[10 - i]);
            }

            // The last decryption key is the first encryption key
            _roundKeys[10] = encKeys[0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> ExpandStep(Vector128<byte> key, Vector128<byte> assist)
        {
            var s = Sse2.Shuffle(assist.AsInt32(), 0xFF).AsByte();
            var t = Sse2.ShiftLeftLogical128BitLane(key, 4);
            key = Sse2.Xor(key, t);
            t = Sse2.ShiftLeftLogical128BitLane(t, 4);
            key = Sse2.Xor(key, t);
            t = Sse2.ShiftLeftLogical128BitLane(t, 4);
            key = Sse2.Xor(key, t);
            return Sse2.Xor(key, s);
        }
    }
}