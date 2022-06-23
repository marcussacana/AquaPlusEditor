#define Utawarerumono
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AquaPlusEditor {
    public class CSTS {
        const uint Signature = 0x43535453;
        bool IsBigEnddian = false;
       
        public List<uint> OffPos;
        byte[] Script;

        private byte[] EditorSignature = new byte[] { 0x00, 0x45, 0x64, 0x69, 0x74, 0x65, 0x64, 0x20, 0x57, 0x69, 0x74, 0x68, 0x20, 0x41, 0x71, 0x75, 0x61, 0x50, 0x6C, 0x75, 0x73, 0x45, 0x64, 0x69, 0x74, 0x6F, 0x72, 0x00 };
        public CSTS(byte[] Script) {
            this.Script = Script;
            uint Sig = GetDW(0);
            if (Signature != Sig)
                IsBigEnddian = true;
            Sig = GetDW(0);
            if (Signature != Sig)
                throw new Exception("Invalid Script");
        }

        public string[] Import() {
            OffPos = new List<uint>();
            uint StrStart = GetWritePos();
            for (uint i = StrStart - 3; i >= 0; i--) {
                ushort w = GetW(i);
                if (w != 0x31)
                    continue;
                if (Script[i + 5] == 0 || Script[i + 4] != 0)
                    break;
                StrStart = i + 5;
                break;
            }
            bool ForceTable = StrStart != Script.LongLength;
            for (uint i = 0; i < Script.LongLength - 2; i++) {
                ushort Word = GetW(i);
                if (!(Word >= 0x50 && Word <= 0x53))//0x40 contains strings too
                    continue;
                uint OffPos = i + 2;
                uint Offset = GetDW(OffPos);
                if (Offset > Script.Length)
                    continue;
                if (Offset < i)
                    continue;
                if (StrStart != Script.Length) {
                    if (Script[Offset - 1] != 0x00)
                        continue;
                }
                if (Script[Offset] == 0x00)
                    continue;
                i += 5;
                if (Offset < StrStart) {
                    if (ForceTable)
                        continue;

                    if (StrStart == Script.Length)
                    {
                        if (Offset + 1 >= Script.Length || !IsUTF8(Script[Offset]) || !IsUTF8(Script[Offset + 1]))
                            continue;
                        
                        StrStart = Offset;
                    }
                    else /*if (Offset + 50 >= StrStart) // hacky
                        StrStart = Offset;
                    else*/ 
                    if (Offset + 1 < Script.Length){
                        byte CharA = Script[Offset];
                        byte CharB = Script[Offset + 1];
                        //https://en.wikipedia.org/wiki/UTF-8
                        if (IsUTF8(CharA) && IsUTF8(CharB)) {
                            StrStart = Offset;
                        } else
                            continue;
                    }
                } else if (Offset < StrStart)
                    continue;

                this.OffPos.Add(OffPos);
            }

            List<string> Strings = new List<string>();
            foreach (uint OffPos in this.OffPos) {
                Strings.Add(GetStr(OffPos));
            }

            return Strings.ToArray();
        }

        bool IsUTF8(byte Byte)
        {
            return Byte >> 4 == 0xE && ((Byte & 0xF) >= 0x3 && (Byte & 0xF) <= 8) || Byte == 0xC3 || (Byte >= 0x41 && Byte <= 0x7A);
        }

        public byte[] Export(string[] Strings) {
            List<uint> Offsets = new List<uint>();
            List<byte> StrTable = new List<byte>(EditorSignature);
            uint WritePos = GetWritePos();
            for (int i = 0; i < Strings.Length; i++) {
                string Str = Strings[i];
                Offsets.Add((uint)(StrTable.Count + WritePos));
                StrTable.AddRange(Encoding.UTF8.GetBytes(Str));
                StrTable.Add(0x00);
            }

            byte[] Output = new byte[WritePos];
            for (uint i = 0; i < Output.LongLength; i++) {
                Output[i] = Script[i];
            }

            for (int i = 0; i < OffPos.Count; i++) {
                uint OffPos = this.OffPos[i];
                if (WritePos == Script.LongLength)
                    ClearStr(Output, GetDW(OffPos));
                //if (i > 0 && StrTable[((int)Offsets[i] - Output.Length)-1] != 0x00)
                //    System.Diagnostics.Debugger.Break();
                SetDW(Output, OffPos, Offsets[i]);
            }

            return Output.Concat(StrTable).ToArray();
        }

        private uint GetWritePos() {
            for (uint i = 0; i < Script.Length; i++) {
                if (EqualsAt(EditorSignature, i))
                    return i;
            }

            return (uint)Script.LongLength;
        }

        private bool EqualsAt(byte[] Arr, uint At) {
            if (Arr.Length + At > Script.Length)
                return false;

            for (uint i = 0; i < Arr.Length; i++) {
                if (Arr[i] != Script[At + i])
                    return false;
            }

            return true;
        }

        private void ClearStr(byte[] Script, uint At) {
            uint Len = 0;
            while (Script[At + Len] != 0x00)
                Script[At + Len++] = 0x00;
        }

        private string GetStr(uint OffPos) {
            uint Offset = GetDW(OffPos);

            List<byte> Buffer = new List<byte>();
            while (Script[Offset + Buffer.Count] != 0x00)
                Buffer.Add(Script[Offset + Buffer.Count]);
#if Utawarerumono
            return Encoding.UTF8.GetString(Buffer.ToArray()).Replace("鵼", "…");//WTF
#else
            return Encoding.UTF8.GetString(Buffer.ToArray());
#endif
        }

        private ushort GetW(uint At) {
            byte[] Buff = new byte[2];
            for (uint i = 0; i < Buff.Length; i++)
                Buff[i] = Script[i + At];

            if (IsBigEnddian)
                Buff = Buff.Reverse().ToArray();

            return BitConverter.ToUInt16(Buff, 0);
        }
        private uint GetDW(uint At) {
            byte[] Buff = new byte[4];
            for (uint i = 0; i < Buff.Length; i++)
                Buff[i] = Script[i + At];

            if (IsBigEnddian)
                Buff = Buff.Reverse().ToArray();

            return BitConverter.ToUInt32(Buff, 0);
        }

        private void SetDW(byte[] Script, uint At, uint Value) {
            byte[] Buff = BitConverter.GetBytes(Value);

            if (IsBigEnddian)
                Buff = Buff.Reverse().ToArray();

            Buff.CopyTo(Script, At);
        }
    }
}
