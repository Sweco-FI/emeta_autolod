using System.Diagnostics;
using Xbim.Common;
using Xbim.Ifc;
using Xbim.IO;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc4.Interfaces;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lodifier
{
    public class IfcSplitter
    {
        public static void SplitAllFoundIFCsByType(string pathIn, string pathOut)
        {
            Stopwatch stopwatch = new Stopwatch();
            foreach (var filename in Directory.GetFiles(pathIn, "*.ifc"))
            {
                stopwatch.Restart();
                SplitByTypeObjects(filename, pathOut);
                // Console.WriteLine($"{filename}: {stopwatch.Elapsed}");
            }
            stopwatch.Stop();
        }

        public static void SplitByTypeObjects(string filenameOrig, string outputPath)
        {
            var model = IfcStore.Open(filenameOrig);

            var allProducts = model.Instances.OfType<IfcProduct>().ToList();

            var allProductsTyped = allProducts
                .Where(prod => prod.IsTypedBy != null);

            var allProductsNotTyped = allProducts
                .Where(prod => prod.IsTypedBy == null);

            Console.WriteLine($"{allProductsTyped.Count()} typed vs {allProductsNotTyped.Count()} not typed");

            var typeObjects = model.Instances.OfType<IfcTypeObject>().ToList();

            int fileNumberIdx = 0;

            foreach (var typeObject in typeObjects)
            {
                // find all ifc products that use the type object, and write them to their own IFC file
                var instances = AllInstancesOfGivenType(typeObject, allProductsTyped).ToList();

                var filenameOut = GetFilenameAppended(filenameOrig, outputPath, $"_{fileNumberIdx}");

                WriteCopy(model, instances, filenameOut);

                fileNumberIdx++;
            }

            string fileNameNotTyped = GetFilenameAppended(filenameOrig, outputPath, "_not_typed");

            WriteCopy(model, allProductsNotTyped.ToList(), fileNameNotTyped);
        }

        private static string GetFilenameAppended(string origFilename, string path, string appended)
        {
            var noExt = Path.GetFileNameWithoutExtension(origFilename);
            noExt = noExt.Replace(" ", "_");
            var extension = Path.GetExtension(origFilename);
            return Path.Combine(path, noExt + appended + extension);
        }

        private static IEnumerable<IfcProduct> AllInstancesOfGivenType(IfcTypeObject typeObject, IEnumerable<IfcProduct> allProducts) => allProducts.Where(p => p.IsTypedBy == typeObject);

        private static void WriteCopy(IfcStore modelOriginal, List<IfcProduct> toBeCopied, string filename)
        {
            PropertyTranformDelegate semanticFilter = (property, parentObject) =>
            {
                //leave out mapped geometry
                if (parentObject is IIfcTypeProduct &&
                     property.PropertyInfo.Name == nameof(IIfcTypeProduct.RepresentationMaps))
                    return null;


                //only bring over IsDefinedBy and IsTypedBy inverse relationships which will take over all properties and types
                if (property.EntityAttribute.Order < 0 && !(
                    property.PropertyInfo.Name == nameof(IIfcProduct.IsDefinedBy) ||
                    property.PropertyInfo.Name == nameof(IIfcProduct.IsTypedBy)
                    ))
                    return null;

                return property.PropertyInfo.GetValue(parentObject, null);
            };



            using (var iModel = IfcStore.Create(modelOriginal.SchemaVersion, XbimStoreType.InMemoryModel))
            {
                using (var txn = iModel.BeginTransaction("Insert copy"))
                {
                    //single map should be used for all insertions between two models
                    var map = new XbimInstanceHandleMap(modelOriginal, iModel);

                    foreach (var prod in toBeCopied)
                    {
                        iModel.InsertCopy(prod, map, semanticFilter, true, false);
                    }

                    txn.Commit();
                }

                iModel.SaveAs(filename);
            }
        }

        private static List<IfcProduct> FindFirstInstances(List<IfcTypeObject> typeObjects, IEnumerable<IfcProduct> allProductsTyped)
        {
            List<IfcProduct> firstInstances = new List<IfcProduct>();
            foreach (var typeObject in typeObjects)
            {
                var instanceOfType = allProductsTyped.FirstOrDefault(product => product.IsTypedBy == typeObject);
                if (instanceOfType == null)
                {
                    throw new Exception("Couldn't find a single IfcProduct that was typed by the given instance");
                }
                firstInstances.Add(instanceOfType);
            }
            return firstInstances;
        }
    }
}
