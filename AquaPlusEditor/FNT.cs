using AdvancedBinary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace AquaPlusEditor {
    public unsafe class FNT {

        long TexturePos = 0;
        uint FontSize = 0;
        bool? BigEnddian = null;
        bool? SteamVer = null;

        uint HeaderNameSize => (SteamVer == true ? 12u : 8u);

        Stream Font;
        Bitmap Texture;
        public FNT(Stream Font) => this.Font = Font;
        public FNT(Stream Font, bool BigEnddian) {
            this.BigEnddian = BigEnddian;
            Font = this.Font;
        }

        public Glyph[] GetGlyphs() {
            if (SteamVer == null) {
                byte[] Buff = new byte[4];
                Font.Position = 8;
                Font.Read(Buff, 0, Buff.Length);
                uint DW = BitConverter.ToUInt32(Buff, 0);
                SteamVer = DW == 0x20202020;
                if (SteamVer.Value)
                    BigEnddian = false;
            }

            if (BigEnddian == null) {
                Font.Seek(HeaderNameSize, 0);

                byte[] Buff = new byte[4];
                Font.Read(Buff, 0, Buff.Length);
                int Len = BitConverter.ToInt32(Buff, 0);

                if (Len < 0 || Len > Font.Length)
                    BigEnddian = true;
                else
                    BigEnddian = false;
            }
            Font.Seek(0,0);
            StructReader Reader = new StructReader(Font, (bool)BigEnddian);
            Reader.Seek(HeaderNameSize + 4, 0);
            FontSize = Reader.ReadRawType(Const.UINT32);
            Glyph[] Glyphs = new Glyph[Reader.ReadRawType(Const.UINT32)];

            for (uint i = 0; i < Glyphs.LongLength; i++) {
                Glyph Glyph = new Glyph();
                byte[] Buffer = new byte[4];
                Reader.Read(Buffer, 0, Buffer.Length);
                if (!(bool)BigEnddian)
                    Buffer = Buffer.Reverse().ToArray();
                List<byte> Buff = new List<byte>();
                for (int x = 0, y = 0; x < Buffer.Length; x++) {
                    if (y == 0) {
                        if (Buffer[x] == 0x00)
                            continue;
                        else
                            y = 1;
                    }
                    Buff.Add(Buffer[x]);
                }

                Glyph.Char = Encoding.UTF8.GetChars(Buff.ToArray()).First();

                Glyphs[i] = Glyph;
                Reader.Seek(0x8, SeekOrigin.Current);//X and Y of the char in the texture, But isn't needed, ignoring...
            }

            Reader.Seek(HeaderNameSize, 0);
            Reader.Seek(Reader.ReadRawType(Const.UINT32), SeekOrigin.Begin);

            if (SteamVer.Value)
                Reader.ReadByte();

            while (Reader.BaseStream.Position % 4 != 0)
                Reader.Seek(1, SeekOrigin.Current);

            TexturePos = Reader.BaseStream.Position;
            int TexHeaderSize = 0x14 + (int)HeaderNameSize;
            Reader.Seek(HeaderNameSize, SeekOrigin.Current);
            long TexLen = (long)Reader.ReadRawType(Const.UINT32) - (TexturePos+TexHeaderSize);

            if (SteamVer.Value)
            {
                Reader.Seek(8, SeekOrigin.Current);
                TexLen = (long)Reader.ReadRawType(Const.UINT32);
            }

            VirtStream Stream = new VirtStream(Reader.BaseStream, TexturePos + TexHeaderSize, TexLen);
            if (SteamVer.Value)
            {
                Reader.BaseStream.Position = TexturePos + 0x1C;
                int Width = Reader.ReadUInt16();
                int Height = Reader.ReadUInt16();

                byte[] Buffer = new byte[Stream.Length];
                Stream.Read(Buffer, 0, Buffer.Length);
                fixed (byte* Addr = &Buffer[0])
                {
                    Texture = new Bitmap(Width, Height, 4 * Width, PixelFormat.Format32bppArgb, new IntPtr(Addr));
                }
            }
            else Texture = new Bitmap(Stream);


            int CharPerLine = (int)(Texture.Width / FontSize);
            for (uint i = 0; i < Glyphs.LongLength; i++)
            {
                Glyphs[i].Texture = new Bitmap((int)FontSize, (int)FontSize);

                int X = (int)((i % CharPerLine) * FontSize);
                int Y = (int)((i / CharPerLine) * FontSize);
                if (Y >= Texture.Height)
                    continue;

                for (int x = 0; x < FontSize; x++)
                    for (int y = 0; y < FontSize; y++)
                    {
                        Color Pixel = Texture.GetPixel(X + x, Y + y);
                        Glyphs[i].Texture.SetPixel(x, y, Pixel);
                    }

            }

            return Glyphs;
        }

        public void UpdatedGlyphs(Glyph[] Glyphs, Stream Output, bool CloseStream = true) {
            Font.Seek(0, 0);
            CopyBytes(Font, Output, HeaderNameSize + 12);

            Bitmap Texture = this.Texture.Clone(new Rectangle(Point.Empty, new Size(this.Texture.Width, this.Texture.Height)), PixelFormat.Format32bppArgb);

            foreach (Glyph Glyph in Glyphs) {
                byte[] Buff = Encoding.UTF8.GetBytes(Glyph.Char.ToString());
                Buff = new byte[4 - Buff.Length].Concat(Buff).ToArray();
                if (!(bool)BigEnddian)
                    Buff = Buff.Reverse().ToArray();

                Output.Write(Buff, 0, Buff.Length);
                Font.Seek(4, SeekOrigin.Current);
                CopyBytes(Font, Output, 0x8);
            }

            if (SteamVer.Value) {
                Output.WriteByte(0);
                while (Output.Position % 4 != 0)
                    Output.WriteByte(0);
            }

            long NewTextPos = Output.Position;

            int CharPerLine = (int)(Texture.Width / FontSize);
            for (int i = 0; i < Glyphs.LongLength; i++)
            {
                int X = (int)((i % CharPerLine) * FontSize);
                int Y = (int)((i / CharPerLine) * FontSize);

                var Glyph = Glyphs[i];

                for (int x = 0; x < FontSize; x++)
                    for (int y = 0; y < FontSize; y++)
                    {
                        Color Pixel = Glyph.Texture.GetPixel(x, y);
                        Texture.SetPixel(X + x, Y + y, Pixel);
                    }

            }



            int TexHeaderSize = 0x14 + (int)HeaderNameSize;
            MemoryStream TextData = new MemoryStream();
            if (SteamVer.Value)
            {
                Font.Seek(TexturePos, 0);
                CopyBytes(Font, TextData, TexHeaderSize);

                BinaryWriter pWriter = new BinaryWriter(TextData);
                for (int y = 0; y < Texture.Height; y++)
                    for (int x = 0; x < Texture.Width; x++) {
                        var Pixel = Texture.GetPixel(x, y);
                        pWriter.Write(Pixel.ToArgb());
                    }
            }
            else
            {
                long Reaming = (Font.Position - TexturePos) + 0x1CL;
                CopyBytes(Font, Output, Reaming);

                Texture.Save(TextData, ImageFormat.Png);
            }

            TextData.Seek(0, 0);
            CopyBytes(TextData, Output, TextData.Length);

            uint SecLen = (uint)(TextData.Length + (SteamVer.Value ? 0 : TexHeaderSize));
            while (SecLen % (SteamVer.Value ? 0x10 : 0x4) != 0)
                SecLen++;

            StructWriter Writer = new StructWriter(Output, (bool)BigEnddian);
            while (Writer.BaseStream.Position % (SteamVer.Value ? 0x10 : 0x4) != 0)
                Writer.Write((byte)0x00);

            Writer.Seek(NewTextPos + HeaderNameSize, SeekOrigin.Begin);
            Writer.WriteRawType(Const.UINT32, SecLen);
            Writer.Seek(SteamVer.Value ? 8 : 4, SeekOrigin.Current);
            Writer.WriteRawType(Const.UINT32, (uint)(TextData.Length - (SteamVer.Value ? TexHeaderSize : 0)));


            Writer.Flush();

            TextData.Close();
            if (CloseStream)
                Output.Close();
        }

        private void CopyBytes(Stream From, Stream to, long Count) {
            while (Count != 0) {
                const int Size = 1024 * 1024;
                long ToCopy = Count;
                if (ToCopy > Size)
                    ToCopy = Size;
                if (ToCopy > Count)
                    ToCopy = Count;
                Count -= ToCopy;

                CopyBytes(From, to, (int)ToCopy);
            }
        }
        private void CopyBytes(Stream From, Stream To, int Count) {
            byte[] Buff = new byte[Count];
            From.Read(Buff, 0, Buff.Length);
            To.Write(Buff, 0, Buff.Length);
        }

        static void ChangeProtection(int* Address, uint Size, uint Protection) {
            VirtualProtectEx(System.Diagnostics.Process.GetCurrentProcess().Handle, Address, Size, Protection, out _);
        }
        [DllImport("kernel32.dll")]
        unsafe static extern bool VirtualProtectEx(IntPtr hProcess, int* lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);
    }


    public struct Glyph {
        public char Char;
        public Bitmap Texture;
    }
}
