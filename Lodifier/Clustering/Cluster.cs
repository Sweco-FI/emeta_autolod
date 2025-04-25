using SharpGLTF.Schema2;
using System.Collections.Generic;

namespace Lodifier.Clustering
{
    public class Cluster
    {
        public List<Entity> Entities { get; set; } = new List<Entity>();
        public Material Material { get; set; }
        public BoundingVolumeAABB Volume;
    }
}
