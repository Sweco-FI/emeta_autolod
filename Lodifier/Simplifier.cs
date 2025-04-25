using Medallion.Shell;
using System;
using System.Diagnostics;
using System.IO;

namespace Lodifier
{
    public class Simplifier
    {
        const string gltfPackPath = @"gltfpack\gltfpack.exe";
        
        private static string GetArgPack(string inputFile, string outputFile) => string.Format("-i {0} -o {1}", inputFile, outputFile);
        private static string GetArgSimplify(string inputFile, string outputFile, float simplifyLevel) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "-i {0} -o {1} -si {2}", inputFile, outputFile, simplifyLevel);

        static float[] lodLevels = { 0.5f, 0.25f, 0.1f };
        static TimeSpan timeout = TimeSpan.FromSeconds(60);

        public static void SimplifyAllFoundGLTF(string pathInput, string pathOutput)
        {
            Stopwatch stopwatch = new Stopwatch();
            foreach (var filename in Directory.GetFiles(pathInput, "*.gltf"))
            {
                stopwatch.Restart();
                Simplify(filename, pathOutput);
                // Console.WriteLine($"{filename}: {stopwatch.Elapsed}");
            }
            stopwatch.Stop();
        }

        private static string GetFilename(string filenameOrig, string pathOutput, int lodLevel)
        {
            return Path.Combine(GetPath(filenameOrig, pathOutput), $"lod_{lodLevel}.gltf");
        }

        private static string GetPath(string filenameOrig, string pathOutput)
        {
            string noExtension = Path.GetFileNameWithoutExtension(filenameOrig);
            return Path.Combine(pathOutput, noExtension);
        }

        private static void Simplify(string filenameIn, string pathOutput)
        {
            filenameIn = Path.GetFullPath(filenameIn);
            EnsureDirectoryExistsAndCleanIt(filenameIn, pathOutput);
            CreateLod0(filenameIn, pathOutput);

            for(int i = 0; i < lodLevels.Length; i++)
            {
                CreateLod(filenameIn, pathOutput, lodLevels[i], i + 1);
            }
        }

        private static void CreateLod(string filenameIn, string pathOutput, float simplifyLevel, int filenameLodLevel)
        {
            var filenameOut = GetFilename(filenameIn, pathOutput, filenameLodLevel);

            var arg = GetArgSimplify(filenameIn, filenameOut, simplifyLevel);

            CallGLTFPack(arg);
        }

        private static void EnsureDirectoryExistsAndCleanIt(string filenameIn, string pathOutput)
        {
            var path = GetPath(filenameIn, pathOutput);

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            Directory.CreateDirectory(path);
        }

        

        private static void CreateLod0(string filenameIn, string pathOutput)
        {
            var filenameOut = GetFilename(filenameIn, pathOutput, 0);

            var arg = GetArgPack(filenameIn, filenameOut);

            CallGLTFPack(arg);
        }

        private static void CallGLTFPack(string arg)
        {
            var command = MedallionExecutable.CreateCommand(gltfPackPath, arg, timeout);
            
            command.Wait();

            var result = command.Result;

            if (!result.Success)
            {
                Console.Error.WriteLine($"gltfpack failed with exit code {result.ExitCode}: {result.StandardError}");
            }
        }
    }
}
