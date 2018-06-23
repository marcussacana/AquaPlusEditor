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
        uint Unk;
        public TEX(Stream Texture) {
            this.Texture = Texture;
        }
        public TEX(Stream Texture, bool BigEnddian) {
            this.Texture = Texture;
            IsBigEnddian = BigEnddian;
        }

        public Stream Open() {
            if (IsBigEnddian == null) {
                byte[] DW = new byte[4];
                Texture.Seek(8, 0);
                Texture.Read(DW, 0, DW.Length);
                IsBigEnddian = BitConverter.ToUInt32(DW, 0) > Texture.Length;
            }
            Texture.Seek(0, 0);
            StructReader Reader = new StructReader(Texture, (bool)IsBigEnddian);
            Reader.Seek(0xC, 0);
            Unk = Reader.ReadRawType(Const.UINT32);
            uint PNGLen = Reader.ReadRawType(Const.UINT32);
            uint Width = Reader.ReadRawType(Const.UINT32);
            uint Height = Reader.ReadRawType(Const.UINT32);

            TexSize = new Size((int)Width, (int)Height);
            return new VirtStream(Texture, 0x1C, PNGLen);
        }
        public Bitmap Decode() {
            Bitmap Output = Image.FromStream(Open()) as Bitmap;
            TexSize = Output.Size;
            return Output;
        }

        public void Encode(Bitmap Texture, Stream Output, bool CloseStreams = true) {
            StructWriter Writer = new StructWriter(Output, (bool)IsBigEnddian);
            Writer.Write(0x2065727574786554);
            Writer.Write(0);//0x8, blocklen
            Writer.WriteRawType(Const.UINT32, Unk);
            Writer.Write(0);//0x10, pnglen
            Writer.WriteRawType(Const.UINT32, (uint)TexSize.Width);
            Writer.WriteRawType(Const.UINT32, (uint)TexSize.Height);
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

            Reader.BaseStream.CopyTo(Reader.BaseStream);
            Writer.Flush();

            if (CloseStreams) {
                Writer.Close();
                Output?.Close();
            }
        }
    }
}
