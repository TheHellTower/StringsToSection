using dnlib.DotNet;
using dnlib.DotNet.Writer;
using System;
using System.IO;

namespace StringsToSection
{
    internal static class Program
    {
        static string filePath = string.Empty, outputFilePath = string.Empty;
        static ModuleDefMD Module = null;
        public static ModuleWriterOptions MWO = null;

        static void Main(string[] args)
        {
            if(args.Length != 0)
                filePath = args[0].Trim('"');
            while (!File.Exists(filePath))
            {
                Console.WriteLine("File Path: ");
                filePath = Console.ReadLine().Trim('"');
                Console.Clear();
            }

            outputFilePath = filePath.Insert(filePath.Length - 4, "-STS");

            Module = ModuleDefMD.Load(filePath);
            MWO = new ModuleWriterOptions(Module);

            try
            {
                Protections.Strings.Execute(Module);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            try
            {
                Module.Write(outputFilePath, MWO);
                Console.WriteLine("Protected with success !");
            } catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.ToString()}");

                MWO.Logger = DummyLogger.NoThrowInstance;
                MWO.MetadataOptions.Flags = MetadataFlags.KeepOldMaxStack;

                Module.Write(outputFilePath, MWO);
            }

            Console.ReadLine();
        }
    }
}