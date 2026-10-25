using System;

namespace Silence.Core
{
    // Engine-independent geometry in a measured torso coordinate frame (metres).
    public readonly struct RigVector
    {
        public readonly float X, Y, Z;
        public RigVector(float x, float y, float z) { X = x; Y = y; Z = z; }
        public float Length => (float)Math.Sqrt(Dot(this, this));
        public bool Finite => IsFinite(X) && IsFinite(Y) && IsFinite(Z);
        public RigVector Normalized => Length > .00001f ? this / Length : new RigVector(1, 0, 0);
        public static RigVector operator +(RigVector a, RigVector b) => new RigVector(a.X+b.X, a.Y+b.Y, a.Z+b.Z);
        public static RigVector operator -(RigVector a, RigVector b) => new RigVector(a.X-b.X, a.Y-b.Y, a.Z-b.Z);
        public static RigVector operator *(RigVector a, float s) => new RigVector(a.X*s, a.Y*s, a.Z*s);
        public static RigVector operator /(RigVector a, float s) => a * (1/s);
        public static float Dot(RigVector a, RigVector b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        private static bool IsFinite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }

    // Elliptical capsule between hips and upper chest. This is a conservative
    // anatomical envelope, not a triangle self-collision or cloth simulation.
    public readonly struct TorsoEnvelope
    {
        public readonly float Bottom, Top, Width, Depth, Cap;
        public TorsoEnvelope(float bottom, float top, float width, float depth, float cap)
        { Bottom = bottom; Top = top; Width = Math.Max(.06f, width); Depth = Math.Max(.06f, depth); Cap = Math.Max(.03f, cap); }
        public RigVector Clear(RigVector p, float radius, float side)
        {
            if (!p.Finite) return p;
            float y = Math.Max(Bottom, Math.Min(Top, p.Y)), r = Math.Max(0, radius);
            var scale = new RigVector(Width+r, Cap+r, Depth+r);
            var q = new RigVector(p.X/scale.X, (p.Y-y)/scale.Y, p.Z/scale.Z);
            float length = q.Length;
            if (length >= 1) return p;
            if (length < .0001f) q = new RigVector(side < 0 ? -1 : 1, 0, 0);
            else q = q / length;
            return new RigVector(q.X*scale.X, y+q.Y*scale.Y, q.Z*scale.Z);
        }
        public bool Contains(RigVector p, float radius)
        { return (Clear(p, radius, p.X) - p).Length > .0001f; }
    }

    public readonly struct ArmSolution
    {
        public readonly RigVector Hand, Elbow;
        public readonly bool Corrected, RemainingContact;
        public ArmSolution(RigVector hand, RigVector elbow, bool corrected, bool remaining)
        { Hand = hand; Elbow = elbow; Corrected = corrected; RemainingContact = remaining; }
    }

    public readonly struct BodyCapsule
    {
        public readonly RigVector Start, End;
        public readonly float Radius;
        public BodyCapsule(RigVector start, RigVector end, float radius) { Start=start; End=end; Radius=radius; }
        public RigVector Clear(RigVector p, float skin, float side)
        {
            if(!(Radius>0) || !p.Finite)return p;
            var axis=End-Start;float square=RigVector.Dot(axis,axis);
            float t=square>.00001f?Math.Max(0,Math.Min(1,RigVector.Dot(p-Start,axis)/square)):0;
            var centre=Start+axis*t;var delta=p-centre;float radius=Radius+Math.Max(0,skin);
            return delta.Length>=radius?p:centre+(delta.Length>.00001f?delta.Normalized:new RigVector(side<0?-1:1,0,0))*radius;
        }
    }

    public static class ArmAnatomyModel
    {
        // A two-link solve with measured limb lengths, a stable pole plane and
        // a nonzero elbow bend. Never stretches the skeleton to reach a target.
        public static ArmSolution Solve(RigVector shoulder, RigVector elbow, RigVector requested,
            float upper, float forearm, float side, TorsoEnvelope torso, float handRadius, float armRadius, BodyCapsule[] limbs=null)
        {
            if (!shoulder.Finite || !elbow.Finite || !requested.Finite || !(upper > .02f && upper < 1) || !(forearm > .02f && forearm < 1))
                return new ArmSolution(requested, elbow, false, true);
            float sign = side < 0 ? -1 : 1;
            var hand = Clear(requested, torso, limbs, handRadius, sign);
            var preferred = new RigVector(sign, -.15f, .25f).Normalized;
            bool correction = (hand-requested).Length > .0001f || Contact(shoulder,elbow,requested,torso,limbs,armRadius);
            var solved = elbow;
            for (int iteration = 0; iteration < 8; iteration++)
            {
                var delta = hand-shoulder;
                // Bend range 8..150 degrees; the SDK's solver receives a reachable goal.
                float minimum = (float)Math.Sqrt(upper*upper+forearm*forearm+2*upper*forearm*Math.Cos(150*Math.PI/180));
                float maximum = (float)Math.Sqrt(upper*upper+forearm*forearm+2*upper*forearm*Math.Cos(8*Math.PI/180));
                float distance = Math.Max(minimum, Math.Min(maximum, delta.Length));
                var direction = delta.Length > .0001f ? delta.Normalized : new RigVector(sign*.1f, -1, .2f).Normalized;
                hand = shoulder+direction*distance;
                var pole = elbow-shoulder;
                pole = pole-direction*RigVector.Dot(pole, direction);
                if (pole.Length < .01f || iteration > 0 || Inside(elbow,torso,limbs,armRadius))
                    pole = preferred-direction*RigVector.Dot(preferred, direction);
                if (pole.Length < .001f) { pole = new RigVector(0, 0, 1); pole -= direction*RigVector.Dot(pole, direction); }
                float along = (upper*upper-forearm*forearm+distance*distance)/(2*distance);
                float height = (float)Math.Sqrt(Math.Max(0, upper*upper-along*along));
                solved = shoulder+direction*along+pole.Normalized*height;
                bool contact = Contact(shoulder, solved, hand, torso, limbs, armRadius) || Inside(hand, torso, limbs, handRadius);
                if (!contact) break;
                correction = true;
                if (iteration == 7) break;
                // Iterate a bounded outward adjustment; unreachable cases are reported.
                hand = Clear(hand+preferred*.018f, torso, limbs, handRadius, sign);
            }
            bool remaining = Contact(shoulder, solved, hand, torso, limbs, armRadius) || Inside(hand, torso, limbs, handRadius);
            correction |= (hand-requested).Length > .002f || Inside(elbow,torso,limbs,armRadius);
            return new ArmSolution(hand, solved, correction, remaining);
        }
        private static RigVector Clear(RigVector p,TorsoEnvelope torso,BodyCapsule[] limbs,float radius,float side)
        {
            for(int pass=0;pass<3;pass++)
            {
                p=torso.Clear(p,radius,side);
                if(limbs!=null)for(int i=0;i<limbs.Length;i++)p=limbs[i].Clear(p,radius,side);
            }
            return p;
        }
        private static bool Inside(RigVector p,TorsoEnvelope torso,BodyCapsule[] limbs,float radius)
        {
            if(torso.Contains(p,radius))return true;
            if(limbs!=null)for(int i=0;i<limbs.Length;i++)if((limbs[i].Clear(p,radius,p.X)-p).Length>.0001f)return true;
            return false;
        }
        private static bool Contact(RigVector shoulder, RigVector elbow, RigVector hand, TorsoEnvelope torso, BodyCapsule[] limbs, float radius)
        {
            for (int i=0; i<=6; i++)
                if (Inside(elbow+(hand-elbow)*(i/6f),torso,limbs,radius)) return true;
            // The shoulder joint joins the torso; test beyond that attachment.
            for(int i=2;i<=6;i++)
                if(Inside(shoulder+(elbow-shoulder)*(i/6f),torso,limbs,radius))return true;
            return false;
        }
    }
}
