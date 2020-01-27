using AdvancedBinary;
using libWiiSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace AquaPlusEditor {
    public unsafe class TEX {

        const uint NameSize = 8;
        const uint ExNameSize = 12;

        Size TexSize;
        bool WithSection = true;
        bool? SteamVersion = null;
        bool? IsBigEnddian = null;
        Stream Texture;

        uint Flags;

        ulong SteamUnk;

        bool? _Compressed = null;
        bool Tiled => ((Flags >> 8 * 2) & 0xFF) == 0x3;
        bool Compressed => _Compressed ?? ((Flags >> 8 * 2) & 0xFF) == 0x6;
        public TEX(Stream Texture) {
            this.Texture = Texture;
        }
        public TEX(Stream Texture, bool BigEnddian) {
            this.Texture = Texture;
            IsBigEnddian = BigEnddian;
        }

        Stream Open() {
            WithSection = true;
            if (SteamVersion == null) {
                byte[] DW = new byte[4];
                Texture.Seek(8, 0);
                Texture.Read(DW, 0, DW.Length);
                SteamVersion = BitConverter.ToUInt32(DW, 0) == 0x20202020;

                Texture.Seek(0, 0);
                Texture.Read(DW, 0, DW.Length);
                if (BitConverter.ToUInt32(DW, 0) != 0x74786554)
                {
                    SteamVersion = true;
                    WithSection = false;
                }

                if (SteamVersion.Value)
                    IsBigEnddian = false;
            }
            if (IsBigEnddian == null) {
                byte[] DW = new byte[4];
                Texture.Seek(8, 0);
                Texture.Read(DW, 0, DW.Length);
                IsBigEnddian = BitConverter.ToUInt32(DW, 0) > Texture.Length;
            }
            uint SectionHeaderSize = WithSection ? (SteamVersion.Value ? ExNameSize : NameSize) + 4 : 0;
            Texture.Seek(0, 0);
            StructReader Reader = new StructReader(Texture, (bool)IsBigEnddian);
            Reader.Seek(SectionHeaderSize, 0);

            uint PNGLen, Width, Height;
            if (SteamVersion.Value)
            {
                Flags = 0;

                SteamUnk = Reader.ReadRawType(Const.UINT64);

                PNGLen = Reader.ReadRawType(Const.UINT32);
                Width = Reader.ReadRawType(Const.UINT16);
                Height = Reader.ReadRawType(Const.UINT16);

                _Compressed = Reader.ReadUInt32() == 0x37375A4C;
            }
            else
            {
                _Compressed = null;

                Flags = Reader.ReadRawType(Const.UINT32);
                PNGLen = Reader.ReadRawType(Const.UINT32);
                Width = Reader.ReadRawType(Const.UINT32);
                Height = Reader.ReadRawType(Const.UINT32);
            }

            TexSize = new Size((int)Width, (int)Height);
            return new VirtStream(Texture, (SteamVersion.Value ? ExNameSize : NameSize) + 0x14, PNGLen);
        }

        public Bitmap Decode() {
            MemoryStream Stream = new MemoryStream();
            Open().CopyTo(Stream);
            Stream.Position = 0;

            if (Compressed && SteamVersion.Value) {
                Stream = new MemoryStream(LZSSDecompress(Stream));
            } else if (Compressed) {
                var Decompressor = new Lz77();
                MemoryStream Buffer = new MemoryStream();
                Decompressor.Decompress(Stream).CopyTo(Buffer);
                Stream.Close();
                Stream = Buffer;
            }

            Bitmap Output;

            if (SteamVersion.Value)
            {
                Output = new Bitmap(TexSize.Width, TexSize.Height);
                byte[] Buffer = Stream.ToArray();
                fixed (void* pAddr = &Buffer[0])
                {

#if DEBUG
                    System.Diagnostics.Debug.Assert(Buffer.Length == TexSize.Width * TexSize.Height);
#endif
                    uint* pPixels = (uint*)pAddr;
                    int Stride = TexSize.Width;
                    for (int i = 0; i < Buffer.Length/4; i++, pPixels++)
                    {
                        int X = i % Stride;
                        int Y = i / Stride;

                        unchecked
                        {
                            uint ARGB = *pPixels;

                            //ABGR => ARGB 
                            ARGB = (ARGB & 0xFF00FF00) | ((ARGB & 0x00FF0000) >> 8 * 2) | ((ARGB & 0x000000FF) << 8 * 2);

                            Output.SetPixel(X, Y, Color.FromArgb((int)ARGB));
                        }
                    }
                }
            }
            else Output = Image.FromStream(Stream) as Bitmap;

            Stream.Close();

            TexSize = Output.Size;

            if (Tiled)
                Output = Untile(Output);

            return Output;
        }

        public static Bitmap Untile(Bitmap Picture) {
            Bitmap Result = new Bitmap(Picture.Width, Picture.Height, PixelFormat.Format32bppArgb);
            Point[] ZMap = GenZMap((ulong)Picture.Width * (ulong)Picture.Height);
            ulong ZIndex = 0;
            for (int y = 0; y < Result.Height; y++)
                for (int x = 0; x < Result.Width; x++) {
                    Point ZPos = ZMap[ZIndex++];
                    Result.SetPixel(ZPos.X, ZPos.Y, Picture.GetPixel(x, y));
                }
            return Result;

        }
        public static Bitmap Retile(Bitmap Picture) {
            Bitmap Result = new Bitmap(Picture.Width, Picture.Height, PixelFormat.Format32bppArgb);
            Point[] ZMap = GenZMap((ulong)Picture.Width * (ulong)Picture.Height);
            ulong ZIndex = 0;
            for (int y = 0; y < Result.Height; y++)
                for (int x = 0; x < Result.Width; x++) {
                    Point ZPos = ZMap[ZIndex++];
                    Result.SetPixel(x, y, Picture.GetPixel(ZPos.X, ZPos.Y));
                }
            return Result;

        }

        static uint Morton1(ulong x) {
            x = x & 0x5555555555555555;
            x = (x | (x >> 1)) & 0x3333333333333333;
            x = (x | (x >> 2)) & 0x0F0F0F0F0F0F0F0F;
            x = (x | (x >> 4)) & 0x00FF00FF00FF00FF;
            x = (x | (x >> 8)) & 0x0000FFFF0000FFFF;
            x = (x | (x >> 16)) & 0x00000000FFFFFFFF;
            return (uint)x;
        }

        static Point GetMorton(ulong index) {
            int x = (int)Morton1(index);
            int y = (int)Morton1(index >> 1);

            return new Point(x, y);
        }
        static Point[] GenZMap(ulong Count) {
            List<Point> Map = new List<Point>();
            for (ulong n = 0; n < Count; n++) {

                Map.Add(GetMorton(n));
            }
            return Map.ToArray();
        }


        public void Encode(Bitmap Texture, Stream Output, bool CloseStreams = true) {
            StructWriter Writer = new StructWriter(Output, (bool)IsBigEnddian);
            Writer.Write(0x2065727574786554);

            if (SteamVersion.Value)
            {
                Writer.Write(0x20202020);

                Writer.Write(0);//0xC, section length 
                Writer.WriteRawType(Const.UINT64, SteamUnk);
                Writer.Write(0);//0x18, pnglen
                Writer.WriteRawType(Const.UINT16, (ushort)TexSize.Width);
                Writer.WriteRawType(Const.UINT16, (ushort)TexSize.Height);
            }
            else
            {

                Writer.Write(0);//0x8, blocklen
                Writer.WriteRawType(Const.UINT32, Flags);
                Writer.Write(0);//0x10, pnglen
                Writer.WriteRawType(Const.UINT32, (uint)TexSize.Width);
                Writer.WriteRawType(Const.UINT32, (uint)TexSize.Height);

            }

            if (Tiled)
                Texture = Retile(Texture);
            uint SectionLength;
            if (SteamVersion.Value)
            {
                byte[] Buffer = new byte[Texture.Width * Texture.Height * 4];
                fixed (void* pBuff = &Buffer[0]) {
                    int Stride = Texture.Width;
                    uint* pPixel = (uint*)pBuff;
                    unchecked
                    {
                        for (int i = 0; i < Buffer.Length / 4; i++)
                        {
                            int X = i % Stride;
                            int Y = i / Stride;

                            uint ARGB = (uint)Texture.GetPixel(X, Y).ToArgb();

                            //ARGB => ABGR
                            *pPixel++ = (ARGB & 0xFF00FF00) | ((ARGB & 0x00FF0000) >> 8 * 2) | ((ARGB & 0x000000FF) << 8 * 2);
                        }
                    }
                }

                if (Compressed) {
                    Buffer = LZSSCompress(Buffer);
                }

                Writer.Write(Buffer);

                SectionLength = (uint)Writer.BaseStream.Position;
                uint TexLen = SectionLength - 0x20;
                Writer.Seek(0xC, 0);
                Writer.WriteRawType(Const.UINT32, SectionLength);
                Writer.Seek(0x18, 0);
                Writer.WriteRawType(Const.UINT32, TexLen);
            }
            else
            {
                Texture.Save(Writer.BaseStream, ImageFormat.Png);
                SectionLength = (uint)Writer.BaseStream.Position;
                uint PngLen = SectionLength - 0x1C;
                Writer.Seek(0x8, 0);
                Writer.WriteRawType(Const.UINT32, SectionLength);
                Writer.Seek(0x10, 0);
                Writer.WriteRawType(Const.UINT32, PngLen);
            }

            Writer.Seek(SectionLength, 0);
            while (Writer.BaseStream.Position % (SteamVersion.Value ? 8 : 4) != 0)
                Writer.Write((byte)0x0);

            StructReader Reader = new StructReader(this.Texture, (bool)IsBigEnddian);
            Reader.Seek(SteamVersion.Value ? 0x0C : 0x08, 0);
            Reader.Seek(Reader.ReadRawType(Const.UINT32), 0);

            while (Reader.BaseStream.Position % (SteamVersion.Value ? 8 : 4) != 0)
                Reader.ReadByte();
            
            Reader.BaseStream.CopyTo(Writer.BaseStream);
            Writer.Flush();

            if (CloseStreams) {
                Writer.Close();
                Output?.Close();
            }
        }

        public byte[] LZSSDecompress(Stream Input) {
            var Reader = new BinaryReader(Input);

            if (Reader.ReadUInt32() != 0x37375A4C)
                throw new Exception("Unexpected Compression Header");

            uint Size = Reader.ReadUInt32();

            uint OPCount = Reader.ReadUInt32();
            uint DataOffset = Reader.ReadUInt32();
            uint TotalFlags = DataOffset - 16;

            byte[] dst = new byte[Size];

            Queue<byte> FlagsBuffer = new Queue<byte>();
            for (int i = 0; i < TotalFlags; i++)
                FlagsBuffer.Enqueue(Reader.ReadByte());

            for (uint o = 0, FLAGS = 0, BitCount = 0, x = 0; o < OPCount; o++) { 
                if (BitCount == 0) {
                    BitCount = 8;
                    FLAGS = FlagsBuffer.Dequeue();
                }

                if ((FLAGS & 0x80) != 0)
                {
                    byte Offset = Reader.ReadByte();
                    byte Amount = (byte)(Reader.ReadByte() + 3);

                    for (int i = 0; i < Amount; i++)
                        dst[x + i] = dst[x - Offset + i];

                    x += Amount;
                }
                else
                    dst[x++] = Reader.ReadByte();

                FLAGS <<= 1;
                BitCount--;
            }

            return dst;
        }

        public static byte[] LZSSCompress(byte[] Input)
        {
            List<byte> Flags = new List<byte>();
            List<byte> Data = new List<byte>();
            uint Operations = 0;
            unchecked {
                byte CurrentFlag = 0;
                int BitCount = 0;
                for (int i = 0; i < Input.Length;) { 
                    if (BitCount == 8) {
                        Flags.Add(CurrentFlag);
                        CurrentFlag = 0;
                        BitCount = 0;
                    }
                    bool Match = false;
                    int Reaming = Input.Length - i;
                    if (Reaming > 255) Reaming = 255;
                    for (int Offset = 255; Offset > 0; Offset--)
                    {
                        if (Offset < 3 || i - Offset < 0)
                            continue;
                        byte[] Range = Take(Input, i - Offset, 3);
                        if (EqualsAt(Input, i, Range))
                        {
                            int Amount = 0;

                            fixed (void* pvBuffer = &Input[i - Offset])
                            fixed (void* pvInput  = &Input[i])
                            {
                                byte* pBuffer = (byte*)pvBuffer;
                                byte* pInput = (byte*)pvInput;

                                while (*pBuffer == *pInput && pBuffer < pvInput) {
                                    pBuffer++;
                                    pInput++;
                                    Amount++;
                                }
                            }

                            while (Amount > Offset || Amount >= Reaming)
                                Amount--;

                            if (Amount < 0)
                                goto EndSearch;

                            Match = true;
                            Data.Add((byte)Offset);
                            Data.Add((byte)(Amount - 3));
                            i += Amount;
                            goto EndSearch;
                        }
                    }
                EndSearch:;

                    if (!Match)
                        Data.Add(Input[i++]);
                    else
                        CurrentFlag |= (byte)(1 << 7 - BitCount);

                    BitCount++;
                    Operations++;
                }

                if (BitCount != 0) 
                    Flags.Add((byte)(CurrentFlag << 7 - BitCount));
            }

            using (MemoryStream Output = new MemoryStream()) 
            using (BinaryWriter Writer = new BinaryWriter(Output))
            {
                Writer.Write(0x37375A4C);//"LZ77"
                Writer.Write((uint)Input.LongLength);
                Writer.Write(Operations);
                uint DataOffset = (uint)Flags.Count + 16;
                Writer.Write(DataOffset);
                Writer.Write(Flags.ToArray(), 0, Flags.Count);
                Writer.Write(Data.ToArray(), 0, Data.Count);

                Writer.Flush();
                return Output.ToArray();
            }
        }

        private static byte[] Take(byte[] Buffer, int Pos, int Amount) {
            if (Pos < 0)
                return null;
            byte[] Rst = new byte[Amount];
            for (int i = 0; i < Amount; i++)
                Rst[i] = Buffer[i + Pos];
            return Rst;
        }

        private static bool EqualsAt(byte[] Buffer, int Pos, byte[] Data)
        {
            if (Pos < 0)
                return false;
            if (Data == null)
                return false;
            if (Pos + Data.Length >= Buffer.Length)
                return false;

            for (int i = 0; i < Data.Length; i++)
                if (Buffer[Pos + i] != Data[i])
                    return false;
            return true;
        }
    }
}
