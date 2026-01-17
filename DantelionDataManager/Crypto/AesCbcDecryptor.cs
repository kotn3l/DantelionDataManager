using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace DantelionDataManager.Crypto
{
    public unsafe sealed class AesCbcDecryptor
    {
        // 14 rounds for AES-256 + 1 initial key
        private readonly Vector128<byte>* _roundKeys;
        // Pinned memory for keys
        private readonly byte[] _keyScheduleStorage;

        public AesCbcDecryptor(ReadOnlySpan<byte> key)
        {
            if (!Aes.IsSupported || !Sse2.IsSupported)
            {
                throw new PlatformNotSupportedException("AES-NI and SSE2 are required for this implementation.");
            }

            if (key.Length != 32)
            {
                throw new ArgumentException("Key size must be 256-bit (32 bytes).", nameof(key));
            }

            // Allocate pinned memory (15 keys * 16 bytes = 240 bytes)
            // We allocate extra to ensure we can manually align to 16 bytes if necessary
            int size = 15 * 16;
            _keyScheduleStorage = GC.AllocateArray<byte>(size + 16, pinned: true);

            // Manually align pointer to 16-byte boundary
            ref byte refData = ref MemoryMarshal.GetArrayDataReference(_keyScheduleStorage);
            nint address = (nint)Unsafe.AsPointer(ref refData);
            nint aligned = (address + 15) & ~15;

            _roundKeys = (Vector128<byte>*)aligned;

            ExpandKey256(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DecryptInPlace(Span<byte> data, ReadOnlySpan<byte> iv)
        {
            if (iv.Length != 16)
            {
                throw new ArgumentException("IV must be 128-bit (16 bytes).", nameof(iv));
            }

            fixed (byte* ptr = data)
            fixed (byte* ivPtr = iv)
            {
                // We use Unaligned loads for the IV/Data to be safe against arbitrary Span inputs
                DecryptBlocks(ptr, data.Length / 16, Sse2.LoadVector128(ivPtr));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void DecryptBlocks(byte* rawData, int blockCount, Vector128<byte> iv)
        {
            var keys = _roundKeys; // Local copy for performance

            // Process 8 blocks at a time to maximize pipeline throughput
            while (blockCount >= 8)
            {
                // Load ciphertexts
                // Using Unsafe.ReadUnaligned to support arbitrary alignment of input buffer
                var c0 = Unsafe.ReadUnaligned<Vector128<byte>>(rawData);
                var c1 = Unsafe.ReadUnaligned<Vector128<byte>>(rawData + 16);
                var c2 = Unsafe.ReadUnaligned<Vector128<byte>>(rawData + 32);
                var c3 = Unsafe.ReadUnaligned<Vector128<byte>>(rawData + 48);
                var c4 = Unsafe.ReadUnaligned<Vector128<byte>>(rawData + 64);
                var c5 = Unsafe.ReadUnaligned<Vector128<byte>>(rawData + 80);
                var c6 = Unsafe.ReadUnaligned<Vector128<byte>>(rawData + 96);
                var c7 = Unsafe.ReadUnaligned<Vector128<byte>>(rawData + 112);

                // Initialize working blocks with ciphertext
                var b0 = c0; var b1 = c1; var b2 = c2; var b3 = c3;
                var b4 = c4; var b5 = c5; var b6 = c6; var b7 = c7;

                // Initial Round (XOR with first decryption key)
                var k = keys[0];
                b0 = Sse2.Xor(b0, k);
                b1 = Sse2.Xor(b1, k);
                b2 = Sse2.Xor(b2, k);
                b3 = Sse2.Xor(b3, k);
                b4 = Sse2.Xor(b4, k);
                b5 = Sse2.Xor(b5, k);
                b6 = Sse2.Xor(b6, k);
                b7 = Sse2.Xor(b7, k);

                // Main Rounds 1-13 (AES-256 has 14 rounds)
                for (int i = 1; i < 14; i++)
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
                k = keys[14];
                b0 = Aes.DecryptLast(b0, k);
                b1 = Aes.DecryptLast(b1, k);
                b2 = Aes.DecryptLast(b2, k);
                b3 = Aes.DecryptLast(b3, k);
                b4 = Aes.DecryptLast(b4, k);
                b5 = Aes.DecryptLast(b5, k);
                b6 = Aes.DecryptLast(b6, k);
                b7 = Aes.DecryptLast(b7, k);

                // CBC Mode XOR Step: P[i] = D(C[i]) ^ C[i-1]
                // IV acts as C[-1]
                b0 = Sse2.Xor(b0, iv);
                b1 = Sse2.Xor(b1, c0);
                b2 = Sse2.Xor(b2, c1);
                b3 = Sse2.Xor(b3, c2);
                b4 = Sse2.Xor(b4, c3);
                b5 = Sse2.Xor(b5, c4);
                b6 = Sse2.Xor(b6, c5);
                b7 = Sse2.Xor(b7, c6);

                // Prepare IV for next batch (last ciphertext block)
                iv = c7;

                // Store plaintext in-place
                Unsafe.WriteUnaligned(rawData, b0);
                Unsafe.WriteUnaligned(rawData + 16, b1);
                Unsafe.WriteUnaligned(rawData + 32, b2);
                Unsafe.WriteUnaligned(rawData + 48, b3);
                Unsafe.WriteUnaligned(rawData + 64, b4);
                Unsafe.WriteUnaligned(rawData + 80, b5);
                Unsafe.WriteUnaligned(rawData + 96, b6);
                Unsafe.WriteUnaligned(rawData + 112, b7);

                rawData += 128;
                blockCount -= 8;
            }

            // Process remaining blocks individually
            while (blockCount > 0)
            {
                var c = Unsafe.ReadUnaligned<Vector128<byte>>(rawData);
                var b = Sse2.Xor(c, keys[0]);

                for (int i = 1; i < 14; i++)
                {
                    b = Aes.Decrypt(b, keys[i]);
                }

                b = Aes.DecryptLast(b, keys[14]);
                b = Sse2.Xor(b, iv); // XOR with IV (previous ciphertext)

                Unsafe.WriteUnaligned(rawData, b);

                iv = c; // Update IV to current ciphertext for next block
                rawData += 16;
                blockCount--;
            }
        }

        private void ExpandKey256(ReadOnlySpan<byte> key)
        {
            // AES-256 Key Expansion for Decryption (Equivalent Inverse Cipher)
            
            // 1. Generate Encryption Round Keys (15 keys total)
            Vector128<byte>* encKeys = stackalloc Vector128<byte>[15];
            
            fixed (byte* kPtr = key)
            {
                encKeys[0] = Sse2.LoadVector128(kPtr);
                encKeys[1] = Sse2.LoadVector128(kPtr + 16);
            }

            Vector128<byte> temp = encKeys[1];
            Vector128<byte> prev = encKeys[0];
            byte rcon = 0x01;

            for (int i = 1; i <= 6; i++)
            {
                // Even Keys: Standard expansion with RCON (generates 1st half of 256-bit block)
                var assist = Aes.KeygenAssist(temp, rcon);
                var kEven = ExpandStep(prev, assist);
                encKeys[2 * i] = kEven;

                // Update RCON for next loop
                if (rcon == 0x80) rcon = 0x1B; else rcon = (byte)(rcon << 1);

                // Odd Keys: SubWord with no RCON/RotWord (generates 2nd half of 256-bit block)
                // Need to apply SubWord to the last column of the new kEven.
                // We broadcast the last word (index 3) of kEven
                var t = Sse2.Shuffle(kEven.AsInt32(), 0xFF).AsByte();
                // EncryptLast with Key=0 does SubBytes + ShiftRows. Since all words are same, ShiftRows is no-op.
                // So this effectively computes SubWord(lastColumn) broadcasted.
                var subVector = Aes.EncryptLast(t, Vector128<byte>.Zero); 
                
                // IMPORTANT: Odd keys in AES-256 depend on the previous Odd key (temp)
                var kOdd = ExpandStep(temp, subVector);
                encKeys[2 * i + 1] = kOdd;

                prev = kEven;
                temp = kOdd;
            }

            // Final Key (14) - RCON was updated to 0x40 in the last loop
            var lastAssist = Aes.KeygenAssist(temp, rcon);
            encKeys[14] = ExpandStep(prev, lastAssist);

            // 2. Convert to Decryption Round Keys
            // The first decryption key is the last encryption key
            _roundKeys[0] = encKeys[14];

            // The middle keys are the InverseMixColumns of the encryption keys (reverse order)
            for (int i = 1; i < 14; i++)
            {
                _roundKeys[i] = Aes.InverseMixColumns(encKeys[14 - i]);
            }

            // The last decryption key is the first encryption key
            _roundKeys[14] = encKeys[0];
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