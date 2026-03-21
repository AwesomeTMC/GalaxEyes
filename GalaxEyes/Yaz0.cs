using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Linq;

namespace GalaxEyes;

/// <summary>
/// A yaz0 compression algorithm that's fast and strong, based on zlib's LZ77 deflate and Hack.IO's lazy lookahead.
/// See https://wiki.tockdom.com/wiki/YAZ0_(File_Format)
/// </summary>
public static class Yaz0
{
    private static int MAX_MATCH_SIZE = 0xFF + 0x12;
    /// <summary>
    /// Compresses a specified ReadOnlySpan with yaz0. Compresses fast, but not the most optimal size (for this algorithm)
    /// </summary>
    /// <param name="input">The span to compress</param>
    /// <returns></returns>
    public static byte[] CompressSpeed(ReadOnlySpan<byte> input) => Compress(input, 0x1000, 0x10);
    /// <summary>
    /// Compresses a specified ReadOnlySpan with yaz0. Compresses with a balance between size and speed. 
    /// </summary>
    /// <param name="input">The span to compress</param>
    /// <returns></returns>
    public static byte[] CompressBalanced(ReadOnlySpan<byte> input) => Compress(input, 0x1000, 0x100);
    /// <summary>
    /// Compresses a specified ReadOnlySpan with yaz0. Gets the best compressed file size (for this algorithm)
    /// </summary>
    /// <param name="input">The span to compress</param>
    /// <returns></returns>
    public static byte[] CompressSize(ReadOnlySpan<byte> input) => Compress(input, 0x1000, 0x1000);
    /// <summary>
    /// Compresses a specified ReadOnlySpan with yaz0. If you want it to be faster, prefer reducing maxChainLength over maxLookback, <br />
    /// because reducing maxChainLength will reduce time significantly while not impacting file size as much as maxLookback. <br />
    /// Note that yaz0 compression can in some cases increase the file size. 
    /// </summary>
    /// <param name="input">The span to compress</param>
    /// <param name="maxLookback">The maximum it will search back in the file. Reducing this will save time at the cost of a bigger file size. Generally, don't touch this.</param>
    /// <param name="maxChainLength">The maximum it will search back in the *chain*. Reducing this will save time at the cost of a bigger file size.</param>
    /// <returns></returns>
    public static byte[] Compress(ReadOnlySpan<byte> input, ushort maxLookback = 0x1000, ushort maxChainLength = 0x1000)
    {
        // Never compress a file with yaz0 twice, it's pointless
        if (input.Length < 0x10 || input.Slice(0, 4).SequenceEqual("Yaz0"u8)) 
            return input.ToArray();
        maxLookback = Math.Min(maxLookback, (ushort)0x1000);
        maxChainLength = Math.Clamp(maxChainLength, (ushort)1, (ushort)0x1000);

        Span<byte> output = new byte[input.Length + (input.Length / 8) + 0x11];

        // Magic
        "Yaz0"u8.CopyTo(output);
        Span<byte> outputPos = output.Slice(4);

        // Uncompressed Size
        BinaryPrimitives.WriteUInt32BigEndian(outputPos, (uint)input.Length);
        outputPos = output.Slice(0x10); // the other two bytes are 0. maybe I'll use it for versioning in the future

        using MatchDictionary dictionary = new();
        int inputPos = 0;
        Span<byte> groupHeaderByte = default;
        byte chunkCount = 0;

        while (inputPos < input.Length)
        {
            if (chunkCount == 0)
            {
                groupHeaderByte = outputPos;
                outputPos = outputPos.Slice(1);
            }
            groupHeaderByte[0] <<= 1;


            int bestLen = 0;
            var match = FindLookahead(input, inputPos, maxLookback, maxChainLength, ref bestLen, dictionary);
            if (bestLen < 3)
                bestLen = 1;

            // Add to dict
            for (int i = inputPos; i < inputPos + bestLen; i++)
            {
                if (i + 2 <= input.Length)
                {
                    ushort hash = BinaryPrimitives.ReadUInt16BigEndian(input.Slice(i));
                    dictionary.AddMatch(hash, i);
                }
            }

            // Copy ONE byte since we didn't find a good match
            if (match == -1 || bestLen < 3)
            {
                groupHeaderByte[0] |= 0x01;
                outputPos[0] = input[inputPos++];
                outputPos = outputPos.Slice(1);
            }
            else
            {
                ushort RRR = (ushort)((inputPos - match - 1) & 0x0FFF);
                if (inputPos - match > 0x1000)
                    throw new UnreachableException("inputPos - match > 0x1000. FindLookahead should not give a inputPos - match > 0x1000 out of efficiency.");
                // 2 byte encoding, supports 0x2 through 0xF + 0x2 copied bytes
                if (bestLen < 0x12)
                {
                    // NR RR, where N is "copy N + 2 bytes", and R is "go back RRR+1 bytes"
                    ushort NRRR = (ushort)(RRR | ((bestLen - 2) << 12)); // Add in N
                    BinaryPrimitives.WriteUInt16BigEndian(outputPos, NRRR);
                    outputPos = outputPos.Slice(2);
                }
                // 3 byte encoding, supports 0x12 through 0xFF + 0x12 copied bytes
                else
                {
                    // 0R RR NN
                    // 0RRR is already in the state we need
                    BinaryPrimitives.WriteUInt16BigEndian(outputPos, RRR);
                    outputPos = outputPos.Slice(2);
                    if (bestLen > MAX_MATCH_SIZE)
                        throw new UnreachableException("bestLen > MAX_MATCH_SIZE. FindLookahead should not give a bestLen > MAX_MATCH_SIZE out of efficiency.");
                    outputPos[0] = (byte)(bestLen - 0x12);
                    outputPos = outputPos.Slice(1);
                }
                inputPos += bestLen;
            }

            chunkCount++;
            if (chunkCount == 8)
                chunkCount = 0;
        }

        // remaining "chunks"
        if (chunkCount > 0)
            groupHeaderByte[0] <<= (8 - chunkCount);

        return output.Slice(0, output.Length - outputPos.Length).ToArray();
    }

    /// <summary>
    /// <see cref="MatchDictionary.FindBestMatch"/>, but with lazy lookahead.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="pos"></param>
    /// <param name="maxLookback"></param>
    /// <param name="bestLen"></param>
    /// <param name="dictionary"></param>
    /// <returns></returns>
    private static int FindLookahead(ReadOnlySpan<byte> src, int pos, ushort maxLookback, ushort maxChainLength, ref int bestLen, MatchDictionary dictionary)
    {
        int firstBestLen = 0;
        int firstBestMatch = dictionary.FindBestMatch(src, pos, maxLookback, maxChainLength, ref firstBestLen);

        bestLen = firstBestLen;

        if (bestLen >= 3)
        {
            int secondBestLen = 0;
            int secondBestMatch = dictionary.FindBestMatch(src, pos + 1, maxLookback, maxChainLength, ref secondBestLen);

            if (secondBestLen >= bestLen + 2)
            {
                bestLen = 1;
                return -1;
            }
        }

        return firstBestMatch;
    }

    /// <summary>
    /// Match dictionary, based on zlib's LZ77 implementation
    /// </summary>
    private class MatchDictionary : IDisposable
    {
        /// <summary>
        /// Stores the most recent instance of the match
        /// </summary>
        private readonly int[] head;
        /// <summary>
        /// Stores previous matches up to the lookback window size
        /// </summary>
        private readonly int[] prev;
        /// <summary>
        /// The window size. The max lookback amount doesn't take up much ram so we just use that here.
        /// </summary>
        private const int WINDOW_SIZE = 4096;

        public MatchDictionary()
        {
            head = ArrayPool<int>.Shared.Rent(ushort.MaxValue + 1);
            Array.Fill(head, -1);

            prev = ArrayPool<int>.Shared.Rent(WINDOW_SIZE);
            Array.Fill(prev, -1);
        }

        public void AddMatch(ushort val, int pos)
        {
            prev[pos % WINDOW_SIZE] = head[val];
            head[val] = pos;
        }

        public int FindBestMatch(ReadOnlySpan<byte> input, int pos, ushort maxLookback, ushort maxChainLength, ref int bestLen)
        {
            bestLen = 0;
            int bestMatchPos = -1;

            if (pos + 2 > input.Length)
                return -1;

            ushort startVal = BinaryPrimitives.ReadUInt16BigEndian(input.Slice(pos));
            int matchPos = head[startVal];
            int startPos = Math.Max(pos - maxLookback, 0);

            int maxLen = Math.Min(MAX_MATCH_SIZE, input.Length - pos);
            int chainLength = 0;
            while (matchPos >= startPos && matchPos != -1 && chainLength < maxChainLength)
            {
                int maxPossible = Math.Min(maxLen, input.Length - matchPos);
                int len = input.Slice(matchPos, maxPossible).CommonPrefixLength(input.Slice(pos, maxPossible));

                if (len > bestLen)
                {
                    bestLen = len;
                    bestMatchPos = matchPos;

                    // We already have the best match, we can't get higher
                    if (bestLen == maxLen) 
                        break;
                }

                matchPos = prev[matchPos % WINDOW_SIZE];
                chainLength++;
            }

            return bestMatchPos;
        }

        public void Dispose()
        {
            ArrayPool<int>.Shared.Return(head);
            ArrayPool<int>.Shared.Return(prev);
        }
    }

};
