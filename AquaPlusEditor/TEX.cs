using AdvancedBinary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace AquaPlusEditor {
    public class TEX {

        Size TexSize;
        bool? IsBigEnddian = null;
        Stream Texture;
        uint Flags;

        bool Tiled => ((Flags >> 8 * 2) & 0xFF) == 0x3;
        public TEX(Stream Texture) {
            this.Texture = Texture;
        }
        public TEX(Stream Texture, bool BigEnddian) {
            this.Texture = Texture;
            IsBigEnddian = BigEnddian;
        }

        Stream Open() {
            if (IsBigEnddian == null) {
                byte[] DW = new byte[4];
                Texture.Seek(8, 0);
                Texture.Read(DW, 0, DW.Length);
                IsBigEnddian = BitConverter.ToUInt32(DW, 0) > Texture.Length;
            }
            Texture.Seek(0, 0);
            StructReader Reader = new StructReader(Texture, (bool)IsBigEnddian);
            Reader.Seek(0xC, 0);
            Flags = Reader.ReadRawType(Const.UINT32);
            uint PNGLen = Reader.ReadRawType(Const.UINT32);
            uint Width = Reader.ReadRawType(Const.UINT32);
            uint Height = Reader.ReadRawType(Const.UINT32);

            TexSize = new Size((int)Width, (int)Height);
            return new VirtStream(Texture, 0x1C, PNGLen);
        }

        public Bitmap Decode() {
            Bitmap Output = Image.FromStream(Open()) as Bitmap;
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
            Writer.Write(0);//0x8, blocklen
            Writer.WriteRawType(Const.UINT32, Flags);
            Writer.Write(0);//0x10, pnglen
            Writer.WriteRawType(Const.UINT32, (uint)TexSize.Width);
            Writer.WriteRawType(Const.UINT32, (uint)TexSize.Height);

            if (Tiled)
                Texture = Retile(Texture);

            Texture.Save(Writer.BaseStream, ImageFormat.Png);
            uint BlockLen = (uint)Writer.BaseStream.Position;
            uint PngLen = BlockLen - 0x1C;
            Writer.Seek(0x8, 0);
            Writer.WriteRawType(Const.UINT32, BlockLen);
            Writer.Seek(0x10, 0);
            Writer.WriteRawType(Const.UINT32, PngLen);

            Writer.Seek(BlockLen, 0);
            while (Writer.BaseStream.Position % 4 != 0)
                Writer.Write((byte)0x0);

            StructReader Reader = new StructReader(this.Texture, (bool)IsBigEnddian);
            Reader.Seek(0x8, 0);
            Reader.Seek(Reader.ReadRawType(Const.UINT32), 0);

            while (Reader.BaseStream.Position % 4 != 0)
                Reader.ReadByte();
            
            Reader.BaseStream.CopyTo(Writer.BaseStream);
            Writer.Flush();

            if (CloseStreams) {
                Writer.Close();
                Output?.Close();
            }
        }
    }
}
