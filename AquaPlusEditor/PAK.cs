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
        public static Entry[] Open(Stream Package, bool? BigEndian, bool? SteamVersion)
        {
            if (SteamVersion == null)
            {
                Package.Seek(NameSize, 0);
                byte[] Buff = new byte[4];
                Package.Read(Buff, 0, Buff.Length);
                uint DW = BitConverter.ToUInt32(Buff, 0);

                SteamVersion = DW == 0x20202020;
            }

            uint HeaderSize = (SteamVersion.Value ? ExNameSize : NameSize) + 4;

            if (BigEndian == null)
            {
                Package.Seek((SteamVersion.Value ? ExNameSize : NameSize), 0);

                byte[] Buff = new byte[4];
                Package.Read(Buff, 0, Buff.Length);
                int Len = BitConverter.ToInt32(Buff, 0);

                if (Len < 0 || Len > Package.Length)
                    BigEndian = true;
                else
                    BigEndian = false;
            }

            Package.Seek(0, 0);
            StructReader Reader = new StructReader(Package, (bool)BigEndian);

            object Section = SteamVersion.Value ? (object)new SteamSection() : new Section();
            ((dynamic)Section).Event = new FieldInvoke(SectionEvent);
            Reader.ReadStruct(SteamVersion.Value ? typeof(SteamSection) : typeof(Section), ref Section);

            byte[] Data = ((dynamic)Section).Data;

            Entry[] Entries = new Entry[(GetDWAt(Data, 0, BigEndian.Value) / 4)];

            for (uint i = 0; i < Entries.Length; i++)
            {
                Entries[i] = new Entry();
                uint Offset = GetDWAt(Data, i, BigEndian.Value);
                Entries[i].Filename = GetStringAt(Data, Offset);
            }

            Reader.BaseStream.Position = Data.LongLength + HeaderSize;

            while (Reader.BaseStream.Position % (SteamVersion.Value ? 8 : 4) != 0)
                Reader.ReadByte();

            Reader.ReadStruct(SteamVersion.Value ? typeof(SteamSection) : typeof(Section), ref Section);
            Data = ((dynamic)Section).Data;

            for (uint i = 0; i < Entries.Length; i++)
            {
                uint Offset = GetDWAt(Data, 1 + (i * 2) + 0, BigEndian.Value);
                uint Length = GetDWAt(Data, 1 + (i * 2) + 1, BigEndian.Value);

                VirtStream Stream = new VirtStream(Package, Offset, Length);
                Entries[i].Content = Stream;
            }

            return Entries;
        }

        public static void Save(Stream Output, Entry[] Content, bool BigEndian, bool SteamVer, bool CloseStreams = true)
        {
            uint HeaderSize = (SteamVer ? ExNameSize : NameSize) + 4;

            StructWriter Writer = new StructWriter(Output, BigEndian);
            object Section = SteamVer ? (object)new SteamSection() : new Section();
            ((dynamic)Section).Name = SteamVer ? "Filename    " : "Filename";
            ((dynamic)Section).Event = new FieldInvoke(SectionEvent);

            long BasePos = (uint)(Content.LongLength * 4);
            List<byte> OffsetBuffer = new List<byte>((int)BasePos);
            List<byte> NameBuffer = new List<byte>();
            foreach (Entry Entry in Content)
            {
                OffsetBuffer.Add((uint)(NameBuffer.Count + BasePos), BigEndian);
                NameBuffer.AddRange(Encoding.UTF8.GetBytes(Entry.Filename + "\x0"));
            }

            ((dynamic)Section).Data = OffsetBuffer.Concat(NameBuffer).ToArray();

            Writer.WriteStruct(SteamVer ? typeof(SteamSection) : typeof(Section), ref Section);

            while (Writer.BaseStream.Position % (SteamVer ? 8 : 4) != 0)
                Writer.Write((byte)0x00);

            long PackStart = Writer.BaseStream.Position;
            ((dynamic)Section).Name = SteamVer ? "Pack        " : "Pack    ";

            List<byte> OffsetTable = new List<byte>();

            OffsetTable.Add(Content.Length, BigEndian);

            BasePos = PackStart + HeaderSize + 4 + (Content.Length * 8);

            while (BasePos % (SteamVer ? 8 : 0x100) != 0)
                BasePos++;

            foreach (Entry Entry in Content)
            {
                OffsetTable.Add((uint)BasePos, BigEndian);
                OffsetTable.Add((uint)Entry.Content.Length, BigEndian);
                BasePos += Entry.Content.Length;

                while (!SteamVer && BasePos % 0x100 != 0)
                    BasePos++;
            }

            ((dynamic)Section).Data = OffsetTable.ToArray();
            Writer.WriteStruct(SteamVer ? typeof(SteamSection) : typeof(Section), ref Section);


            while (Writer.BaseStream.Position % (SteamVer ? 8 : 0x100) != 0)
                Writer.Write((byte)0);


            Writer.Flush();


            foreach (Entry Entry in Content)
            {
                Entry.Content.Position = 0;
                Entry.Content.CopyTo(Output);

                while (!SteamVer && Output.Position % 0x100 != 0)
                    Output.WriteByte(0);
            }

            while (SteamVer && Output.Position % 0x10 != 0)
                Output.WriteByte(0);

            Writer.Close();
            Output?.Close();
        }

        private unsafe static uint GetDWAt(byte[] Buffer, uint At, bool BigEndian)
        {
            fixed (void* Addr = &Buffer[0])
            {
                uint* dAddr = (uint*)Addr;
                uint DW = *(dAddr + At);
                if (BigEndian)
                {
                    byte[] tmp = BitConverter.GetBytes(DW);
                    Array.Reverse(tmp);
                    fixed (void* tAddr = &tmp[0])
                    {
                        DW = *(uint*)tAddr;
                    }
                }
                return DW;
            }
        }

        private static dynamic SectionEvent(Stream Stream, bool FromReader, bool BigEndian, dynamic This)
        {
            if (FromReader)
            {
                This.Data = new byte[This.Length - This.Name.Length - 4];
                Stream.Read(This.Data, 0, This.Data.Length);
            }
            else
            {
                Stream.Seek(-4, SeekOrigin.Current);
                var Buffer = BitConverter.GetBytes(This.Data.Length + This.Name.Length + 4);
                if (BigEndian)
                    Array.Reverse(Buffer);
                Stream.Write(Buffer, 0, 4);
                Stream.Write(This.Data, 0, This.Data.Length);
            }
            return This;
        }

        private static string GetStringAt(byte[] Data, uint At)
        {
            List<byte> Buffer = new List<byte>();
            while (Data[At + Buffer.Count] != 0x00)
                Buffer.Add(Data[At + Buffer.Count]);

            return Encoding.UTF8.GetString(Buffer.ToArray());
        }
    }

    static class Extensions
    {
        public static void Add(this List<byte> Buffer, uint Value, bool BigEndian)
        {
            byte[] Data = BitConverter.GetBytes(Value);
            if (BigEndian)
                Array.Reverse(Data, 0, Data.Length);
            Buffer.AddRange(Data);
        }

        public static void Add(this List<byte> Buffer, int Value, bool BigEndian)
        {
            byte[] Data = BitConverter.GetBytes(Value);
            if (BigEndian)
                Array.Reverse(Data, 0, Data.Length);
            Buffer.AddRange(Data);
        }
    }

    public struct Section
    {
        [FString(8)]
        public string Name;

        public uint Length;

        public Section(string Name = null)
        {
            this.Name = Name ?? string.Empty;
            Length = 0;
            Data = new byte[0];
            Event = null;
        }

        public FieldInvoke Event;

        [Ignore]
        public byte[] Data;
    }


    public struct SteamSection
    {
        [FString(12)]
        public string Name;

        public uint Length;

        public SteamSection(string Name = null)
        {
            this.Name = Name ?? string.Empty;
            Length = 0;
            Data = new byte[0];
            Event = null;
        }

        public FieldInvoke Event;

        [Ignore]
        public byte[] Data;
    }

    public struct Entry
    {
        public string Filename;
        public Stream Content;
    }
}