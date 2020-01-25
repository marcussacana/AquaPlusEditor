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
        const uint NameSize = 8;
        const uint ExNameSize = 12;
        public static Entry[] Open(Stream Packget) => Open(Packget, null, null);
        public static Entry[] Open(Stream Package, bool? BigEnddian, bool? SteamVersion) {
            if (SteamVersion == null) {
                Package.Seek(NameSize, 0);
                byte[] Buff = new byte[4];
                Package.Read(Buff, 0, Buff.Length);
                uint DW = BitConverter.ToUInt32(Buff, 0);

                SteamVersion = DW == 0x20202020;
            }
            
            uint HeaderSize = (SteamVersion.Value ? ExNameSize : NameSize) + 4;

            if (BigEnddian == null) {
                Package.Seek((SteamVersion.Value ? ExNameSize : NameSize), 0);

                byte[] Buff = new byte[4];
                Package.Read(Buff, 0, Buff.Length);
                int Len = BitConverter.ToInt32(Buff, 0);

                if (Len < 0 || Len > Package.Length)
                    BigEnddian = true;
                else
                    BigEnddian = false;
            }

            Package.Seek(0, 0);
            StructReader Reader = new StructReader(Package, (bool)BigEnddian);
            Reader.Seek((SteamVersion.Value ? ExNameSize : NameSize), 0);
            uint SectionLen = Reader.ReadRawType(Const.UINT32);
            uint Count = Reader.ReadRawType(Const.UINT32) / 4;
            Reader.Seek(-4, SeekOrigin.Current);

            SectionLen -= HeaderSize;

            while (SteamVersion.Value && SectionLen % 0x10 != 0)
                SectionLen++;

            Section Section = new Section();
            Section.Data = Reader.ReadBytes((int)SectionLen);


            Entry[] Entries = new Entry[Count];
            for (uint i = 0; i < Entries.Length; i++) {
                Entries[i] = new Entry();
                Entries[i].Filename = GetStringAt(Section.Data, Section[i, (bool)BigEnddian]);
            }

            while (Reader.BaseStream.Position %  (SteamVersion.Value ? 0x10 : 4) != 0)
                Reader.ReadByte();

            Reader.Seek((SteamVersion.Value ? ExNameSize : NameSize), SeekOrigin.Current);
            SectionLen = Reader.ReadRawType(Const.UINT32) + 4;
            Reader.ReadUInt32();//entrycount

            if (SteamVersion.Value)
                SectionLen -= HeaderSize + 4;

            while (SteamVersion.Value && (SectionLen + (SteamVersion.Value ? 4 : 0)) % 0x10 != 0)
                SectionLen++;

            Section.Data = Reader.ReadBytes((int)SectionLen);

            for (uint i = 0; i < Entries.Length; i++) {
                VirtStream Stream = new VirtStream(Package, Section[(i*2), (bool)BigEnddian], Section[(i * 2)+1, (bool)BigEnddian]);
                Entries[i].Content = Stream;
            }

            return Entries;
        }

        public static void Save(Stream Output, Entry[] Content, bool BigEnddian, bool SteamVer, bool CloseStreams = true) {
            StructWriter Writer = new StructWriter(Output, BigEnddian);
            Writer.Write(0x656D616E656C6946u);//"Filename"

            if (SteamVer)
                Writer.Write(0x20202020);

            Writer.Write(0);//Section Length

            long BasePos = (uint)Content.LongLength * 4;
            List<byte> NameBuffer = new List<byte>();
            foreach (Entry Entry in Content) {
                Writer.WriteRawType(Const.UINT32, (uint)(BasePos + NameBuffer.Count));
                NameBuffer.AddRange(Encoding.UTF8.GetBytes(Entry.Filename + "\x0"));
            }

            Writer.Write(NameBuffer.ToArray(), 0, NameBuffer.Count);

            long Pos = Writer.BaseStream.Position;
            Writer.Seek(SteamVer ? 12 : 8, 0);
            Writer.WriteRawType(Const.UINT32, (uint)Pos);
            Writer.Seek(Pos, 0);


            while (Writer.BaseStream.Position % (SteamVer ? 0x10 : 4) != 0)
                Writer.Write((byte)0x00);

            long PackStart = Writer.BaseStream.Position;
            Writer.Write(0x202020206B636150u);//"Pack    "
            if (SteamVer)
                Writer.Write(0x20202020);

            Pos = Writer.BaseStream.Position;
            Writer.Write(0);//Section Length

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
