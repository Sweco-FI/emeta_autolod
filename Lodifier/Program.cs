using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;


namespace Lodifier
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string pathSplitterIn = @"work/input";

            // PLEASE BE CAREFUL: EVERYTHING INSIDE THESE PATHS WILL BE DESTROYED RECURSIVELY
            // should work with both absolute and relative paths
            const string pathSplitterOut = @"work/splitter_out";
            const string pathTessellatorOut = @"work/tessellator_out";
            const string pathSimplifierOut = @"work/simplifier_out";
            const string pathClustererOut = @"work/clusterer_out";
            // PLEASE BE CAREFUL.

            DeleteAllFilesRecursively(pathSplitterOut);
            DeleteAllFilesRecursively(pathTessellatorOut);
            DeleteAllFilesRecursively(pathSimplifierOut);
            DeleteAllFilesRecursively(pathClustererOut);

            Clusterer c = new Clusterer();

            Phase("Splitting", () => IfcSplitter.SplitAllFoundIFCsByType(pathSplitterIn, pathSplitterOut));
            Phase("Tessellating", () => Tessellator.TessellateAllFoundIFCs(pathSplitterOut, pathTessellatorOut, 0.001f)); // todo: deduce the scale of the model from IFC instead of offering it here as parameter
            Phase("Simplifying", () => Simplifier.SimplifyAllFoundGLTF(pathTessellatorOut, pathSimplifierOut));
            
            Phase("Clustering: read GLTF", () => c.ReadGLTF(pathSimplifierOut));
            Phase("Clustering: volume creation", c.CreateVolumes);
            Phase("Clustering: ClusterKMeans", c.ClusterKMeans);
            Phase("Clustering: Create cluster volumes", c.CreateClusterVolumes);
            Phase("Clustering: Offset to origo", c.OffsetToOrigo); // this phase is optional. you can either choose to go with this as a convenience or keep the correct global coordinates
            Phase("Clustering: combine from clusters", () => c.CombineGLTF(pathClustererOut));
            
            Console.WriteLine($"All done: {sw.Elapsed}");
        }

        private static void DeleteAllFilesRecursively(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            Directory.CreateDirectory(path);
        }

        private static void Phase(string nameOfPhase, Action startPhase)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Console.WriteLine($"{nameOfPhase}..");
            startPhase();
            Console.WriteLine($"{nameOfPhase} done: {sw.Elapsed}");
        }
    }
}
