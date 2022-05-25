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
        bool? BigEnddian = null;
        bool? SteamVer = null;

        uint HeaderNameSize => (SteamVer == true ? 12u : 8u);

        Stream Font;
        
        public Bitmap Texture;
        
        public uint FontSize = 0;
        public int FontWidth => Texture.Width;
        public int FontHeight => Texture.Height;
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

                Reader.ReadStruct(ref Glyph);
                
                var Buffer = BitConverter.GetBytes(Glyph.UTF8).TakeWhile(x => x != 0);

                if (!(bool)BigEnddian)
                    Buffer = Buffer.Reverse();
                
                Glyph.Char = Encoding.UTF8.GetChars(Buffer.ToArray()).First();

                Glyphs[i] = Glyph;
            }

            Reader.Seek(HeaderNameSize, 0);
            Reader.Seek(Reader.ReadRawType(Const.UINT32), SeekOrigin.Begin);

            if (SteamVer.Value)
                Reader.ReadByte();

            while (Reader.BaseStream.Position % 4 != 0)
                Reader.Seek(1, SeekOrigin.Current);

            TexturePos = Reader.BaseStream.Position;
            int TexHeaderSize = 0x14 + (int)HeaderNameSize;
            Reader.Seek(HeaderNameSize + 8, SeekOrigin.Current);
            long TexLen = (long)Reader.ReadRawType(Const.UINT32);

            if (SteamVer.Value)            
                TexLen = (long)Reader.ReadRawType(Const.UINT32);

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

            
            for (int i = 0; i < Glyphs.Length; i++)
            {
                Glyphs[i].RealX = (int)Math.Round(Glyphs[i].X * Texture.Width);
                Glyphs[i].RealY = (int)Math.Round(Glyphs[i].Y * Texture.Height);
            }

            for (uint i = 0; i < Glyphs.LongLength; i++)
            {
                Glyphs[i].Texture = new Bitmap((int)FontSize, (int)FontSize);

                int X = Glyphs[i].RealX;
                int Y = Glyphs[i].RealY;

                for (int x = 0; x < FontSize; x++)
                    for (int y = 0; y < FontSize; y++)
                    {
                        Color Pixel = Texture.GetPixel(X + x, Y + y);
                        Glyphs[i].Texture.SetPixel(x, y, Pixel);
                    }

            }

            return Glyphs;
        }

        public void UpdatedGlyphs(Glyph[] Glyphs, Stream Output, bool CloseStream = true)
        {
            Font.Seek(0, 0);
            CopyBytes(Font, Output, HeaderNameSize + 4);

            byte[] DW = BitConverter.GetBytes(FontSize).Concat(BitConverter.GetBytes(Glyphs.Length)).ToArray();
            Output.Write(DW, 0, DW.Length);

            
            StructWriter Writer = new StructWriter(Output, (bool)BigEnddian);

            Bitmap Texture = this.Texture.Clone(new Rectangle(Point.Empty, new Size(this.Texture.Width, this.Texture.Height)), PixelFormat.Format32bppArgb);

            for (int i = 0; i < Glyphs.Length; i++){
                
                byte[] Buff = Encoding.UTF8.GetBytes(Glyphs[i].Char.ToString());
                Buff = new byte[4 - Buff.Length].Concat(Buff).ToArray();
                
                if (!(bool)BigEnddian)
                    Buff = Buff.Reverse().ToArray();

                Glyphs[i].UTF8 = BitConverter.ToUInt32(Buff, 0);



                Glyphs[i].X = (float)Glyphs[i].RealX / Texture.Width;
                Glyphs[i].Y = (float)Glyphs[i].RealY / Texture.Height;
                
                Writer.WriteStruct(ref Glyphs[i]);
            }

            Writer.Flush();

            if (SteamVer.Value) {
                Output.WriteByte(0);
                while (Output.Position % 4 != 0)
                    Output.WriteByte(0);
            }

            long NewTextPos = Output.Position;

            for (int i = 0; i < Glyphs.LongLength; i++)
            {
                var Glyph = Glyphs[i];
                
                int X = Glyph.RealX;
                int Y = Glyph.RealY;


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
                CopyBytes(Font, TextData, TexHeaderSize - 8);

                DW = BitConverter.GetBytes(Texture.Height * Texture.Width * 4).ToArray();
                TextData.Write(DW, 0, DW.Length);

                DW = BitConverter.GetBytes((ushort)Texture.Width).Concat(BitConverter.GetBytes((ushort)Texture.Height)).ToArray();
                TextData.Write(DW, 0, DW.Length);

                BinaryWriter pWriter = new BinaryWriter(TextData);
                for (int y = 0; y < Texture.Height; y++)
                    for (int x = 0; x < Texture.Width; x++) {
                        var Pixel = Texture.GetPixel(x, y);
                        pWriter.Write(Pixel.ToArgb());
                    }
            }
            else
            {
                long Reaming = (Font.Position - TexturePos) + 0x1CL - 8;
                CopyBytes(Font, Output, Reaming);

                Texture.Save(TextData, ImageFormat.Png);

                DW = BitConverter.GetBytes(TextData.Length).ToArray();
                Output.Write(DW, 0, DW.Length);

                
                //Maybe the code is working, but require check, if the PS3/PSV ver has the Width/Height in the header
                //since the texture is an png, while Steam ver is RGBA
                //if don't have width/height remove the - 8 in the reaming and put -4 as well
                throw new NotImplementedException();

                
                DW = BitConverter.GetBytes((ushort)Texture.Width).Concat(BitConverter.GetBytes((ushort)Texture.Height)).ToArray();
                Output.Write(DW, 0, DW.Length);
            }

            TextData.Seek(0, 0);
            CopyBytes(TextData, Output, TextData.Length);

            uint SecLen = (uint)(TextData.Length + (SteamVer.Value ? 0 : TexHeaderSize));
            while (SecLen % (SteamVer.Value ? 0x10 : 0x4) != 0)
                SecLen++;

            
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
    }


    public struct Glyph
    {
        public uint UTF8;
        public float X;
        public float Y;

        [Ignore]
        public int RealX;
        [Ignore]
        public int RealY;
        
        [Ignore]
        public char Char;
        
        [Ignore]
        public Bitmap Texture;
    }
}
