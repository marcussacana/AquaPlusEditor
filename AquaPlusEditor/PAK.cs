using AdvancedBinary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AquaPlusEditor
{
    public static class PAK
    {
        public static Entry[] Open(Stream Packget) => Open(Packget, null);
        public static Entry[] Open(Stream Packget, bool? BigEnddian) {
            if (BigEnddian == null) {
                Packget.Seek(8, 0);

                byte[] Buff = new byte[4];
                Packget.Read(Buff, 0, Buff.Length);
                int Len = BitConverter.ToInt32(Buff, 0);

                if (Len < 0 || Len > Packget.Length)
                    BigEnddian = true;
                else
                    BigEnddian = false;
            }

            Packget.Seek(0, 0);
            StructReader Reader = new StructReader(Packget, (bool)BigEnddian);
            Reader.Seek(0x8, 0);
            uint SectionLen = Reader.ReadRawType(Const.UINT32);
            uint Count = Reader.ReadRawType(Const.UINT32) / 4;
            Reader.Seek(0xC, 0);

            Section Section = new Section();
            Section.Data = Reader.ReadBytes((int)SectionLen - 0xC);


            Entry[] Entries = new Entry[Count];
            for (uint i = 0; i < Entries.Length; i++) {
                Entries[i] = new Entry();
                Entries[i].Filename = GetStringAt(Section.Data, Section[i, (bool)BigEnddian]);
            }

            while (Reader.BaseStream.Position % 4 != 0)
                Reader.ReadByte();

            Reader.Seek(8, SeekOrigin.Current);
            SectionLen = Reader.ReadRawType(Const.UINT32) + 4;
            Reader.ReadUInt32();//entrycount
            Section.Data = Reader.ReadBytes((int)SectionLen);

            for (uint i = 0; i < Entries.Length; i++) {
                VirtStream Stream = new VirtStream(Packget, Section[(i*2), (bool)BigEnddian], Section[(i * 2)+1, (bool)BigEnddian]);
                Entries[i].Content = Stream;
            }

            return Entries;
        }

        public static void Save(Stream Output, Entry[] Content, bool BigEnddian, bool CloseStreams = true) {
            StructWriter Writer = new StructWriter(Output, BigEnddian);
            Writer.Write(0x656D616E656C6946u);//"Filename"
            Writer.Write(0);//SecEnd

            long BasePos = (uint)Content.LongLength * 4;
            List<byte> NameBuffer = new List<byte>();
            foreach (Entry Entry in Content) {
                Writer.WriteRawType(Const.UINT32, (uint)(BasePos + NameBuffer.Count));
                NameBuffer.AddRange(Encoding.UTF8.GetBytes(Entry.Filename + "\x0"));
            }

            Writer.Write(NameBuffer.ToArray(), 0, NameBuffer.Count);

            long Pos = Writer.BaseStream.Position;
            Writer.Seek(0x8, 0);
            Writer.WriteRawType(Const.UINT32, (uint)Pos);
            Writer.Seek(Pos, 0);


            while (Writer.BaseStream.Position % 4 != 0)
                Writer.Write((byte)0x00);

            long PackStart = Writer.BaseStream.Position;
            Writer.Write(0x202020206B636150u);//"Pack    "
            Pos = Writer.BaseStream.Position;
            Writer.Write(0);//SecEnd

            Writer.WriteRawType(Const.UINT32, (uint)Content.LongLength);

            BasePos = Writer.BaseStream.Position + (Content.LongLength * 8);
            while (BasePos % 0x100 != 0)
                BasePos++;

            foreach (Entry Entry in Content) {
                Writer.WriteRawType(Const.UINT32, (uint)BasePos);
                Writer.WriteRawType(Const.UINT32, (uint)Entry.Content.Length);
                BasePos += Entry.Content.Length;
                while (BasePos % 0x100 != 0)
                    BasePos++;
            }

            long Pos2 = Writer.BaseStream.Position;
            Writer.Seek(Pos, 0);
            Writer.WriteRawType(Const.UINT32, (uint)(Pos2 - PackStart));
            Writer.Seek(Pos2, 0);

            foreach (Entry Entry in Content) {
                while (Writer.BaseStream.Position % 0x100 != 0)
                    Writer.Write((byte)0x00);

                Entry.Content.CopyTo(Writer.BaseStream);
                if (CloseStreams)
                    Entry.Content.Close();
            }

            Writer.Close();
            Output?.Close();
        }

        private static string GetStringAt(byte[] Data, uint At) {
            List<byte> Buffer = new List<byte>();
            while (Data[At + Buffer.Count] != 0x00)
                Buffer.Add(Data[At + Buffer.Count]);

            return Encoding.UTF8.GetString(Buffer.ToArray());
        }
    }


    internal struct Section {
        public byte[] Data;

        public uint this[uint ID, bool BigEnd] { get {
                byte[] Arr = new byte[4];

                for (uint i = 0; i < Arr.Length; i++)
                    Arr[i] = Data[i + (ID * 4)];

                if (BigEnd)
                    Arr = Arr.Reverse().ToArray();

                return BitConverter.ToUInt32(Arr, 0);
            }
            set {
                uint Index = ID * 4;
                if (Index + 4 > Data.Length) {
                    byte[] tmp = new byte[Index + 4];
                    Data.CopyTo(tmp, 0);
                    Data = tmp;
                }

                byte[] DW = BitConverter.GetBytes(ID);
                if (BigEnd)
                    DW = DW.Reverse().ToArray();

                for (uint i = 0; i < DW.Length; i++)
                    Data[Index + i] = DW[i];
            }

        }
    }
    public struct Entry {
        public string Filename;
        public Stream Content;
    }
}
