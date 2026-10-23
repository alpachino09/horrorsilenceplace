using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Silence.Graphics;

namespace Silence.Multiplayer.Editor.Visual
{
    internal static class VisualPerformanceRecorder
    {
        [Serializable]private sealed class Report
        {
            public string unity,utc,device,gpu,preset,scope="Editor frame samples; build measurements still required";
            public bool completed,gpuTimingAvailable;
            public int frames,width,height;
            public float meanFrameMs,p95FrameMs,meanFps;
            public double gpuMeanMs;
            public long allocatedBytes,reservedBytes;
        }
        private static readonly List<float> samples=new List<float>();
        private static readonly List<double> gpuSamples=new List<double>();
        private static readonly FrameTiming[] timings=new FrameTiming[1];
        private static double finish;
        private static int previousFrame;
        private static bool recording;
        public static void Start()
        {
            if(recording)throw new InvalidOperationException("Performance recording is already running.");
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play and reach the farm before recording.");
            samples.Clear();gpuSamples.Clear();finish=EditorApplication.timeSinceStartup+15;previousFrame=-1;recording=true;
            EditorApplication.update+=Tick;Debug.Log("SILENCE: recording 15 seconds of actual Editor frame times. Keep playing; no FPS target is assumed.");
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup>=finish){Finish(EditorApplication.isPlaying);return;}
            if(previousFrame==Time.frameCount)return;previousFrame=Time.frameCount;
            if(Time.unscaledDeltaTime>0)samples.Add(Time.unscaledDeltaTime*1000);
            FrameTimingManager.CaptureFrameTimings();
            if(FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].gpuFrameTime>0)gpuSamples.Add(timings[0].gpuFrameTime);
        }
        private static void Finish(bool completed)
        {
            EditorApplication.update-=Tick;recording=false;
            float[] sorted=samples.OrderBy(v=>v).ToArray();var quality=UnityEngine.Object.FindAnyObjectByType<GraphicsQualityManager>();
            var report=new Report{unity=Application.unityVersion,utc=DateTime.UtcNow.ToString("O"),device=SystemInfo.deviceModel,gpu=SystemInfo.graphicsDeviceName,
                preset=quality!=null&&quality.Active!=null?quality.Active.preset.ToString():"unknown",completed=completed&&samples.Count>=30,
                frames=samples.Count,width=Screen.width,height=Screen.height,meanFrameMs=samples.Count>0?samples.Average():0,
                p95FrameMs=sorted.Length>0?sorted[Mathf.Min(sorted.Length-1,Mathf.FloorToInt(sorted.Length*.95f))]:0,
                gpuTimingAvailable=gpuSamples.Count>0,gpuMeanMs=gpuSamples.Count>0?gpuSamples.Average():0,
                allocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),reservedBytes=Profiler.GetTotalReservedMemoryLong()};
            report.meanFps=report.meanFrameMs>0?1000/report.meanFrameMs:0;
            Directory.CreateDirectory("Library/Silence");string path="Library/Silence/VisualPerformance-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json";
            File.WriteAllText(path,JsonUtility.ToJson(report,true));Debug.Log("SILENCE: recorded "+report.frames+" Editor frame samples: "+path+". GPU timings may be unavailable; this is not a standalone-build certification.");
        }
    }
}
