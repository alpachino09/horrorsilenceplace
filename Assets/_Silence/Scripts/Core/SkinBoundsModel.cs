using System;

namespace Silence.Core
{
    public static class SkinBoundsModel
    {
        // Local mesh units have no universal minimum size. Small facial parts
        // and FBX scale compensation can legitimately have tiny bounds. Planar
        // meshes are usable too; reject only invalid or completely zero bounds.
        public static bool IsUsable(RigVector center,RigVector extents)
            => center.Finite && extents.Finite && extents.X>=0 && extents.Y>=0 && extents.Z>=0
                && (extents.X>0 || extents.Y>0 || extents.Z>0);

        // Exact axis-aligned half size under a linear transform, including
        // nonuniform scale, negative scale and shear. Averaged scale is invalid.
        public static RigVector TransformExtents(RigVector e,RigVector x,RigVector y,RigVector z)
        {
            return new RigVector(Math.Abs(x.X)*e.X+Math.Abs(y.X)*e.Y+Math.Abs(z.X)*e.Z,
                Math.Abs(x.Y)*e.X+Math.Abs(y.Y)*e.Y+Math.Abs(z.Y)*e.Z,
                Math.Abs(x.Z)*e.X+Math.Abs(y.Z)*e.Y+Math.Abs(z.Z)*e.Z);
        }
    }
}
