using System;

namespace Silence.Core
{
    public static class FlashlightHandMotion
    {
        // The cap can turn through several revolutions; a human wrist cannot.
        // Relax/regrip cycles keep the hand bounded and return to neutral.
        public static float WristTurn(float progress)
        {
            if(float.IsNaN(progress) || float.IsInfinity(progress))return 0;
            double u=Math.Max(0,Math.Min(1,progress));
            return (float)(40*Math.Sin(4*Math.PI*u)*Math.Sin(Math.PI*u));
        }
    }
}
