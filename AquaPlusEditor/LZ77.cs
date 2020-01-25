/*
* This file is part of libWiiSharp
* Copyright(C) 2009 Leathl
*
* libWiiSharp is free software: you can redistribute it and/or
* modify it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.

*
* libWiiSharp is distributed in the hope that it will be
* useful, but WITHOUT ANY WARRANTY; without even the implied warranty
* of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
 * GNU General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with this program.If not, see<http://www.gnu.org/licenses/>.
*/
 
//Lz77 (de-)compression algorithm based on gbalzss by Andre Perrot (Thanks!)

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace libWiiSharp {
    public class Lz77 {
        private static uint lz77Magic = 0x4c5a3737;
        private const int N = 4096;
        private const int F = 18;
        private const int threshold = 2;
        private int[] leftSon = new int[N + 1];
        private int[] rightSon = new int[N + 257];
        private int[] dad = new int[N + 1];
        private ushort[] textBuffer = new ushort[N + 17];
        private int matchPosition = 0, matchLength = 0;

        /// <summary>
        /// Lz77 Magic.
        /// </summary>
        public static uint Lz77Magic { get { return lz77Magic; } }

        #region Public Functions
        /// <summary>
        /// Checks whether a file is Lz77 compressed or not.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static bool IsLz77Compressed(string file) {
            return IsLz77Compressed(File.ReadAllBytes(file));
        }

        /// <summary>
        /// Checks whether a file is Lz77 compressed or not.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static bool IsLz77Compressed(byte[] file) {
            Headers.HeaderType h = Headers.DetectHeader(file);
            return (Shared.Swap(BitConverter.ToUInt32(file, (int)h)) == lz77Magic);
        }

        /// <summary>
        /// Checks whether a file is Lz77 compressed or not.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static bool IsLz77Compressed(Stream file) {
            Headers.HeaderType h = Headers.DetectHeader(file);
            byte[] temp = new byte[4];
            file.Seek((long)h, SeekOrigin.Begin);
            file.Read(temp, 0, temp.Length);
            return (Shared.Swap(BitConverter.ToUInt32(temp, 0)) == lz77Magic);
        }



        /// <summary>
        /// Compresses a file using the Lz77 algorithm.
        /// </summary>
        /// <param name="inFile"></param>
        /// <param name="outFile"></param>
        public void Compress(string inFile, string outFile) {
            Stream compressedFile;

            using (FileStream fsIn = new FileStream(inFile, FileMode.Open))
                compressedFile = compress(fsIn);

            byte[] output = new byte[compressedFile.Length];
            compressedFile.Read(output, 0, output.Length);

            if (File.Exists(outFile)) File.Delete(outFile);

            using (FileStream fs = new FileStream(outFile, FileMode.Create))
                fs.Write(output, 0, output.Length);
        }

        /// <summary>
        /// Compresses the byte array using the Lz77 algorithm.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public byte[] Compress(byte[] file) {
            return ((MemoryStream)compress(new MemoryStream(file))).ToArray();
        }

        /// <summary>
        /// Compresses the stream using the Lz77 algorithm.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public Stream Compress(Stream file) {
            return compress(file);
        }

        /// <summary>
        /// Decompresses a file using the Lz77 algorithm.
        /// </summary>
        /// <param name="inFile"></param>
        /// <param name="outFile"></param>
        public void Decompress(string inFile, string outFile) {
            Stream compressedFile;

            using (FileStream fsIn = new FileStream(inFile, FileMode.Open))
                compressedFile = decompress(fsIn);

            byte[] output = new byte[compressedFile.Length];
            compressedFile.Read(output, 0, output.Length);

            if (File.Exists(outFile)) File.Delete(outFile);

            using (FileStream fs = new FileStream(outFile, FileMode.Create))
                fs.Write(output, 0, output.Length);
        }

        /// <summary>
        /// Decompresses the byte array using the Lz77 algorithm.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public byte[] Decompress(byte[] file) {
            return ((MemoryStream)decompress(new MemoryStream(file))).ToArray();
        }

        public Stream Decompress(Stream file) {
            return decompress(file);
        }
        #endregion

        #region Private Functions
        private Stream decompress(Stream inFile) {
            if (!Lz77.IsLz77Compressed(inFile)) return inFile;
            inFile.Seek(0, SeekOrigin.Begin);

            int i, j, k, r, c, z;
            uint flags, decompressedSize, currentSize = 0;

            Headers.HeaderType h = Headers.DetectHeader(inFile);

            byte[] temp = new byte[8];
            inFile.Seek((int)h, SeekOrigin.Begin);
            inFile.Read(temp, 0, 8);

            if (Shared.Swap(BitConverter.ToUInt32(temp, 0)) != lz77Magic) { inFile.Dispose(); throw new Exception("Invaild Magic!"); }
            if (temp[4] != 0x10) { inFile.Dispose(); throw new Exception("Unsupported Compression Type!"); }

            decompressedSize = (BitConverter.ToUInt32(temp, 4)) >> 8;

            for (i = 0; i < N - F; i++) textBuffer[i] = 0xdf;
            r = N - F; flags = 7; z = 7;

            MemoryStream outFile = new MemoryStream();
            while (true) {
                flags <<= 1;
                z++;

                if (z == 8) {
                    if ((c = inFile.ReadByte()) == -1) break;

                    flags = (uint)c;
                    z = 0;
                }

                if ((flags & 0x80) == 0) {
                    if ((c = inFile.ReadByte()) == inFile.Length - 1) break;
                    if (currentSize < decompressedSize) outFile.WriteByte((byte)c);

                    textBuffer[r++] = (byte)c;
                    r &= (N - 1);
                    currentSize++;
                } else {
                    if ((i = inFile.ReadByte()) == -1) break;
                    if ((j = inFile.ReadByte()) == -1) break;

                    j = j | ((i << 8) & 0xf00);
                    i = ((i >> 4) & 0x0f) + threshold;
                    for (k = 0; k <= i; k++) {
                        c = textBuffer[(r - j - 1) & (N - 1)];
                        if (currentSize < decompressedSize) outFile.WriteByte((byte)c); textBuffer[r++] = (byte)c; r &= (N - 1); currentSize++;
                    }
                }
            }
            outFile.Position = 0;
            return outFile;
        }

        private Stream compress(Stream inFile) {
            if (Lz77.IsLz77Compressed(inFile)) return inFile;
            inFile.Seek(0, SeekOrigin.Begin);

            int textSize = 0;
            int codeSize = 0;

            int i, c, r, s, length, lastMatchLength, codeBufferPointer, mask;
            int[] codeBuffer = new int[17];

            uint fileSize = ((Convert.ToUInt32(inFile.Length)) << 8) + 0x10;
            MemoryStream outFile = new MemoryStream();

            outFile.Write(BitConverter.GetBytes(Shared.Swap(lz77Magic)), 0, 4);
            outFile.Write(BitConverter.GetBytes(fileSize), 0, 4);

            InitTree();
            codeBuffer[0] = 0;
            codeBufferPointer = 1;
            mask = 0x80;
            s = 0;
            r = N - F;

            for (i = s; i < r; i++) textBuffer[i] = 0xffff;

            for (length = 0; length < F && (c = (int)inFile.ReadByte()) != -1; length++)
                textBuffer[r + length] = (ushort)c;

            if ((textSize = length) == 0) return inFile;

            for (i = 1; i <= F; i++) InsertNode(r - i);
            InsertNode(r);

            do {
                if (matchLength > length) matchLength = length;

                if (matchLength <= threshold) {
                    matchLength = 1;
                    codeBuffer[codeBufferPointer++] = textBuffer[r];
                } else {
                    codeBuffer[0] |= mask;

                    codeBuffer[codeBufferPointer++] = (char)
                        (((r - matchPosition - 1) >> 8) & 0x0f) |
                        ((matchLength - (threshold + 1)) << 4);

                    codeBuffer[codeBufferPointer++] = (char)((r - matchPosition - 1) & 0xff);
                }

                if ((mask >>= 1) == 0) {
                    for (i = 0; i < codeBufferPointer; i++)
                        outFile.WriteByte((byte)codeBuffer[i]);

                    codeSize += codeBufferPointer;
                    codeBuffer[0] = 0; codeBufferPointer = 1;
                    mask = 0x80;
                }

                lastMatchLength = matchLength;
                for (i = 0; i < lastMatchLength && (c = (int)inFile.ReadByte()) != -1; i++) {
                    DeleteNode(s);

                    textBuffer[s] = (ushort)c;
                    if (s < F - 1) textBuffer[s + N] = (ushort)c;
                    s = (s + 1) & (N - 1); r = (r + 1) & (N - 1);

                    InsertNode(r);
                }

                while (i++ < lastMatchLength) {
                    DeleteNode(s);

                    s = (s + 1) & (N - 1); r = (r + 1) & (N - 1);
                    if (--length != 0) InsertNode(r);
                }
            } while (length > 0);


            if (codeBufferPointer > 1) {
                for (i = 0; i < codeBufferPointer; i++) outFile.WriteByte((byte)codeBuffer[i]);
                codeSize += codeBufferPointer;
            }

            if (codeSize % 4 != 0)
                for (i = 0; i < 4 - (codeSize % 4); i++)
                    outFile.WriteByte(0x00);

            return outFile;
        }

        private void InitTree() {
            int i;
            for (i = N + 1; i <= N + 256; i++) rightSon[i] = N;
            for (i = 0; i < N; i++) dad[i] = N;
        }

        private void InsertNode(int r) {
            int i, p, cmp;
            cmp = 1;
            p = N + 1 + (textBuffer[r] == 0xffff ? 0 : (int)textBuffer[r]);
            rightSon[r] = leftSon[r] = N; matchLength = 0;

            for (; ; )
            {
                if (cmp >= 0) {
                    if (rightSon[p] != N) p = rightSon[p];
                    else { rightSon[p] = r; dad[r] = p; return; }
                } else {
                    if (leftSon[p] != N) p = leftSon[p];
                    else { leftSon[p] = r; dad[r] = p; return; }
                }

                for (i = 1; i < F; i++)
                    if ((cmp = textBuffer[r + i] - textBuffer[p + i]) != 0) break;

                if (i > matchLength) {
                    matchPosition = p;
                    if ((matchLength = i) >= F) break;
                }
            }

            dad[r] = dad[p]; leftSon[r] = leftSon[p]; rightSon[r] = rightSon[p];
            dad[leftSon[p]] = r; dad[rightSon[p]] = r;

            if (rightSon[dad[p]] == p) rightSon[dad[p]] = r;
            else leftSon[dad[p]] = r;

            dad[p] = N;
        }

        private void DeleteNode(int p) {
            int q;

            if (dad[p] == N) return;

            if (rightSon[p] == N) q = leftSon[p];
            else if (leftSon[p] == N) q = rightSon[p];
            else {
                q = leftSon[p];

                if (rightSon[q] != N) {
                    do { q = rightSon[q]; } while (rightSon[q] != N);
                    rightSon[dad[q]] = leftSon[q]; dad[leftSon[q]] = dad[q];
                    leftSon[q] = leftSon[p]; dad[leftSon[p]] = q;
                }

                rightSon[q] = rightSon[p]; dad[rightSon[p]] = q;
            }

            dad[q] = dad[p];

            if (rightSon[dad[p]] == p) rightSon[dad[p]] = q;
            else leftSon[dad[p]] = q;

            dad[p] = N;
        }
        #endregion
    }

    public class Headers {
        private static uint imd5Magic = 0x494d4435;
        private static uint imetMagic = 0x494d4554;

        /// <summary>
        /// Convert HeaderType to int to get it's Length.
        /// </summary>
        public enum HeaderType {
            None = 0,
            /// <summary>
            /// Used in opening.bnr
            /// </summary>
            ShortIMET = 1536,
            /// <summary>
            /// Used in 00000000.app
            /// </summary>
            IMET = 1600,
            /// <summary>
            /// Used in banner.bin / icon.bin
            /// </summary>
            IMD5 = 32,
        }

        #region Public Functions
        /// <summary>
        /// Checks a file for Headers.
        /// </summary>
        /// <param name="pathToFile"></param>
        /// <returns></returns>
        public static HeaderType DetectHeader(string pathToFile) {
            return DetectHeader(File.ReadAllBytes(pathToFile));
        }

        /// <summary>
        /// Checks the byte array for Headers.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static HeaderType DetectHeader(byte[] file) {
            if (file.Length > 68)
                if (Shared.Swap(BitConverter.ToUInt32(file, 64)) == imetMagic)
                    return HeaderType.ShortIMET;
            if (file.Length > 132)
                if (Shared.Swap(BitConverter.ToUInt32(file, 128)) == imetMagic)
                    return HeaderType.IMET;
            if (file.Length > 4)
                if (Shared.Swap(BitConverter.ToUInt32(file, 0)) == imd5Magic)
                    return HeaderType.IMD5;

            return HeaderType.None;
        }

        /// <summary>
        /// Checks the stream for Headers.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static HeaderType DetectHeader(Stream file) {
            byte[] tmp = new byte[4];

            if (file.Length > 68) {
                file.Seek(64, SeekOrigin.Begin);
                file.Read(tmp, 0, tmp.Length);
                if (Shared.Swap(BitConverter.ToUInt32(tmp, 0)) == imetMagic)
                    return HeaderType.ShortIMET;
            }
            if (file.Length > 132) {
                file.Seek(128, SeekOrigin.Begin);
                file.Read(tmp, 0, tmp.Length);
                if (Shared.Swap(BitConverter.ToUInt32(tmp, 0)) == imetMagic)
                    return HeaderType.IMET;
            }
            if (file.Length > 4) {
                file.Seek(0, SeekOrigin.Begin);
                file.Read(tmp, 0, tmp.Length);
                if (Shared.Swap(BitConverter.ToUInt32(tmp, 0)) == imd5Magic)
                    return HeaderType.IMD5;
            }

            return HeaderType.None;
        }
        #endregion

        public class IMET {
            private bool hashesMatch = true;
            private bool isShortImet = false;

            private byte[] additionalPadding = new byte[64];
            private byte[] padding = new byte[64];
            private uint imetMagic = 0x494d4554;
            private uint sizeOfHeader = 0x00000600; //Without additionalPadding
            private uint unknown = 0x00000003;
            private uint iconSize;
            private uint bannerSize;
            private uint soundSize;
            private uint flags = 0x00000000;
            private byte[] japaneseTitle = new byte[84];
            private byte[] englishTitle = new byte[84];
            private byte[] germanTitle = new byte[84];
            private byte[] frenchTitle = new byte[84];
            private byte[] spanishTitle = new byte[84];
            private byte[] italianTitle = new byte[84];
            private byte[] dutchTitle = new byte[84];
            private byte[] unknownTitle1 = new byte[84];
            private byte[] unknownTitle2 = new byte[84];
            private byte[] koreanTitle = new byte[84];
            private byte[] padding2 = new byte[588];
            private byte[] hash = new byte[16];

            /// <summary>
            /// Short IMET has a padding of 64 bytes at the beginning while Long IMET has 128.
            /// </summary>
            public bool IsShortIMET { get { return isShortImet; } set { isShortImet = value; } }
            /// <summary>
            /// The size of uncompressed icon.bin
            /// </summary>
            public uint IconSize { get { return iconSize; } set { iconSize = value; } }
            /// <summary>
            /// The size of uncompressed banner.bin
            /// </summary>
            public uint BannerSize { get { return bannerSize; } set { bannerSize = value; } }
            /// <summary>
            /// The size of uncompressed sound.bin
            /// </summary>
            public uint SoundSize { get { return soundSize; } set { soundSize = value; } }
            /// <summary>
            /// The japanese Title.
            /// </summary>
            public string JapaneseTitle { get { return returnTitleAsString(japaneseTitle); } set { setTitleFromString(value, 0); } }
            /// <summary>
            /// The english Title.
            /// </summary>
            public string EnglishTitle { get { return returnTitleAsString(englishTitle); } set { setTitleFromString(value, 1); } }
            /// <summary>
            /// The german Title.
            /// </summary>
            public string GermanTitle { get { return returnTitleAsString(germanTitle); } set { setTitleFromString(value, 2); } }
            /// <summary>
            /// The french Title.
            /// </summary>
            public string FrenchTitle { get { return returnTitleAsString(frenchTitle); } set { setTitleFromString(value, 3); } }
            /// <summary>
            /// The spanish Title.
            /// </summary>
            public string SpanishTitle { get { return returnTitleAsString(spanishTitle); } set { setTitleFromString(value, 4); } }
            /// <summary>
            /// The italian Title.
            /// </summary>
            public string ItalianTitle { get { return returnTitleAsString(italianTitle); } set { setTitleFromString(value, 5); } }
            /// <summary>
            /// The dutch Title.
            /// </summary>
            public string DutchTitle { get { return returnTitleAsString(dutchTitle); } set { setTitleFromString(value, 6); } }
            /// <summary>
            /// The korean Title.
            /// </summary>
            public string KoreanTitle { get { return returnTitleAsString(koreanTitle); } set { setTitleFromString(value, 7); } }
            /// <summary>
            /// All Titles as a string array.
            /// </summary>
            public string[] AllTitles { get { return new string[] { JapaneseTitle, EnglishTitle, GermanTitle, FrenchTitle, SpanishTitle, ItalianTitle, DutchTitle, KoreanTitle }; } }
            /// <summary>
            /// When parsing an IMET header, this value will turn false if the hash stored in the header doesn't match the headers hash.
            /// </summary>
            public bool HashesMatch { get { return hashesMatch; } }

            #region Public Functions
            /// <summary>
            /// Loads the IMET Header of a file.
            /// </summary>
            /// <param name="pathToFile"></param>
            /// <returns></returns>
            public static IMET Load(string pathToFile) {
                return Load(File.ReadAllBytes(pathToFile));
            }

            /// <summary>
            /// Loads the IMET Header of a byte array.
            /// </summary>
            /// <param name="fileOrHeader"></param>
            /// <returns></returns>
            public static IMET Load(byte[] fileOrHeader) {
                HeaderType type = DetectHeader(fileOrHeader);
                if (type != HeaderType.IMET && type != HeaderType.ShortIMET)
                    throw new Exception("No IMET Header found!");

                IMET s = new IMET();
                if (type == HeaderType.ShortIMET) s.isShortImet = true;

                MemoryStream ms = new MemoryStream(fileOrHeader);
                try { s.parseHeader(ms); } catch { ms.Dispose(); throw; }

                ms.Dispose();
                return s;
            }

            /// <summary>
            /// Loads the IMET Header of a stream.
            /// </summary>
            /// <param name="fileOrHeader"></param>
            /// <returns></returns>
            public static IMET Load(Stream fileOrHeader) {
                HeaderType type = DetectHeader(fileOrHeader);
                if (type != HeaderType.IMET && type != HeaderType.ShortIMET)
                    throw new Exception("No IMET Header found!");

                IMET s = new IMET();
                if (type == HeaderType.ShortIMET) s.isShortImet = true;

                s.parseHeader(fileOrHeader);
                return s;
            }

            /// <summary>
            /// Creates a new IMET Header.
            /// </summary>
            /// <param name="isShortImet"></param>
            /// <param name="iconSize"></param>
            /// <param name="bannerSize"></param>
            /// <param name="soundSize"></param>
            /// <param name="titles"></param>
            /// <returns></returns>
            public static IMET Create(bool isShortImet, int iconSize, int bannerSize, int soundSize, params string[] titles) {
                IMET s = new IMET();
                s.isShortImet = isShortImet;

                for (int i = 0; i < titles.Length; i++)
                    s.setTitleFromString(titles[i], i);

                for (int i = titles.Length; i < 8; i++)
                    s.setTitleFromString((titles.Length > 1) ? titles[1] : titles[0], i);

                s.iconSize = (uint)iconSize;
                s.bannerSize = (uint)bannerSize;
                s.soundSize = (uint)soundSize;

                return s;
            }

            /// <summary>
            /// Removes the IMET Header of a file.
            /// </summary>
            /// <param name="pathToFile"></param>
            public static void RemoveHeader(string pathToFile) {
                byte[] fileWithoutHeader = RemoveHeader(File.ReadAllBytes(pathToFile));
                File.Delete(pathToFile);

                File.WriteAllBytes(pathToFile, fileWithoutHeader);
            }

            /// <summary>
            /// Removes the IMET Header of a byte array.
            /// </summary>
            /// <param name="file"></param>
            /// <returns></returns>
            public static byte[] RemoveHeader(byte[] file) {
                HeaderType type = DetectHeader(file);
                if (type != HeaderType.IMET && type != HeaderType.ShortIMET)
                    throw new Exception("No IMET Header found!");

                byte[] fileWithoutHeader = new byte[file.Length - (int)type];
                Array.Copy(file, (int)type, fileWithoutHeader, 0, fileWithoutHeader.Length);

                return fileWithoutHeader;
            }



            /// <summary>
            /// Sets all title to the given string.
            /// </summary>
            /// <param name="newTitle"></param>
            public void SetAllTitles(string newTitle) {
                for (int i = 0; i < 10; i++)
                    setTitleFromString(newTitle, i);
            }

            /// <summary>
            /// Returns the Header as a memory stream.
            /// </summary>
            /// <returns></returns>
            public MemoryStream ToMemoryStream() {
                MemoryStream ms = new MemoryStream();

                try { writeToStream(ms); } catch { ms.Dispose(); throw; }

                return ms;
            }

            /// <summary>
            /// Returns the Header as a byte array.
            /// </summary>
            /// <returns></returns>
            public byte[] ToByteArray() {
                return ToMemoryStream().ToArray();
            }

            /// <summary>
            /// Writes the Header to the given stream.
            /// </summary>
            /// <param name="writeStream"></param>
            public void Write(Stream writeStream) {
                writeToStream(writeStream);
            }

            /// <summary>
            /// Changes the Titles.
            /// </summary>
            /// <param name="newTitles"></param>
            public void ChangeTitles(params string[] newTitles) {
                for (int i = 0; i < newTitles.Length; i++)
                    setTitleFromString(newTitles[i], i);

                for (int i = newTitles.Length; i < 8; i++)
                    setTitleFromString((newTitles.Length > 1) ? newTitles[1] : newTitles[0], i);
            }

            /// <summary>
            /// Returns a string array with the Titles.
            /// </summary>
            /// <returns></returns>
            public string[] GetTitles() {
                return new string[] { JapaneseTitle, EnglishTitle, GermanTitle, FrenchTitle, SpanishTitle, ItalianTitle, DutchTitle, KoreanTitle };
            }
            #endregion

            #region Private Functions
            private void writeToStream(Stream writeStream) {
                writeStream.Seek(0, SeekOrigin.Begin);

                if (!isShortImet) writeStream.Write(additionalPadding, 0, additionalPadding.Length);

                writeStream.Write(padding, 0, padding.Length);
                writeStream.Write(BitConverter.GetBytes(Shared.Swap(imetMagic)), 0, 4);
                writeStream.Write(BitConverter.GetBytes(Shared.Swap(sizeOfHeader)), 0, 4);
                writeStream.Write(BitConverter.GetBytes(Shared.Swap(unknown)), 0, 4);
                writeStream.Write(BitConverter.GetBytes(Shared.Swap(iconSize)), 0, 4);
                writeStream.Write(BitConverter.GetBytes(Shared.Swap(bannerSize)), 0, 4);
                writeStream.Write(BitConverter.GetBytes(Shared.Swap(soundSize)), 0, 4);
                writeStream.Write(BitConverter.GetBytes(Shared.Swap(flags)), 0, 4);

                writeStream.Write(japaneseTitle, 0, japaneseTitle.Length);
                writeStream.Write(englishTitle, 0, englishTitle.Length);
                writeStream.Write(germanTitle, 0, germanTitle.Length);
                writeStream.Write(frenchTitle, 0, frenchTitle.Length);
                writeStream.Write(spanishTitle, 0, spanishTitle.Length);
                writeStream.Write(italianTitle, 0, italianTitle.Length);
                writeStream.Write(dutchTitle, 0, dutchTitle.Length);
                writeStream.Write(unknownTitle1, 0, unknownTitle1.Length);
                writeStream.Write(unknownTitle2, 0, unknownTitle2.Length);
                writeStream.Write(koreanTitle, 0, koreanTitle.Length);
                writeStream.Write(padding2, 0, padding2.Length);

                int hashPos = (int)writeStream.Position;
                hash = new byte[16];
                writeStream.Write(hash, 0, hash.Length);

                byte[] toHash = new byte[writeStream.Position];
                writeStream.Seek(0, SeekOrigin.Begin);
                writeStream.Read(toHash, 0, toHash.Length);

                computeHash(toHash, isShortImet ? 0 : 0x40);

                writeStream.Seek(hashPos, SeekOrigin.Begin);
                writeStream.Write(hash, 0, hash.Length);
            }

            private void computeHash(byte[] headerBytes, int hashPos) {
                MD5 md5 = MD5.Create();
                hash = md5.ComputeHash(headerBytes, hashPos, 0x600);
                md5.Clear();
            }

            private void parseHeader(Stream headerStream) {
                headerStream.Seek(0, SeekOrigin.Begin);
                byte[] tmp = new byte[4];

                if (!isShortImet) headerStream.Read(additionalPadding, 0, additionalPadding.Length);
                headerStream.Read(padding, 0, padding.Length);

                headerStream.Read(tmp, 0, 4);
                if (Shared.Swap(BitConverter.ToUInt32(tmp, 0)) != imetMagic)
                    throw new Exception("Invalid Magic!");

                headerStream.Read(tmp, 0, 4);
                if (Shared.Swap(BitConverter.ToUInt32(tmp, 0)) != sizeOfHeader)
                    throw new Exception("Invalid Header Size!");

                headerStream.Read(tmp, 0, 4);
                unknown = Shared.Swap(BitConverter.ToUInt32(tmp, 0));

                headerStream.Read(tmp, 0, 4);
                iconSize = Shared.Swap(BitConverter.ToUInt32(tmp, 0));

                headerStream.Read(tmp, 0, 4);
                bannerSize = Shared.Swap(BitConverter.ToUInt32(tmp, 0));

                headerStream.Read(tmp, 0, 4);
                soundSize = Shared.Swap(BitConverter.ToUInt32(tmp, 0));

                headerStream.Read(tmp, 0, 4);
                flags = Shared.Swap(BitConverter.ToUInt32(tmp, 0));

                headerStream.Read(japaneseTitle, 0, japaneseTitle.Length);
                headerStream.Read(englishTitle, 0, englishTitle.Length);
                headerStream.Read(germanTitle, 0, germanTitle.Length);
                headerStream.Read(frenchTitle, 0, frenchTitle.Length);
                headerStream.Read(spanishTitle, 0, spanishTitle.Length);
                headerStream.Read(italianTitle, 0, italianTitle.Length);
                headerStream.Read(dutchTitle, 0, dutchTitle.Length);
                headerStream.Read(unknownTitle1, 0, unknownTitle1.Length);
                headerStream.Read(unknownTitle2, 0, unknownTitle2.Length);
                headerStream.Read(koreanTitle, 0, koreanTitle.Length);

                headerStream.Read(padding2, 0, padding2.Length);
                headerStream.Read(hash, 0, hash.Length);

                headerStream.Seek(-16, SeekOrigin.Current);
                headerStream.Write(new byte[16], 0, 16);

                byte[] temp = new byte[headerStream.Length];
                headerStream.Seek(0, SeekOrigin.Begin);
                headerStream.Read(temp, 0, temp.Length);

                MD5 m = MD5.Create();
                byte[] newHash = m.ComputeHash(temp, (isShortImet) ? 0 : 0x40, 0x600);
                m.Clear();

                hashesMatch = Shared.CompareByteArrays(newHash, hash);
            }

            private string returnTitleAsString(byte[] title) {
                string tempStr = string.Empty;

                for (int i = 0; i < 84; i += 2) {
                    char tempChar = BitConverter.ToChar(new byte[] { title[i + 1], title[i] }, 0);
                    if (tempChar != 0x00) tempStr += tempChar;
                }

                return tempStr;
            }

            private void setTitleFromString(string title, int titleIndex) {
                byte[] tempArray = new byte[84];

                for (int i = 0; i < title.Length; i++) {
                    byte[] tempBytes = BitConverter.GetBytes(title[i]);
                    tempArray[i * 2 + 1] = tempBytes[0];
                    tempArray[i * 2] = tempBytes[1];
                }

                switch (titleIndex) {
                    case 0:
                        japaneseTitle = tempArray;
                        break;
                    case 1:
                        englishTitle = tempArray;
                        break;
                    case 2:
                        germanTitle = tempArray;
                        break;
                    case 3:
                        frenchTitle = tempArray;
                        break;
                    case 4:
                        spanishTitle = tempArray;
                        break;
                    case 5:
                        italianTitle = tempArray;
                        break;
                    case 6:
                        dutchTitle = tempArray;
                        break;
                    case 7:
                        koreanTitle = tempArray;
                        break;
                }
            }
            #endregion
        }

        public class IMD5 {
            private uint imd5Magic = 0x494d4435;
            private uint fileSize;
            private byte[] padding = new byte[8];
            private byte[] hash = new byte[16];

            /// <summary>
            /// The size of the file without the IMD5 Header.
            /// </summary>
            public uint FileSize { get { return fileSize; } }
            /// <summary>
            /// The hash of the file without the IMD5 Header.
            /// </summary>
            public byte[] Hash { get { return hash; } }

            private IMD5() { }

            #region Public Functions
            /// <summary>
            /// Loads the IMD5 Header of a file.
            /// </summary>
            /// <param name="pathToFile"></param>
            /// <returns></returns>
            public static IMD5 Load(string pathToFile) {
                return Load(File.ReadAllBytes(pathToFile));
            }

            /// <summary>
            /// Loads the IMD5 Header of a byte array.
            /// </summary>
            /// <param name="fileOrHeader"></param>
            /// <returns></returns>
            public static IMD5 Load(byte[] fileOrHeader) {
                HeaderType type = DetectHeader(fileOrHeader);
                if (type != HeaderType.IMD5)
                    throw new Exception("No IMD5 Header found!");

                IMD5 h = new IMD5();
                MemoryStream ms = new MemoryStream(fileOrHeader);

                try { h.parseHeader(ms); } catch { ms.Dispose(); throw; }

                ms.Dispose();
                return h;
            }

            /// <summary>
            /// Loads the IMD5 Header of a stream.
            /// </summary>
            /// <param name="fileOrHeader"></param>
            /// <returns></returns>
            public static IMD5 Load(Stream fileOrHeader) {
                HeaderType type = DetectHeader(fileOrHeader);
                if (type != HeaderType.IMD5)
                    throw new Exception("No IMD5 Header found!");

                IMD5 h = new IMD5();
                h.parseHeader(fileOrHeader);
                return h;
            }

            /// <summary>
            /// Creates a new IMD5 Header.
            /// </summary>
            /// <param name="file"></param>
            /// <returns></returns>
            public static IMD5 Create(byte[] file) {
                IMD5 h = new IMD5();

                h.fileSize = (uint)file.Length;
                h.computeHash(file);

                return h;
            }

            /// <summary>
            /// Adds an IMD5 Header to a file.
            /// </summary>
            /// <param name="pathToFile"></param>
            public static void AddHeader(string pathToFile) {
                byte[] fileWithHeader = AddHeader(File.ReadAllBytes(pathToFile));
                File.Delete(pathToFile);

                using (FileStream fs = new FileStream(pathToFile, FileMode.Create))
                    fs.Write(fileWithHeader, 0, fileWithHeader.Length);
            }

            /// <summary>
            /// Adds an IMD5 Header to a byte array.
            /// </summary>
            /// <param name="file"></param>
            /// <returns></returns>
            public static byte[] AddHeader(byte[] file) {
                IMD5 h = IMD5.Create(file);

                MemoryStream ms = new MemoryStream();
                h.writeToStream(ms);
                ms.Write(file, 0, file.Length);

                byte[] res = ms.ToArray();
                ms.Dispose();
                return res;
            }

            /// <summary>
            /// Removes the IMD5 Header of a file.
            /// </summary>
            /// <param name="pathToFile"></param>
            public static void RemoveHeader(string pathToFile) {
                byte[] fileWithoutHeader = RemoveHeader(File.ReadAllBytes(pathToFile));
                File.Delete(pathToFile);

                using (FileStream fs = new FileStream(pathToFile, FileMode.Create))
                    fs.Write(fileWithoutHeader, 0, fileWithoutHeader.Length);
            }

            /// <summary>
            /// Removes the IMD5 Header of a byte array.
            /// </summary>
            /// <param name="file"></param>
            /// <returns></returns>
            public static byte[] RemoveHeader(byte[] file) {
                MemoryStream ms = new MemoryStream();
                ms.Write(file, 32, file.Length - 32);

                byte[] ret = ms.ToArray();
                ms.Dispose();

                return ret;
            }



            /// <summary>
            /// Returns the IMD5 Header as a memory stream.
            /// </summary>
            /// <returns></returns>
            public MemoryStream ToMemoryStream() {
                MemoryStream ms = new MemoryStream();

                try { writeToStream(ms); } catch { ms.Dispose(); throw; }

                return ms;
            }

            /// <summary>
            /// Returns the IMD5 Header as a byte array.
            /// </summary>
            /// <returns></returns>
            public byte[] ToByteArray() {
                return ToMemoryStream().ToArray();
            }

            /// <summary>
            /// Writes the IMD5 Header to the given stream.
            /// </summary>
            /// <param name="writeStream"></param>
            public void Write(Stream writeStream) {
                writeToStream(writeStream);
            }
            #endregion

            #region Private Functions
            private void writeToStream(Stream writeStream) {
                writeStream.Seek(0, SeekOrigin.Begin);
                writeStream.Write(BitConverter.GetBytes(Shared.Swap(imd5Magic)), 0, 4);
                writeStream.Write(BitConverter.GetBytes(Shared.Swap(fileSize)), 0, 4);
                writeStream.Write(padding, 0, padding.Length);
                writeStream.Write(hash, 0, hash.Length);
            }

            private void computeHash(byte[] bytesToHash) {
                MD5 md5 = MD5.Create();
                hash = md5.ComputeHash(bytesToHash);
                md5.Clear();
            }

            private void parseHeader(Stream headerStream) {
                headerStream.Seek(0, SeekOrigin.Begin);
                byte[] tmp = new byte[4];

                headerStream.Read(tmp, 0, 4);
                if (Shared.Swap(BitConverter.ToUInt32(tmp, 0)) != imd5Magic)
                    throw new Exception("Invalid Magic!");

                headerStream.Read(tmp, 0, 4);
                fileSize = Shared.Swap(BitConverter.ToUInt32(tmp, 0));

                headerStream.Read(padding, 0, padding.Length);
                headerStream.Read(hash, 0, hash.Length);
            }
            #endregion
        }
    }

    public static class Shared {
        /// <summary>
        /// Merges two string arrays into one without double entries.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static string[] MergeStringArrays(string[] a, string[] b) {
            List<string> sList = new List<string>(a);

            foreach (string currentString in b)
                if (!sList.Contains(currentString)) sList.Add(currentString);

            sList.Sort();
            return sList.ToArray();
        }

        /// <summary>
        /// Compares two byte arrays.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="firstIndex"></param>
        /// <param name="second"></param>
        /// <param name="secondIndex"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static bool CompareByteArrays(byte[] first, int firstIndex, byte[] second, int secondIndex, int length) {
            if (first.Length < length || second.Length < length) return false;

            for (int i = 0; i < length; i++)
                if (first[firstIndex + i] != second[secondIndex + i]) return false;

            return true;
        }

        /// <summary>
        /// Compares two byte arrays.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static bool CompareByteArrays(byte[] first, byte[] second) {
            if (first.Length != second.Length) return false;
            else
                for (int i = 0; i < first.Length; i++)
                    if (first[i] != second[i]) return false;

            return true;
        }

        /// <summary>
        /// Turns a byte array into a string, default separator is a space.
        /// </summary>
        /// <param name="byteArray"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        public static string ByteArrayToString(byte[] byteArray, char separator = ' ') {
            string res = string.Empty;

            foreach (byte b in byteArray)
                res += b.ToString("x2").ToUpper() + separator;

            return res.Remove(res.Length - 1);
        }

        /// <summary>
        /// Turns a hex string into a byte array.
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public static byte[] HexStringToByteArray(string hexString) {
            byte[] ba = new byte[hexString.Length / 2];

            for (int i = 0; i < hexString.Length / 2; i++)
                ba[i] = byte.Parse(hexString.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);

            return ba;
        }

        /// <summary>
        /// Counts how often the given char exists in the given string.
        /// </summary>
        /// <param name="theString"></param>
        /// <param name="theChar"></param>
        /// <returns></returns>
        public static int CountCharsInString(string theString, char theChar) {
            int count = 0;

            foreach (char thisChar in theString)
                if (thisChar == theChar)
                    count++;

            return count;
        }

        /// <summary>
        /// Pads the given value to a multiple of the given padding value, default padding value is 64.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static long AddPadding(long value) {
            return AddPadding(value, 64);
        }

        /// <summary>
        /// Pads the given value to a multiple of the given padding value, default padding value is 64.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="padding"></param>
        /// <returns></returns>
        public static long AddPadding(long value, int padding) {
            if (value % padding != 0) {
                value = value + (padding - (value % padding));
            }

            return value;
        }

        /// <summary>
        /// Pads the given value to a multiple of the given padding value, default padding value is 64.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int AddPadding(int value) {
            return AddPadding(value, 64);
        }

        /// <summary>
        /// Pads the given value to a multiple of the given padding value, default padding value is 64.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="padding"></param>
        /// <returns></returns>
        public static int AddPadding(int value, int padding) {
            if (value % padding != 0) {
                value = value + (padding - (value % padding));
            }

            return value;
        }

        /// <summary>
        /// Swaps endianness.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ushort Swap(ushort value) {
            return (ushort)IPAddress.HostToNetworkOrder((short)value);
        }

        /// <summary>
        /// Swaps endianness.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint Swap(uint value) {
            return (uint)IPAddress.HostToNetworkOrder((int)value);
        }

        /// <summary>
        /// Swaps endianness
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ulong Swap(ulong value) {
            return (ulong)IPAddress.HostToNetworkOrder((long)value);
        }

        /// <summary>
        /// Turns a ushort array into a byte array.
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static byte[] UShortArrayToByteArray(ushort[] array) {
            List<byte> results = new List<byte>();
            foreach (ushort value in array) {
                byte[] converted = BitConverter.GetBytes(value);
                results.AddRange(converted);
            }
            return results.ToArray();
        }

        /// <summary>
        /// Turns a uint array into a byte array.
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static byte[] UIntArrayToByteArray(uint[] array) {
            List<byte> results = new List<byte>();
            foreach (uint value in array) {
                byte[] converted = BitConverter.GetBytes(value);
                results.AddRange(converted);
            }
            return results.ToArray();
        }

        /// <summary>
        /// Turns a byte array into a uint array.
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static uint[] ByteArrayToUIntArray(byte[] array) {
            UInt32[] converted = new UInt32[array.Length / 4];
            int j = 0;

            for (int i = 0; i < array.Length; i += 4)
                converted[j++] = BitConverter.ToUInt32(array, i);

            return converted;
        }

        /// <summary>
        /// Turns a byte array into a ushort array.
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static ushort[] ByteArrayToUShortArray(byte[] array) {
            ushort[] converted = new ushort[array.Length / 2];
            int j = 0;

            for (int i = 0; i < array.Length; i += 2)
                converted[j++] = BitConverter.ToUInt16(array, i);

            return converted;
        }
    }
}