using Lodifier.Clustering;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Lodifier
{
    public class Clusterer
    {
        public void ReadGLTF(string pathInput)
        {
            Stopwatch stopwatch = new Stopwatch();

            // assume that there are subpaths that each contain multiple LODs in GLTF
            foreach (var lodPath in Directory.GetDirectories(pathInput))
            {
                stopwatch.Restart();
                ProcessLODPath(lodPath);
                // Console.WriteLine($"{lodPath}: {stopwatch.Elapsed}");
            }

            stopwatch.Stop();
        }

        public void CreateVolumes()
        {
            foreach (var entities in entitiesByMaterial.Values)
            {
                foreach (var entity in entities)
                {
                    CalcBoundingVolume(entity);
                }
            }
        }

        public void ClusterKMeans()
        {
            clusters.Clear();

            foreach (var pair in entitiesByMaterial)
            {
                var clusterMaterial = pair.Key;
                var entities = pair.Value;
                int clusterAmount = Math.Min(3, entities.Count);

                var points = ConvertToDataVec(entities);
                var cl = new Clustering.KMeans.KMeansClustering(points, clusterAmount);
                var kMeansClusters = cl.Compute();

                foreach (var kMeansC in kMeansClusters)
                {
                    clusters.Add(new Cluster
                    {
                        Entities = kMeansC.Points.Select(p => ((DataVecEntity)p).Entity).ToList(),
                        Material = clusterMaterial,
                    });
                }
            }
        }

        public void CreateClusterVolumes()
        {
            clusters
                .Where(c => c.Entities.Count > 0)
                .ToList()
                .ForEach(CalcClusterVolume);
        }

        public void OffsetToOrigo()
        {
            if (clusters.Count <= 0)
            {
                globalOffset = new Vector3 (0, 0, 0);
                return;
            }

            var volumes = clusters.Select(c => c.Volume).ToList();

            float minX = volumes.Min(v => v.MinX);
            float minY = volumes.Min(v => v.MinY);
            float minZ = volumes.Min(v => v.MinZ);

            float maxX = volumes.Max(v => v.MaxX);
            float maxY = volumes.Max(v => v.MaxY);
            float maxZ = volumes.Max(v => v.MaxZ);

            var globalVolume = new BoundingVolumeAABB(minX, maxX, minY, maxY, minZ, maxZ);

            globalOffset = new Vector3(-globalVolume.CenterX, -globalVolume.CenterY + globalVolume.HalfSizeY, -globalVolume.CenterZ);

            Console.WriteLine($"Offsetting with: {globalOffset}");
        }

        public void CombineGLTF(string pathClustererOut)
        {
            int clusterIdx = 0;
            int lodCount = GetLodCount();

            foreach (var cluster in clusters)
            {
                if (cluster.Entities.Count <= 0)
                    continue;

                // combine one new GLTF out of all the mesh data in all of the Entities in this Cluster

                for (int i = 0; i < lodCount; i++)
                {
                    string filename = GetClusterFilename(pathClustererOut, clusterIdx, i);
                    var builder = CreateClusterBuilder(cluster, i);
                    builder.ToGltf2().SaveGLB(filename);
                }

                clusterIdx++;
            }
        }

        private class DataVecEntity : Clustering.KMeans.DataVec
        {
            public DataVecEntity(Entity e)
            {
                Entity = e;

                var translation = e.Geometry.LODs.First().Node.WorldMatrix.Translation;
                Components = new[] { (double)translation.X, translation.Y, translation.Z };
            }

            public Entity Entity { get; set; }
        }

        private class GLTFHandle
        {
            // handle between Entity class and the actual GLTF mesh data
            public Entity Entity { get; set; }
        }

        private Dictionary<Guid, GLTFHandle> gltfEntityMapping = new Dictionary<Guid, GLTFHandle>();
        private List<Cluster> clusters = new List<Cluster>();
        private Dictionary<Material, List<Entity>> entitiesByMaterial = new Dictionary<Material, List<Entity>>();
        private Vector3 globalOffset;

        private void CalcBoundingVolume(Entity entity)
        {
            float minX = float.NaN, minY = float.NaN, minZ = float.NaN, maxX = float.NaN, maxY = float.NaN, maxZ = float.NaN;

            var origNode = entity.Geometry.LODs[0].Node;

            foreach (var meshPrim in origNode.Mesh.Primitives)
            {
                var vertAccessor = meshPrim.GetVertices("POSITION");
                var verts = vertAccessor.AsVector3Array();

                foreach (var v in verts)
                {
                    var vWorldMatrix = Matrix4x4Factory.LocalToWorld(origNode.WorldMatrix, Matrix4x4.CreateTranslation(v));
                    var translation = vWorldMatrix.Translation;

                    if (float.IsNaN(minX))
                    {
                        minX = maxX = translation.X;
                        minY = maxY = translation.Y;
                        minZ = maxZ = translation.Z;
                    }
                    else
                    {
                        minX = Math.Min(minX, translation.X);
                        minY = Math.Min(minY, translation.Y);
                        minZ = Math.Min(minZ, translation.Z);

                        maxX = Math.Max(maxX, translation.X);
                        maxY = Math.Max(maxY, translation.Y);
                        maxZ = Math.Max(maxZ, translation.Z);
                    }
                }
            }

            entity.Volume = new BoundingVolumeAABB(minX, maxX, minY, maxY, minZ, maxZ);
        }

        private void ProcessLODPath(string lodPath)
        {
            var filenames = Directory.GetFiles(lodPath, "*_?.gltf");

            if (filenames.Length > 0)
                ReadEntities(filenames);
        }

        private void ReadEntities(string[] filenames)
        {
            ModelRoot[] models = new ModelRoot[filenames.Length];
            for(int i=0; i <filenames.Length; i++)
            {
                models[i] = ModelRoot.Load(filenames[i]);
            }

            Node[] lodNodes = new Node[models.Length];
            
            for(int i=0; i < models[0].LogicalNodes.Count; i++)
            {
                var node = models[0].LogicalNodes[i];

                if (node.Mesh == null)
                    continue;

                for(int k = 0; k < lodNodes.Length; k++)
                {
                    lodNodes[k] = models[k].LogicalNodes[i];
                }

                var mat = GetMaterial(node);
                var entity = CreateEntityByNode(lodNodes);

                var entityList = GetListOfEntitiesByMaterial(mat);
                entityList.Add(entity);
            }
        }

        private Entity CreateEntityByNode(Node[] lodNodes)
        {
            var entity = new Entity();
            entity.Geometry = new GeometryLOD();
            entity.Geometry.LODs.AddRange(
                lodNodes.Select(node => new GeometryBlueprint() { Node = node })
            );
            return entity;
        }

        private List<Entity> GetListOfEntitiesByMaterial(Material mat)
        {
            List<Entity> result;

            if (!entitiesByMaterial.TryGetValue(mat, out result))
            {
                result = entitiesByMaterial[mat] = new List<Entity>();
            }

            return result;
        }

        private Material GetMaterial(Node node)
        {
            if (node.Mesh == null)
            {
                throw new Exception("No mesh on the node!");
            }
            if (node.Mesh.Primitives.Count == 0)
            {
                throw new Exception("No primitives on the mesh!");
            }
            if (node.Mesh.Primitives.Count != 1)
            {
                Console.WriteLine($"Multiple primitives on the mesh, material info might get mixed up: {node.Mesh.Primitives.Count}");
            }

            return node.Mesh.Primitives[0].Material;
        }

        private MaterialBuilder CreateRandomColor(Material clusterMaterial)
        {
            var result = clusterMaterial.ToMaterialBuilder();

            var r = new Random();
            
            return result
                .WithBaseColor(new Vector4((float)r.NextDouble(), (float)r.NextDouble(), (float) r.NextDouble(), 1f));
        }

        private DataVecEntity[] ConvertToDataVec(List<Entity> entities)
        {
            return entities.Select(e => new DataVecEntity(e)).ToArray();
        }

        private int GetLodCount()
        {
            var firstPair = entitiesByMaterial.FirstOrDefault();

            if (firstPair.Value == null)
                return 0;

            var firstEntity = firstPair.Value.FirstOrDefault();

            if (firstEntity == null) 
                return 0;

            return firstEntity.Geometry.LODs.Count;
        }

        private string GetClusterFilename(string basePath, int clusterIdx, int lodIdx) => Path.Combine(basePath, $"cluster_{clusterIdx}_lod_{lodIdx}.glb");

        private SceneBuilder CreateClusterBuilder(Cluster cluster, int lodIndex)
        {
            var scene = new SceneBuilder();

            Matrix4x4 center = CalcCenter(cluster);

            var materialBuilder = cluster.Material.ToMaterialBuilder();
            // var materialBuilder = CreateRandomColor(cluster.Material);

            var builder = new MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexEmpty, VertexEmpty>();
            var prim = builder.UsePrimitive(materialBuilder);

            bool loggedMissingMesh = false;

            foreach (var entity in cluster.Entities)
            {
                var origNode = entity.Geometry.LODs[lodIndex].Node;

                if (origNode.Mesh == null || origNode.Mesh.Primitives == null)
                {
                    if (!loggedMissingMesh)
                    {
                        Console.WriteLine($"Node.Mesh in GLTF file was null, it definitely should not have been and there's probably a cluster with missing meshes now in the results");
                        loggedMissingMesh = true;
                    }
                    
                    continue;
                }

                foreach (var meshPrim in origNode.Mesh.Primitives)
                {
                    var triangles = meshPrim.GetTriangleIndices();

                    var vertAccessor = meshPrim.GetVertices("POSITION");
                    var normalAccessor = meshPrim.GetVertices("NORMAL");
                    var verts = vertAccessor.AsVector3Array();
                    var normals = normalAccessor.AsVector3Array();

                    foreach(var tri in triangles)
                    {
                        // using the Matrix4x4Factory LocalToWorld and WorldToLocal, transform each point to world space first, then into the local space of the center node
                        var m4WorldA = Matrix4x4Factory.LocalToWorld(origNode.WorldMatrix, Matrix4x4.CreateTranslation(verts[tri.A]));
                        var m4WorldB = Matrix4x4Factory.LocalToWorld(origNode.WorldMatrix, Matrix4x4.CreateTranslation(verts[tri.B]));
                        var m4WorldC = Matrix4x4Factory.LocalToWorld(origNode.WorldMatrix, Matrix4x4.CreateTranslation(verts[tri.C]));

                        var m4LocalA = Matrix4x4Factory.WorldToLocal(center, m4WorldA);
                        var m4LocalB = Matrix4x4Factory.WorldToLocal(center, m4WorldB);
                        var m4LocalC = Matrix4x4Factory.WorldToLocal(center, m4WorldC);

                        var a = new VertexPositionNormal(m4LocalA.Translation + globalOffset, normals[tri.A]);
                        var b = new VertexPositionNormal(m4LocalB.Translation + globalOffset, normals[tri.B]);
                        var c = new VertexPositionNormal(m4LocalC.Translation + globalOffset, normals[tri.C]);

                        prim.AddTriangle(a, b, c);
                    }
                }
            }

            if (!builder.IsEmpty)
            {
                scene.AddRigidMesh(builder, center);
            }

            return scene;
        }

        private Matrix4x4 CalcCenter(Cluster cluster)
        {
            var volume = cluster.Volume;
            var v = new Vector3(volume.CenterX, volume.CenterY, volume.CenterZ);
            return Matrix4x4.CreateTranslation(v);
        }

        private void CalcClusterVolume(Cluster cluster)
        {
            float minX = float.NaN, minY = float.NaN, minZ = float.NaN, maxX = float.NaN, maxY = float.NaN, maxZ = float.NaN;

            foreach (var entity in cluster.Entities)
            {
                var volume = entity.Volume;

                if (float.IsNaN(minX))
                {
                    minX = volume.MinX;
                    minY = volume.MinY;
                    minZ = volume.MinZ;

                    maxX = volume.MaxX;
                    maxY = volume.MaxY;
                    maxZ = volume.MaxZ;
                }
                else
                {
                    minX = Math.Min(minX, volume.MinX);
                    minY = Math.Min(minY, volume.MinY);
                    minZ = Math.Min(minZ, volume.MinZ);

                    maxX = Math.Max(maxX, volume.MaxX);
                    maxY = Math.Max(maxY, volume.MaxY);
                    maxZ = Math.Max(maxZ, volume.MaxZ);
                }
            }

            if (float.IsNaN(minX))
            {
                cluster.Volume = new BoundingVolumeAABB(0, 0, 0, 0, 0, 0);
                return;
            }

            cluster.Volume = new BoundingVolumeAABB(minX, maxX, minY, maxY, minZ, maxZ);
        }
    }
}