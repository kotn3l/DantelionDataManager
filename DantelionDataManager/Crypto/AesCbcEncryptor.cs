using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Aes = System.Runtime.Intrinsics.X86.Aes;

namespace DantelionDataManager.Crypto
{
    public unsafe sealed class AesCbcEncryptor
    {
        // 14 rounds for AES-256 + 1 initial key
        private readonly Vector128<byte>* _roundKeys;
        // Pinned memory for keys
        private readonly byte[] _keyScheduleStorage;

        public AesCbcEncryptor(ReadOnlySpan<byte> key)
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
            // Use extra space for manual 16-byte alignment
            _keyScheduleStorage = GC.AllocateArray<byte>(15 * 16 + 16, pinned: true);

            ref byte refData = ref MemoryMarshal.GetArrayDataReference(_keyScheduleStorage);
            nint address = (nint)Unsafe.AsPointer(ref refData);
            nint aligned = (address + 15) & ~15;

            _roundKeys = (Vector128<byte>*)aligned;

            ExpandKey256(key);
        }

        public int GetCiphertextLength(int plainLength)
        {
            // PKCS7 padding always adds between 1 and 16 bytes to reach next block boundary
            return (plainLength / 16 + 1) * 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encrypt(ReadOnlySpan<byte> plaintext, Span<byte> output, ReadOnlySpan<byte> iv)
        {
            if (iv.Length != 16) throw new ArgumentException("IV must be 16 bytes.", nameof(iv));
            
            int paddedLength = GetCiphertextLength(plaintext.Length);
            if (output.Length < paddedLength) throw new ArgumentException("Output buffer too small.", nameof(output));

            fixed (byte* ptPtr = plaintext)
            fixed (byte* outPtr = output)
            fixed (byte* ivPtr = iv)
            {
                EncryptBlocks(ptPtr, plaintext.Length, outPtr, Sse2.LoadVector128(ivPtr));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void EncryptBlocks(byte* ptBase, int ptLen, byte* ctBase, Vector128<byte> iv)
        {
            var keys = _roundKeys;
            Vector128<byte> feedback = iv;
            Vector128<byte> block;

            int fullBlocks = ptLen / 16;
            
            // CBC Encryption must be serial: C[i] = Enc(P[i] ^ C[i-1])
            for (int i = 0; i < fullBlocks; i++)
            {
                // Load Plaintext
                // Using ReadUnaligned to support arbitrary input alignment
                block = Unsafe.ReadUnaligned<Vector128<byte>>(ptBase + (i * 16));

                // XOR with previous ciphertext (IV for first block)
                block = Sse2.Xor(block, feedback);

                // Initial Round
                block = Sse2.Xor(block, keys[0]);

                // Rounds 1-13
                for (int r = 1; r < 14; r++)
                {
                    block = Aes.Encrypt(block, keys[r]);
                }

                // Final Round
                block = Aes.EncryptLast(block, keys[14]);

                // Store Ciphertext
                Unsafe.WriteUnaligned(ctBase + (i * 16), block);

                // Update feedback
                feedback = block;
            }

            // Handle PKCS7 Padding for the final block
            // We construct the final block on the stack to safely handle padding
            byte* finalBlockBytes = stackalloc byte[16];
            int remaining = ptLen % 16;
            
            // Copy remaining plaintext bytes
            if (remaining > 0)
            {
                // We access the end of the plaintext buffer
                Buffer.MemoryCopy(ptBase + (fullBlocks * 16), finalBlockBytes, 16, remaining);
            }

            // Determine pad value (16 - remaining)
            byte padVal = (byte)(16 - remaining);
            
            // Fill remainder with pad value
            for (int j = remaining; j < 16; j++)
            {
                finalBlockBytes[j] = padVal;
            }

            // Load constructed padding block
            block = Sse2.LoadVector128(finalBlockBytes);

            // Encrypt final block
            block = Sse2.Xor(block, feedback); // CBC XOR
            block = Sse2.Xor(block, keys[0]);  // Key Whitening

            for (int r = 1; r < 14; r++)
            {
                block = Aes.Encrypt(block, keys[r]);
            }

            block = Aes.EncryptLast(block, keys[14]);

            // Write final block
            Unsafe.WriteUnaligned(ctBase + (fullBlocks * 16), block);
        }

        private void ExpandKey256(ReadOnlySpan<byte> key)
        {
            // AES-256 Key Expansion (Encryption keys only)
            
            fixed (byte* kPtr = key)
            {
                 _roundKeys[0] = Sse2.LoadVector128(kPtr);
                 _roundKeys[1] = Sse2.LoadVector128(kPtr + 16);
            }

            Vector128<byte> temp =  _roundKeys[1];
            Vector128<byte> prev =  _roundKeys[0];
            byte rcon = 0x01;

            for (int i = 1; i <= 6; i++)
            {
                // Even Keys (Rcon)
                var assist = Aes.KeygenAssist(temp, rcon);
                var kEven = ExpandStep(prev, assist);
                _roundKeys[2 * i] = kEven;

                if (rcon == 0x80) rcon = 0x1B; else rcon = (byte)(rcon << 1);

                // Odd Keys (SubWord broadcast)
                var t = Sse2.Shuffle(kEven.AsInt32(), 0xFF).AsByte();
                var subVector = Aes.EncryptLast(t, Vector128<byte>.Zero); 
                var kOdd = ExpandStep(temp, subVector);
                _roundKeys[2 * i + 1] = kOdd;

                prev = kEven;
                temp = kOdd;
            }

            // Final Key (14)
            var lastAssist = Aes.KeygenAssist(temp, rcon);
            _roundKeys[14] = ExpandStep(prev, lastAssist);
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