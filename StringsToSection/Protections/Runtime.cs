using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace StringsToSection.Protections
{
    public static class StringLoader
    {
        internal static List<string> s;

        public static void Initialize()
        {
            if (s != null) return;
            s = new List<string>();

            var module = typeof(StringLoader).Module;
            IntPtr baseAddr = Marshal.GetHINSTANCE(module);

            unsafe
            {
                byte* basePtr = (byte*)baseAddr.ToPointer();
                byte XK = 0x5A;
                List<byte> SN = new List<byte>() { 0x01, 0x74, 0x69, 0x6A, 0x03, 0x0E, 0x12 };
                string dSN = string.Empty;
                int SNpos = 0;

                uint peOffset = *(uint*)(basePtr + 0x3C);
                byte* peHeader = basePtr + peOffset;
                SN[2] = SN[0];
                ushort numberOfSections = *(ushort*)(peHeader + 6);
                SN.Add(SN[5]);
                ushort sizeOfOptionalHeader = *(ushort*)(peHeader + 0x14);
                byte* sectionTable = peHeader + 0x18 + sizeOfOptionalHeader;

                for (int i = 0; i < numberOfSections; i++)
                {
                    byte* sectionPtr = sectionTable + i * 0x28;
                    string name = string.Empty;
                    for (int j = 0; j < 8; j++)
                    {
                        byte b = *(sectionPtr + j);
                        if (b == 0) break;
                        name += (char)b;
                    }

                    while (SNpos < SN.Count)
                    {
                        int len = SN[SNpos++];
                        for (int ii = 0; ii < len; ii++)
                            dSN += (char)(byte)(SN[SNpos++] ^ XK);
                    }

                    if (name == dSN)
                    {
                        uint virtualSize = *(uint*)(sectionPtr + 8);
                        uint rva = *(uint*)(sectionPtr + 12);
                        byte* dataPtr = basePtr + rva;
                        int pos = 0;

                        while (pos + 4 <= virtualSize)
                        {
                            int length = *(int*)(dataPtr + pos);
                            pos += 4;
                            if (length <= 0 || length > virtualSize - pos) break;

                            byte[] compressed = new byte[length];
                            for (int k = 0; k < length; k++)
                                compressed[k] = *(dataPtr + pos + k);
                            pos += length;

                            int padding = (4 - (length % 4)) % 4;
                            pos += padding;

                            string str = Decompress(compressed);
                            s.Add(str ?? string.Empty);
                        }
                        break;
                    }
                }

                string Decompress(byte[] data)
                {
                    using (var ms = new MemoryStream(data))
                    using (var gz = new GZipStream(ms, CompressionMode.Decompress))
                    using (var outMs = new MemoryStream())
                    {
                        gz.CopyTo(outMs);
                        return Encoding.UTF8.GetString(outMs.ToArray());
                    }
                }
            }
        }
    }
}