using AdvancedBinary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace AquaPlusEditor {
    public class FNT {

        long TexturePos = 0;
        uint FontSize = 0;
        bool? BigEnddian = null;
        Stream Font;
        Bitmap Texture;
        public FNT(Stream Font) => this.Font = Font;
        public FNT(Stream Font, bool BigEnddian) {
            this.BigEnddian = BigEnddian;
            Font = this.Font;
        }

        public Glyph[] GetGlyphs() {
            if (BigEnddian == null) {
                Font.Seek(8, 0);

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
            Reader.Seek(0xC, 0);
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

            Reader.Seek(0x8, 0);
            Reader.Seek(Reader.ReadRawType(Const.UINT32), SeekOrigin.Begin);

            while (Reader.BaseStream.Position % 4 != 0)
                Reader.Seek(1, SeekOrigin.Current);

            TexturePos = Reader.BaseStream.Position;
            Reader.Seek(0x8, SeekOrigin.Current);
            long TexLen = (long)Reader.ReadRawType(Const.UINT32) - (TexturePos+0x1CL);

            VirtStream Stream = new VirtStream(Reader.BaseStream, TexturePos + 0x1C, TexLen);
            Texture = new Bitmap(Stream);


            int CharPerLine = (int)(Texture.Width / FontSize);
            for (uint i = 0; i < Glyphs.LongLength; i++) {
                Glyphs[i].Texture = new Bitmap((int)FontSize, (int)FontSize);

                int X = (int)((i % CharPerLine)*FontSize);
                int Y = (int)((i / CharPerLine)*FontSize);
                if (Y >= Texture.Height)
                    continue;

                for (int x = 0; x < FontSize; x++)
                    for (int y = 0; y < FontSize; y++) {
                        Color Pixel = Texture.GetPixel(X + x, Y + y);
                        Glyphs[i].Texture.SetPixel(x, y, Pixel);
                    }
            }

            return Glyphs;
        }

        public void UpdatedGlyphs(Glyph[] Glyphs, Stream Output, bool CloseStream = true) {
            Font.Seek(0, 0);
            CopyBytes(Font, Output, 0x14);

            foreach (Glyph Glyph in Glyphs) {
                byte[] Buff = Encoding.UTF8.GetBytes(Glyph.Char.ToString());
                Buff = new byte[4 - Buff.Length].Concat(Buff).ToArray();
                if (!(bool)BigEnddian)
                    Buff = Buff.Reverse().ToArray();

                Output.Write(Buff, 0, Buff.Length);
                Font.Seek(4, SeekOrigin.Current);
                CopyBytes(Font, Output, 0x8);
            }

            int CharPerLine = (int)(Texture.Width / FontSize);
            for (int i = 0; i < Glyphs.LongLength; i++) {
                int X = (int)((i % CharPerLine) * FontSize);
                int Y = (int)((i / CharPerLine) * FontSize);

                var Glyph = Glyphs[i];

                for (int x = 0; x < FontSize; x++)
                    for (int y = 0; y < FontSize; y++) {
                        Color Pixel = Glyph.Texture.GetPixel(x, y);
                        Texture.SetPixel(X + x, Y + y, Pixel);
                    }
            }

            long Reaming = (Font.Position - TexturePos) + 0x1CL;
            CopyBytes(Font, Output, Reaming);

            MemoryStream TextData = new MemoryStream();
            Texture.Save(TextData, ImageFormat.Png);

            TextData.Seek(0, 0);
            CopyBytes(TextData, Output, TextData.Length);


            uint SecLen = (uint)(TextData.Length + 0x1CL);
            while (SecLen % 4 != 0)
                SecLen++;

            StructWriter Writer = new StructWriter(Output, (bool)BigEnddian);
            while (Writer.BaseStream.Position % 4 != 0)
                Writer.Write((byte)0x00);

            Writer.Seek(TexturePos + 0x8, SeekOrigin.Begin);
            Writer.WriteRawType(Const.UINT32, SecLen);
            Writer.Seek(4, SeekOrigin.Current);
            Writer.WriteRawType(Const.UINT32, (uint)TextData.Length);


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
    }


    public struct Glyph {
        public char Char;
        public Bitmap Texture;
    }
}
