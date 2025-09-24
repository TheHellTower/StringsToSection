using Confuser.Core.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace StringsToSection.Protections
{
    internal static class Strings
    {
        static Dictionary<string, int> stringMap = new Dictionary<string, int>();
        static List<string> toIgnore = new List<string>() { string.Empty, "\n", "base.Icon", "Name", "Text", "Image", "Icon", "Resources" }; //Useless bs dont worry

        internal static void Execute(ModuleDefMD Module)
        {
            // 1) Collect all strings
            foreach (var type in Module.GetTypes().Where(T => !T.IsGlobalModuleType).OrderBy(t => t.Name))
                foreach (var method in type.Methods.Where(M => M.HasBody && M.Body.HasInstructions).OrderBy(m => m.Name))
                    foreach (var instr in method.Body.Instructions)
                        if (instr.OpCode == OpCodes.Ldstr && instr.Operand is string s)
                            if (!toIgnore.Contains(s) && !stringMap.ContainsKey(s))
                                stringMap.Add(s, stringMap.Count);

            // 2) Writer event
            Program.MWO.WriterEvent += WriterEvent;

            // 3) Inject StringLoader class
            ModuleDefMD selfModule = ModuleDefMD.Load(typeof(StringLoader).Module);
            TypeDef stringLoader = selfModule.GetTypes().First(T => T.Name == nameof(StringLoader));
            var injectedMethods = InjectHelper.Inject(stringLoader, Module.GlobalType, Module);

            var stringsField = (FieldDef)injectedMethods.First(F => F.Name == nameof(StringLoader.s));
            var initMethod = (MethodDef)injectedMethods.First(M => M.Name == nameof(StringLoader.Initialize));

            // 4) Call Initialize in cctor
            var cctor = Module.GlobalType.FindOrCreateStaticConstructor();
            cctor.Body.Instructions.Insert(0, OpCodes.Call.ToInstruction(initMethod));

            // 5) Replace all ldstr with runtime lookup
            var getItemRef = Module.Import(typeof(List<string>).GetProperty("Item").GetGetMethod());
            foreach (var type in Module.GetTypes().Where(T => !T.IsGlobalModuleType).OrderBy(t => t.Name))
                foreach (var method in type.Methods.Where(M => M.HasBody && M.Body.HasInstructions).OrderBy(m => m.Name))
                {
                    var body = method.Body;
                    body.SimplifyMacros(method.Parameters);

                    var ldstrInstrs = body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr).ToList();

                    for (int j = ldstrInstrs.Count - 1; j >= 0; j--)
                    {
                        var ld = ldstrInstrs[j];
                        if (!(ld.Operand is string literal)) continue;
                        if (toIgnore.Contains(literal)) continue;
                        if (!stringMap.TryGetValue(literal, out int stringIndex)) continue;
                        
                        int pos = body.Instructions.IndexOf(ld);
                        if (pos < 0) continue;

                        ld.OpCode = OpCodes.Ldsfld;
                        ld.Operand = stringsField;
                        body.Instructions.Insert(pos + 1, CreateLdcI4Instruction(stringIndex));
                        body.Instructions.Insert(pos + 2, Instruction.Create(OpCodes.Callvirt, getItemRef));
                    }

                    body.OptimizeMacros();
                    body.OptimizeBranches();
                }
        }

        private static Random random = new Random();

        private static void WriterEvent(object sender, ModuleWriterEventArgs e)
        {
            var writer = e.Writer;

            if (e.Event == ModuleWriterEvent.MDEndCreateTables)
            {
                // YCK1509
                var allStrings = stringMap.Keys.ToList(); // deterministic order
                byte[] allData = CompressStringsAligned(allStrings);

                var newSection = new PESection(".0THT", 0xE0000040);
                writer.Sections.Insert(0, newSection);

                uint alignment = writer.TextSection.Remove(writer.Metadata).Value;
                writer.TextSection.Add(writer.Metadata, alignment);

                alignment = writer.TextSection.Remove(writer.NetResources).Value;
                writer.TextSection.Add(writer.NetResources, alignment);

                alignment = writer.TextSection.Remove(writer.Constants).Value;
                newSection.Add(writer.Constants, alignment);

                var peSection = new PESection("", 0x60000020);
                bool moved = false;
                if (writer.StrongNameSignature != null)
                {
                    alignment = writer.TextSection.Remove(writer.StrongNameSignature).Value;
                    peSection.Add(writer.StrongNameSignature, alignment);
                    moved = true;
                }

                if (writer is ModuleWriter managedWriter)
                {
                    if (managedWriter.ImportAddressTable != null)
                    {
                        alignment = writer.TextSection.Remove(managedWriter.ImportAddressTable).Value;
                        peSection.Add(managedWriter.ImportAddressTable, alignment);
                        moved = true;
                    }
                    if (managedWriter.StartupStub != null)
                    {
                        alignment = writer.TextSection.Remove(managedWriter.StartupStub).Value;
                        peSection.Add(managedWriter.StartupStub, alignment);
                        moved = true;
                    }
                }

                if (moved) writer.Sections.AddBeforeReloc(peSection);
                newSection.Add(new ByteArrayChunk(allData), 4);
            }
        }

        private static byte[] CompressStringsAligned(List<string> strings)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                foreach (var s in strings)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(s);
                    byte[] compressed = Compress(bytes);
                    bw.Write(compressed.Length);
                    bw.Write(compressed);
                    int padding = (4 - (compressed.Length % 4)) % 4;
                    for (int i = 0; i < padding; i++) bw.Write((byte)0);
                }
                return ms.ToArray();
            }
        }

        private static byte[] Compress(byte[] data)
        {
            using (var ms = new MemoryStream())
            using (var gz = new GZipStream(ms, CompressionMode.Compress))
            {
                gz.Write(data, 0, data.Length);
                gz.Close();
                return ms.ToArray();
            }
        }

        private static Instruction CreateLdcI4Instruction(int value)
        {
            switch (value)
            {
                case -1: return Instruction.Create(OpCodes.Ldc_I4_M1);
                case 0: return Instruction.Create(OpCodes.Ldc_I4_0);
                case 1: return Instruction.Create(OpCodes.Ldc_I4_1);
                case 2: return Instruction.Create(OpCodes.Ldc_I4_2);
                case 3: return Instruction.Create(OpCodes.Ldc_I4_3);
                case 4: return Instruction.Create(OpCodes.Ldc_I4_4);
                case 5: return Instruction.Create(OpCodes.Ldc_I4_5);
                case 6: return Instruction.Create(OpCodes.Ldc_I4_6);
                case 7: return Instruction.Create(OpCodes.Ldc_I4_7);
                case 8: return Instruction.Create(OpCodes.Ldc_I4_8);
                default:
                    if (value >= sbyte.MinValue && value <= sbyte.MaxValue)
                        return Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)value);
                    else
                        return Instruction.Create(OpCodes.Ldc_I4, value);
            }
        }
    }

    internal static class AntiTamperExtensions
    {
        internal static void AddBeforeReloc(this List<PESection> sections, PESection newSection)
        {
            if (sections == null) throw new ArgumentNullException(nameof(sections));
            InsertBeforeReloc(sections, sections.Count, newSection);
        }

        internal static void InsertBeforeReloc(this List<PESection> sections, int preferredIndex, PESection newSection)
        {
            if (sections == null) throw new ArgumentNullException(nameof(sections));
            if (preferredIndex < 0 || preferredIndex > sections.Count) throw new ArgumentOutOfRangeException(nameof(preferredIndex), preferredIndex, "Preferred index is out of range.");
            if (newSection == null) throw new ArgumentNullException(nameof(newSection));

            var relocIndex = sections.FindIndex(0, Math.Min(preferredIndex + 1, sections.Count), IsRelocSection);
            if (relocIndex == -1)
                sections.Insert(preferredIndex, newSection);
            else
                sections.Insert(relocIndex, newSection);
        }

        private static bool IsRelocSection(PESection section) =>
            section.Name.Equals(".reloc", StringComparison.Ordinal);
    }
}