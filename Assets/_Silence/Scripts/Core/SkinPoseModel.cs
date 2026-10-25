namespace Silence.Core
{
    // Column-vector affine transform. Bind poses and bone transforms must use
    // the same units; their product already includes the mesh's bind scale.
    public readonly struct SkinTransform
    {
        public readonly RigVector X, Y, Z, Translation;
        public SkinTransform(RigVector x, RigVector y, RigVector z, RigVector translation)
        { X=x; Y=y; Z=z; Translation=translation; }
        public RigVector TransformVector(RigVector p) => X*p.X+Y*p.Y+Z*p.Z;
        public RigVector TransformPoint(RigVector p) => TransformVector(p)+Translation;
        public static SkinTransform operator *(SkinTransform parent,SkinTransform child)
            => new SkinTransform(parent.TransformVector(child.X),parent.TransformVector(child.Y),
                parent.TransformVector(child.Z),parent.TransformPoint(child.Translation));
    }

    public readonly struct SkinWeights4
    {
        public readonly int I0,I1,I2,I3;
        public readonly float W0,W1,W2,W3;
        public SkinWeights4(int i0,float w0,int i1=0,float w1=0,int i2=0,float w2=0,int i3=0,float w3=0)
        { I0=i0;W0=w0;I1=i1;W1=w1;I2=i2;W2=w2;I3=i3;W3=w3; }
    }

    public static class SkinPoseModel
    {
        // Inputs have been validated by the editor skin audit. Zero-weight
        // indices need not reference a bone (and must never be dereferenced).
        public static RigVector Position(RigVector vertex,SkinWeights4 weights,SkinTransform[] boneTimesBind)
        {
            var p=new RigVector(0,0,0);
            if(weights.W0>0)p+=boneTimesBind[weights.I0].TransformPoint(vertex)*weights.W0;
            if(weights.W1>0)p+=boneTimesBind[weights.I1].TransformPoint(vertex)*weights.W1;
            if(weights.W2>0)p+=boneTimesBind[weights.I2].TransformPoint(vertex)*weights.W2;
            if(weights.W3>0)p+=boneTimesBind[weights.I3].TransformPoint(vertex)*weights.W3;
            return p;
        }
    }
}
