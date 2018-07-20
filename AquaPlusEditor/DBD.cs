#define Utawarerumono
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AquaPlusEditor {
    public class DBD {

        uint[] Offsets;
        bool? IsBigEnddian = null;
        byte[] Script;

        public DBD(byte[] Script) => this.Script = Script;
        public DBD(byte[] Script, bool BigEnddian) {
            IsBigEnddian = BigEnddian;
        }

        public string[] Import() {
            if (IsBigEnddian == null) {
                IsBigEnddian = false;
                IsBigEnddian = GetDW(0) > Script.Length;
            }
            List<bool> IsString = new List<bool>();
            uint Count = GetDW(0);
            uint? StrStart = GetStrTable(Count);

            if (StrStart == null)
                return new string[0];

            uint BlockLen = (uint)((StrStart - 4) / Count) / 4;


            for (uint i = 0; i < BlockLen; i++) {
                uint Val = GetDW((i * 4) + 4);

                bool IsAString = true;
                /*
                if (Val < StrStart)
                    IsAString = false;
                if (Val > Script.Length)
                    IsAString = false;
                if (IsAString && StrStart != Val && Script[Val - 1] != 0)
                    IsAString = false;
                */
                IsString.Add(IsAString);
            }
            List<uint> TablePos = new List<uint>(GetOffsets((uint)StrStart));
            List<uint> Offsets = new List<uint>();/*
            for (uint x = 0; x < Count; x++)
                for (int i = 0; i < BlockLen; i++) {
                    uint Ptr = x * (BlockLen * 4);
                    Ptr += (uint)(i * 4) + 4;;
                    
                    if (true || IsString[i]){
                    */
            for (uint i = 4; i < StrStart; i += 4) {
                uint Ptr = i;
                uint DW = GetDW(Ptr);
                if (!TablePos.Contains(DW))
                    continue;

                if (!(DW >= StrStart && DW < Script.LongLength)) {
                    continue;
                }

                Offsets.Add(Ptr);

            }
        

            this.Offsets = Offsets.ToArray();

            string[] Strings = new string[Offsets.Count];
            for (uint i = 0; i < Offsets.Count; i++) {
                uint Offset = GetDW(this.Offsets[i]);
                Strings[i] = GetStr(Offset);
            }

            return Strings;
        }

        public byte[] Export(string[] Strings) {
            uint StrStart = GetDW(Offsets.First());

            List<byte> StrTable = new List<byte>();
            byte[] Script = new byte[StrStart];
            for (uint i = 0; i < Script.Length; i++)
                Script[i] = this.Script[i];

            for (uint i = 0; i < Strings.Length; i++) {
                uint Offset = (uint)(Script.LongLength + StrTable.LongCount());
                SetDW(Script, Offsets[i], Offset);
                StrTable.AddRange(Encoding.UTF8.GetBytes(Strings[i] + '\x0'));
            }


            return Script.Concat(StrTable).ToArray();
        }

        private uint[] GetOffsets(uint StrTablePos) {
            List<uint> Offsets = new List<uint>(new uint[] { StrTablePos });
            for (uint i = StrTablePos + 1; i < Script.LongLength - 1; i++) {
                if (Script[i] == 0x0)
                    Offsets.Add(i + 1);
            }

            return Offsets.ToArray();
        }
        private uint? GetStrTable(uint Count) {
            try {
                for (uint i = (uint)(Script.LongLength - (Script.LongLength % 4)) - 4; i >= 4; i -= 4) {                   
                    if (Script[i] == 0xFF)
                        break;
                    if (Script[i - 1] != 0xFF && (Script[i - 1] >= 0x20 && Script[i - 1] != '\n'))
                        continue;
                    if (Script[i - 2] != 0xFF && (Script[i - 2] >= 0x20 && Script[i - 2] != '\n'))
                        continue;
                    if (Script[i] <= 0x20 && Script[i] != '\n' || Script[i] > 0xF0)
                        continue;

                    uint test = (uint)((i - 4) / Count) / 4;//fails if invalid

                    uint x = i;
                    while (Script[x] != 0x00)
                        x++;
                    if (Script[x + 1] == 0x00)
                        continue;
                    x = i;
                    while (Script[--x] == 0x00)
                        continue;
                    if (!(Script[x] <= 0x20 && Script[x] != '\n' || Script[x] > 0xF0))
                        continue;


                        return i;
                }
            } catch { }
            try {
                for (uint i = 0; i < Script.Length; i++) {
                    uint Index = (i * Count) + 4;
                    Index = GetDW(Index);

                    if (Index % 4 != 0)
                        continue;

                    if (Index > Script.Length)
                        continue;

                    string str = GetStr(Index);
                    if ((from c in str where c < 0x20 && c != '\n' select c).Count() != 0)
                        continue;

                    if (Script[Index] < '\n' && Script[Index + 1] < '\n')
                        continue;

                    return Index;
                }
            } catch { }
            return null;
        }
        private string GetStr(uint Offset) {
            List<byte> Buffer = new List<byte>();
            while (Script[Offset + Buffer.Count] != 0x00)
                Buffer.Add(Script[Offset + Buffer.Count]);
#if Utawarerumono
            return Encoding.UTF8.GetString(Buffer.ToArray()).Replace("鵼", "…");//WTF
#else
            return Encoding.UTF8.GetString(Buffer.ToArray());
#endif
        }


        private uint GetDW(uint At) {
            byte[] Buff = new byte[4];
            for (uint i = 0; i < Buff.Length; i++)
                Buff[i] = Script[i + At];

            if ((bool)IsBigEnddian)
                Buff = Buff.Reverse().ToArray();

            return BitConverter.ToUInt32(Buff, 0);
        }

        private void SetDW(byte[] Script, uint At, uint Value) {
            byte[] Buff = BitConverter.GetBytes(Value);

            if ((bool)IsBigEnddian)
                Buff = Buff.Reverse().ToArray();

            Buff.CopyTo(Script, At);
        }
    }
}
