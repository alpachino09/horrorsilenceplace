using System;
using System.Text.RegularExpressions;

namespace Silence.Core
{
    public enum ImportedGround { Forest, Grass, Soil, Gravel }
    public enum ImportedMotion { Idle, Walk, Run, Sprint, WalkBack, StrafeLeft, StrafeRight, RunBack, RunLeft, RunRight, CrouchIdle, CrouchWalk }
    public enum ImportedMap { Unknown, Color, Normal, Roughness, Smoothness, Occlusion, Height, Metallic }

    public static class ImportedAssetModel
    {
        public static string Words(string value)
        {
            value=Regex.Replace(value ?? "","([a-z])([A-Z])","$1 $2");
            value=Regex.Replace(value,"([a-zA-Z])([0-9])","$1 $2");
            value=Regex.Replace(value,"([0-9])([a-zA-Z])","$1 $2");
            return " "+Regex.Replace(value.ToLowerInvariant(),"[^a-z0-9]+"," ").Trim()+" ";
        }
        private static bool Has(string words,string token)=>words.Contains(" "+token+" ");
        private static bool Any(string w,params string[] tokens)
        {foreach(var token in tokens)if(Has(w,token))return true;return false;}
        public static ImportedGround? Ground(string name)
        {
            string w=Words(name);
            if(Any(w,"leaf","leaves","fern","bark","card","foliage","roof","plaster","brick","wood"))return null;
            if(Any(w,"forest","forestfloor","moss","mossy"))return ImportedGround.Forest;
            if(Any(w,"grass","grassy","meadow"))return ImportedGround.Grass;
            if(Any(w,"gravel","rock","rocks","rocky","pebbles","road","path"))return ImportedGround.Gravel;
            if(Any(w,"soil","dirt","mud","muddy","sand","sandy","ground","earth"))return ImportedGround.Soil;
            return null;
        }
        public static ImportedMotion? Motion(string name)
        {
            string w=Words(name);
            if(Any(w,"turn","turning","pivot","stairs","stair","climb","climbing","carry","carrying","wounded","injured","sad","attack","aim","shoot","rifle","pistol","bow"))return null;
            bool walk=Any(w,"walk","walking"),run=Any(w,"run","running","jog","jogging"),crouch=Any(w,"crouch","crouched","crouching"),idle=Has(w,"idle");
            bool back=Any(w,"back","backward","backwards"),left=Has(w,"left"),right=Has(w,"right"),strafe=Any(w,"strafe","strafing","sidestep");
            if(crouch)return walk?(ImportedMotion?)ImportedMotion.CrouchWalk:idle?ImportedMotion.CrouchIdle:null;
            if(run)
            {
                if(left)return ImportedMotion.RunLeft;if(right)return ImportedMotion.RunRight;if(back)return ImportedMotion.RunBack;
                return Any(w,"fast","sprint","sprinting")?ImportedMotion.Sprint:ImportedMotion.Run;
            }
            if(Any(w,"sprint","sprinting"))return ImportedMotion.Sprint;
            if(walk || strafe)
            {if(left)return ImportedMotion.StrafeLeft;if(right)return ImportedMotion.StrafeRight;return back?ImportedMotion.WalkBack:ImportedMotion.Walk;}
            return idle?(ImportedMotion?)ImportedMotion.Idle:null;
        }
        public static int MotionScore(string name,string path,ImportedMotion role)
        {
            if(Motion(name)!=role)return -1;
            string w=Words(path+" "+name);
            int score=10+(Has(w,"female")?12:Has(w,"male")?0:5);
            if(Has(w,"inplace") || w.Contains(" in place "))score+=8;
            if(Has(w,"01") || Has(w,"1"))score+=2;
            return score;
        }
        public static ImportedMap Map(string name)
        {
            string w=Words(name);
            if(Any(w,"normal","normals","normalgl","normaldx","nrm","nor"))return ImportedMap.Normal;
            if(Any(w,"roughness","rough"))return ImportedMap.Roughness;
            if(Any(w,"smoothness","smooth","glossiness","gloss"))return ImportedMap.Smoothness;
            if(Any(w,"occlusion","ao"))return ImportedMap.Occlusion;
            if(Any(w,"height","displacement","disp"))return ImportedMap.Height;
            if(Any(w,"metallic","metalness"))return ImportedMap.Metallic;
            if(Any(w,"albedo","diffuse","color","colour","basecolor","maintex","alb","col"))return ImportedMap.Color;
            return ImportedMap.Unknown;
        }
        public static string TextureKey(string name)
        {
            string w=Words(name);
            foreach(string token in new[]{"base","color","colour","basecolor","albedo","diffuse","maintex","alb","col","normal","normals","normalgl","normaldx","nrm","nor","roughness","rough","smoothness","smooth","glossiness","gloss","ambient","occlusion","ao","height","displacement","disp","metallic","metalness","gl","dx","opengl","directx","1k","2k","4k","8k","1024","2048","4096","8192"})
                w=w.Replace(" "+token+" "," ");
            // Words() splits digit-letter boundaries: remove the remaining k token.
            w=Regex.Replace(w," [1248] k "," ");
            return Regex.Replace(w," +"," ").Trim();
        }
        public static bool Moving(ImportedMotion role)=>role!=ImportedMotion.Idle && role!=ImportedMotion.CrouchIdle;
        public static float CadenceRatio(float oldSpeed,float newSpeed)
        {
            if(!Finite(oldSpeed) || !Finite(newSpeed) || oldSpeed<.15f || newSpeed<.15f)
                throw new ArgumentException("Не измерена скорость контакта стоп.");
            float ratio=oldSpeed/newSpeed;
            if(ratio<.25f || ratio>4)throw new ArgumentException("Несовместимые скорости исходного и нового клипа.");
            return ratio;
        }
        public static float Smoothness(float roughness)=>1-Clamp(roughness);
        public static float Clamp(float value)=>Math.Max(0,Math.Min(1,value));
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }
}
