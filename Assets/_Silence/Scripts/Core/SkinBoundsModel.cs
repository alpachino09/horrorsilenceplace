using System;

namespace Silence.Core
{
    public static class SkinBoundsModel
    {
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
