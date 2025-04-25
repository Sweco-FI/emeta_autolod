namespace Lodifier.Clustering
{
    public class BoundingVolumeAABB
    {
        public float CenterX, CenterY, CenterZ; // in world coordinates
        public float HalfSizeX, HalfSizeY, HalfSizeZ;
        
        public BoundingVolumeAABB(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            HalfSizeX = (maxX - minX) / 2f;
            HalfSizeY = (maxY - minY) / 2f;
            HalfSizeZ = (maxZ - minZ) / 2f;

            CenterX = minX + HalfSizeX;
            CenterY = minY + HalfSizeY;
            CenterZ = minZ + HalfSizeZ;
        }

        public float MinX => CenterX - HalfSizeX;
        public float MinY => CenterY - HalfSizeY;
        public float MinZ => CenterZ - HalfSizeZ;

        public float MaxX => CenterX + HalfSizeX;
        public float MaxY => CenterY + HalfSizeY;
        public float MaxZ => CenterZ + HalfSizeZ;
    }
}