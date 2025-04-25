using System;
using System.Diagnostics;
using System.IO;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.ModelGeometry.Scene;
using Xbim.GLTF;

namespace Lodifier
{
    internal class Tessellator
    {
        public static void TessellateAllFoundIFCs(string pathIn, string pathOut, float scaleMultiplier)
        {
            Stopwatch stopwatch = new Stopwatch();
            foreach (var filename in Directory.GetFiles(pathIn, "*.ifc"))
            {
                stopwatch.Restart();
                Tessellate(filename, pathOut, scaleMultiplier);
                // Console.WriteLine($"{filename}: {stopwatch.Elapsed}");
            }
            stopwatch.Stop();
        }

        private static void Tessellate(string filename, string pathOut, float scaleMultiplier)
        {
            var ifc = new FileInfo(filename);
            var xbim = CreateGeometryXbim(ifc, true, pathOut);
            CreateGeometryGLTF(xbim, pathOut, scaleMultiplier);
        }

        private static void CreateGeometryGLTF(FileInfo xbim, string pathOut, float scaleMultiplier)
        {
            using (var s = IfcStore.Open(xbim.FullName))
            {
                var savename = GetFilenameOut(xbim.FullName, pathOut, ".gltf");
                var bldr = new Builder();

                var transform = XbimMatrix3D.Identity;
                transform.Scale(scaleMultiplier);

                var ret = bldr.BuildInstancedScene(s, transform);
                glTFLoader.Interface.SaveModel(ret, savename);

                // write json
                var jsonFileName = GetFilenameOut(xbim.FullName, pathOut, ".json");
                var bme = new Xbim.GLTF.SemanticExport.BuildingModelExtractor();
                var rep = bme.GetModel(s);
                rep.Export(jsonFileName);
            }
        }

        private static string GetFilenameOut(string origFilename, string pathOut, string extension)
        {
            var noExt = Path.GetFileNameWithoutExtension(origFilename);
            return Path.Combine(pathOut, noExt + extension);
        }

        private static FileInfo CreateGeometryXbim(FileInfo f, bool mode, string pathOut)
        {
            IfcStore.ModelProviderFactory.UseHeuristicModelProvider();
            using (var m = IfcStore.Open(f.FullName))
            {
                var c = new Xbim3DModelContext(m);
                c.CreateContext(null, mode);
                var newName = GetFilenameOut(f.FullName, pathOut, ".xbim");
                m.SaveAs(newName);
                return new FileInfo(newName);
            }
        }
    }
}
