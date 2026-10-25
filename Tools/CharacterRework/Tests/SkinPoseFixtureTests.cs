using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class SkinPoseFixtureTests
    {
        private static RigVector V(JsonElement e)=>new RigVector(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle());
        private static SkinTransform M(JsonElement e)=>new SkinTransform(V(e[0]),V(e[1]),V(e[2]),V(e[3]));
        [Test] public void SkinningMatchesActualBlenderArmatureOnTheDeliveredBody()
        {
            using(var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"SkinPoseFixture.json"))))
            {
                var root=data.RootElement;var bind=root.GetProperty("bind").EnumerateArray().Select(M).ToArray();
                float worst=0;int comparisons=0;
                foreach(var frame in root.GetProperty("poses").EnumerateArray())
                {
                    var bones=frame.GetProperty("bones").EnumerateArray().Select(M).ToArray();
                    var transforms=bones.Select((b,i)=>b*bind[i]).ToArray();int index=0;
                    foreach(var vertex in root.GetProperty("vertices").EnumerateArray())
                    {
                        var w=vertex.GetProperty("weights");
                        var weights=new SkinWeights4(w[0][0].GetInt32(),w[0][1].GetSingle(),w[1][0].GetInt32(),w[1][1].GetSingle(),
                            w[2][0].GetInt32(),w[2][1].GetSingle(),w[3][0].GetInt32(),w[3][1].GetSingle());
                        var actual=SkinPoseModel.Position(V(vertex.GetProperty("vertex")),weights,transforms);
                        var expected=V(frame.GetProperty("expected")[index++]);float error=(actual-expected).Length;
                        worst=Math.Max(worst,error);comparisons++;
                        Assert.That(error,Is.LessThan(.0002f),frame.GetProperty("clip").GetString()+" vertex "+vertex.GetProperty("index"));
                    }
                }
                Assert.That(comparisons,Is.EqualTo(448));
                TestContext.WriteLine("448 real Blender vertex/pose comparisons; worst error (m): "+worst);
            }
        }
    }
}
