using bananapeel;
using Be.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace jatast
{
    /* JATAST by XAYRGA. Code adapted for use for GalaxEyes. */
    /* MIT License

    Copyright (c) 2021 XAYRGA

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE. */

    public enum EncodeFormat
    {
        ADPCM4 = 0,
        PCM16 = 1,
    }


    public class AST
    {
        private const int STRM_HEAD = 0x5354524D;
        private const int STRM_HEAD_SIZE = 0x40;
        private const int BLCK_HEAD = 0x424C434B;
        private const int BLCK_HEAD_SIZE = 0x20;
        private const int BLCK_SIZE = 0x2760;
        private const int BLCK_MAX_CHANNELS = 6;

        public EncodeFormat format;
        public int LoopStart;
        public int LoopEnd;
        public int SampleRate;
        public int SampleCount;
        public bool Loop;

        public short BitsPerSample;
        public short ChannelCount;

        public int BytesPerFrame;
        public int SamplesPerFrame;

        public int TotalBlocks = 0;

        public List<short[]> Channels = new();

        private int[] last = new int[BLCK_MAX_CHANNELS];
        private int[] penult = new int[BLCK_MAX_CHANNELS];
        private int sampleOffset = 0;
        private int writtenBlocks = 0;

        private long total_error = 0;
        public static byte[] PCM16ShortToByteBigEndian(short[] pcm)
        {
            var pcmB = new byte[pcm.Length * 2];
            // For some reason this is faster than pinning in memory?
            for (int i = 0; i < pcmB.Length; i += 2)
            {
                var ci = pcm[i / 2];
                pcmB[i] = (byte)(ci >> 8);
                pcmB[i + 1] = (byte)(ci & 0xFF);
            }
            return pcmB;
        }

        public void ReadFromStream(BeBinaryReader reader)
        {
            if (reader.ReadUInt32() != STRM_HEAD)
                throw new InvalidDataException("NOT AN AST");
            reader.ReadUInt32(); // Size
            format = (EncodeFormat)reader.ReadUInt16();
            BitsPerSample = (short)reader.ReadUInt16();
            ChannelCount = (short)reader.ReadUInt16();
            Loop = reader.ReadUInt16() == 0xFFFF;
            SampleRate = (int)reader.ReadUInt32();
            SampleCount = (int)reader.ReadUInt32();
            LoopStart = (int)reader.ReadUInt32();
            LoopEnd = (int)reader.ReadUInt32();
            reader.BaseStream.Position += 0x20;

            switch (format)
            {
                case EncodeFormat.ADPCM4:
                    BytesPerFrame = 9;
                    SamplesPerFrame = 16;
                    break;
                case EncodeFormat.PCM16:
                    BytesPerFrame = 2;
                    SamplesPerFrame = 1;
                    break;
            }

            int[] last = new int[ChannelCount];
            int[] penult = new int[ChannelCount];

            Channels = new List<short[]>(ChannelCount);
            for (int c = 0; c < ChannelCount; c++) 
                Channels.Add(new short[SampleCount + 32]);

            for (uint currSamp = 0; currSamp < SampleCount;)
            {
                uint magic = reader.ReadUInt32();
                if (magic != BLCK_HEAD)
                    throw new InvalidDataException($"Expected BLCK at {reader.BaseStream.Position - 4:X}");

                uint blockSize = reader.ReadUInt32();
                reader.BaseStream.Position += 0x18;
                uint framesInBlock = 0;
                uint samplesInBlock = 0;
                uint bytesOfAudio = 0;

                if (format == EncodeFormat.ADPCM4)
                {
                    framesInBlock = blockSize / 9;
                    samplesInBlock = framesInBlock * 16;
                    bytesOfAudio = framesInBlock * 9;
                }
                else // PCM16
                {
                    samplesInBlock = blockSize / 2;
                    bytesOfAudio = samplesInBlock * 2;
                }

                uint paddingToSkip = blockSize - bytesOfAudio;

                for (int c = 0; c < ChannelCount; c++)
                {
                    long startPos = reader.BaseStream.Position;

                    if (format == EncodeFormat.PCM16)
                    {
                        for (uint i = 0; i < samplesInBlock && (currSamp + i) < SampleCount; i++)
                            Channels[c][currSamp + i] = reader.ReadInt16();
                    }
                    else if (format == EncodeFormat.ADPCM4)
                    {
                        short[] pcmBuffer = new short[16];
                        for (int f = 0; f < framesInBlock; f++)
                        {
                            byte[] adpcmFrame = reader.ReadBytes(9);
                            ADPCMTRUE.ADPCM4TOPCM16(adpcmFrame, pcmBuffer, ref last[c], ref penult[c]);

                            int writePos = (int)currSamp + (f * 16);
                            if (writePos < SampleCount)
                            {
                                int copyLength = Math.Min(16, SampleCount - writePos);
                                Array.Copy(pcmBuffer, 0, Channels[c], writePos, copyLength);
                            }
                        }
                    }

                    if (paddingToSkip > 0)
                    {
                        reader.BaseStream.Position += paddingToSkip;
                    }
                }

                currSamp += samplesInBlock;
            }
        }
        private int getSampleOffset(int sample, byte channel = 1, int lastBlockSize = BLCK_SIZE)
        {
            var offset = STRM_HEAD_SIZE;
            var samplesPerBlock = (BLCK_SIZE / BytesPerFrame) * SamplesPerFrame;
            var blockNumber = sample / samplesPerBlock;

            // Seek the size of every block that was skipped
            offset += BLCK_HEAD_SIZE * (blockNumber + 1) + (blockNumber * BLCK_SIZE * ChannelCount) + ((channel - 1) * lastBlockSize);

            // Finally, find the offset of the frame that this sample sits in.
            offset += (sample - 1 - (blockNumber * samplesPerBlock)) / SamplesPerFrame * BytesPerFrame;
            return offset;
        }

        private byte[] EncodeADPCM4Block(short[] samples, int sampleCount, ref int last, ref int penult, int channel)
        {

            int frameCount = (sampleCount + 16 - 1) / 16; // Roundup samples to 16 or else we'll truncate frames.
            int frameBufferSize = frameCount * 9;
            byte[] adpcm_data = new byte[frameBufferSize];
            int adpcmBufferPosition = 0;

            // transform one frame at a time
            for (int ix = 0; ix < frameCount; ix++)
            {
                short[] wavIn = new short[16];
                byte[] adpcmOut = new byte[9];

                // Extract samples 16 at a time, 1 frame = 16 samples / 9 bytes. 
                for (int k = 0; k < 16; k++)
                    wavIn[k] = samples[(ix * 16) + k];

                var force_coef = -1;
                if (Loop && ((sampleOffset + (ix * 16)) == LoopStart))
                    force_coef = 0;// for some reason at the loop point the coefs have to be zero.  Thank you @ZyphronG

                total_error += bananapeel.PCM16TOADPCM4(wavIn, adpcmOut, ref last, ref penult, force_coef); // convert PCM16 -> ADPCM4            

                for (int k = 0; k < 9; k++)
                {
                    adpcm_data[adpcmBufferPosition] = adpcmOut[k]; // dump into ADPCM buffer
                    adpcmBufferPosition++;
                }

            }
            return adpcm_data;
        }


        public void WriteToStream(BeBinaryWriter wrt)
        {

            switch (format)
            {
                case EncodeFormat.ADPCM4:
                    BytesPerFrame = 9;
                    SamplesPerFrame = 16;
                    if (LoopStart % 16 != 0)
                        Console.WriteLine($"WARN: Start loop {LoopStart} is not divisible by 16, corrected to {LoopStart += (16 - (LoopStart % 16))} ");

                    break;
                case EncodeFormat.PCM16:
                    BytesPerFrame = 2;
                    SamplesPerFrame = 1;
                    break;
            }

            // Tell encoder to clamp boundary, nothing plays after the loop.
            if (Loop && (SampleCount > LoopEnd))
                SampleCount = LoopEnd;

            wrt.Write(STRM_HEAD);
            wrt.Write(0x00); // Temporary Length
            wrt.Write((ushort)format);
            wrt.Write((ushort)BitsPerSample);
            wrt.Write((ushort)ChannelCount);
            wrt.Write(Loop ? (ushort)0xFFFF : (ushort)0x0000);
            wrt.Write(SampleRate);
            wrt.Write(SampleCount);
            wrt.Write(LoopStart);
            wrt.Write(Loop ? LoopEnd : SampleCount); // ty zyphro <3 
            wrt.Write(BLCK_SIZE);
            wrt.Write(0);
            wrt.Write(0x7F000000);
            wrt.Write(new byte[0x14]);

            Console.WriteLine($"Loop point sits at 0x{getSampleOffset(LoopStart, (byte)ChannelCount):X}");

            TotalBlocks = (((SampleCount / SamplesPerFrame) * BytesPerFrame) + BLCK_SIZE - 1) / BLCK_SIZE;


            // sample history storage for blocks
            last = new int[BLCK_MAX_CHANNELS]; // reset
            penult = new int[BLCK_MAX_CHANNELS]; // reset

            sampleOffset = 0; // reset

            Console.WriteLine("Processing BLCK's");
            for (int i = 0; i < TotalBlocks; i++)
            {
                WriteBlock(wrt, i + 1 == TotalBlocks);
                wrt.Flush();
            }
            Console.WriteLine();

            // Save the size and flush it into 0x04 of the header.
            var size = (int)wrt.BaseStream.Position;
            wrt.BaseStream.Position = 4;
            wrt.Write(size - STRM_HEAD_SIZE);

            wrt.Flush();
            wrt.Close();
#if DEBUG
            Console.WriteLine($"Total sample error {total_error}");
#endif
        }

        private int WriteBlock(BeBinaryWriter wrt, bool lastBlock = false)
        {

            var totalFramesLeft = ((SampleCount - sampleOffset) + SamplesPerFrame - 1) / SamplesPerFrame;
            var thisBlockLength = (totalFramesLeft * BytesPerFrame) >= BLCK_SIZE ? BLCK_SIZE : totalFramesLeft * BytesPerFrame;
            var samplesThisFrame = (thisBlockLength / BytesPerFrame) * SamplesPerFrame;
            var paddingSize = 32 - (thisBlockLength % 32);
            if (paddingSize == 32) // Was zero, we're already aligned.
                paddingSize = 0;


            if (paddingSize > 0)
                Console.WriteLine($"\nLAST BLCK Add size {paddingSize} bytes {paddingSize / BytesPerFrame} frames {(paddingSize / BytesPerFrame) * SamplesPerFrame} samples");

            wrt.Write(BLCK_HEAD);
            wrt.Write(thisBlockLength + paddingSize);

#if DEBUG
            Console.WriteLine($"Encoding BLCK {writtenBlocks}/{TotalBlocks}: offset=0x{wrt.BaseStream.Position:X} samples={samplesThisFrame} frames={totalFramesLeft} size=0x{thisBlockLength:X},0x{thisBlockLength * ChannelCount} ");
#else
        Console.Write(".");
#endif

            var leadoutSampleSavePosition = wrt.BaseStream.Position;
            wrt.Write(new byte[4 * BLCK_MAX_CHANNELS]);

            for (int i = 0; i < Channels.Count; i++)
            {
                var samples = sliceSampleArray(Channels[i], sampleOffset, samplesThisFrame);

                int last_Current = last[i];
                int penultimate_Current = penult[i];

                byte[] writeBuff = new byte[0];
                switch (format)
                {
                    case EncodeFormat.ADPCM4:
                        writeBuff = EncodeADPCM4Block(samples, samplesThisFrame, ref last_Current, ref penultimate_Current, i);
                        break;
                    case EncodeFormat.PCM16:
                        writeBuff = PCM16ShortToByteBigEndian(samples);
                        break;
                }
                wrt.Write(writeBuff);

                last[i] = last_Current;
                penult[i] = penultimate_Current;

                if (paddingSize > 0)
                    wrt.Write(new byte[paddingSize]);
            }

            var oldPos = wrt.BaseStream.Position;
            wrt.BaseStream.Position = leadoutSampleSavePosition;

            // Now that the block has been rendered, push the predictor values into the file.

            if (lastBlock && Loop)
                for (int i = 0; i < BLCK_MAX_CHANNELS; i++)
                {
                    wrt.Write((short)0);
                    wrt.Write((short)0);
                }
            else
                for (int i = 0; i < BLCK_MAX_CHANNELS; i++)
                {
                    wrt.Write((short)(last[i]));
                    wrt.Write((short)(penult[i]));
                }
            wrt.BaseStream.Position = oldPos;

            writtenBlocks++;
            sampleOffset += samplesThisFrame;
            return 0;
        }

        private short[] sliceSampleArray(short[] samples, int start, int sampleCount)
        {

            var ret = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                if ((i + start) < samples.Length)
                    ret[i] = samples[start + (i)];
                else
                    ret[i] = 0;
            return ret;
        }
    }
}
