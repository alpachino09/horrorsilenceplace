using System;
using System.Collections.Generic;
using System.Linq;

namespace Silence.Core
{
    [Flags]
    public enum MissionFlags : byte { None = 0, Battery = 1, Signal = 2, Document = 4 }
    public enum Ending : byte { None, Hope, Outbreak, Uncertain }

    public sealed class CampaignModel
    {
        public ChapterId Chapter { get; private set; }
        public MissionFlags Mission { get; private set; }
        public bool HelpedSurvivors { get; private set; }
        public bool PreservedResearch { get; private set; }
        public Ending Ending { get; private set; }
        public bool CanExit => (Mission & (MissionFlags.Battery | MissionFlags.Signal | MissionFlags.Document)) ==
            (MissionFlags.Battery | MissionFlags.Signal | MissionFlags.Document);
        public void Collected(string id)
        {
            if (id == "battery") Mission |= MissionFlags.Battery;
            if (id == "document") Mission |= MissionFlags.Document;
        }
        public bool ReceiveSignal()
        {
            if ((Mission & MissionFlags.Battery) == 0) return false;
            Mission |= MissionFlags.Signal;
            return true;
        }
        public void ApplyChoice(bool help)
        {
            if (Chapter == ChapterId.Town || Chapter == ChapterId.Lake) HelpedSurvivors |= help;
            if (Chapter == ChapterId.RadioStation) PreservedResearch = help;
        }
        public bool Advance()
        {
            if (!CanExit || Ending != Ending.None) return false;
            if (Chapter == ChapterId.Laboratory)
                Ending = PreservedResearch ? (HelpedSurvivors ? Ending.Hope : Ending.Uncertain) : Ending.Outbreak;
            else { Chapter = (ChapterId)((int)Chapter + 1); Mission = MissionFlags.None; }
            return true;
        }
        public void RestartChapter() => Mission = MissionFlags.None;
        public void Restore(ChapterId chapter, bool helped, bool research)
        {
            if (chapter < ChapterId.Farm || chapter > ChapterId.Laboratory) throw new ArgumentOutOfRangeException(nameof(chapter));
            Chapter = chapter; HelpedSurvivors = helped; PreservedResearch = research; Mission = MissionFlags.None; Ending = Ending.None;
        }
    }

    public sealed class VoteModel
    {
        private readonly HashSet<ulong> members;
        private readonly Dictionary<ulong, bool> votes = new Dictionary<ulong, bool>();
        public bool Resolved { get; private set; }
        public bool Result { get; private set; }
        public double Deadline { get; }
        public int VotedCount => votes.Count;
        public VoteModel(IEnumerable<ulong> participants, double now, double duration = 20)
        {
            if (participants == null || (double.IsNaN(now) || double.IsInfinity(now)) || (double.IsNaN(duration) || double.IsInfinity(duration)) || now < 0 || duration <= 0 || double.IsInfinity(now + duration))
                throw new ArgumentException("A finite vote window and participants are required.");
            members = new HashSet<ulong>(participants);
            if (members.Count == 0 || members.Count > InteractionRules.MaximumPlayers) throw new ArgumentException("Vote must have 1–4 participants.");
            Deadline = now + duration;
        }
        public bool Cast(ulong id, bool choice)
        {
            if (Resolved || !members.Contains(id) || votes.ContainsKey(id)) return false;
            votes.Add(id, choice); return true;
        }
        public void Remove(ulong id) { members.Remove(id); votes.Remove(id); }
        public bool Tick(double now)
        {
            if (Resolved) return true;
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0) return false;
            if (votes.Count < members.Count && now < Deadline) return false;
            // Equal votes and absent votes use the conservative affirmative choice.
            Result = votes.Values.Count(v => v) * 2 >= votes.Count;
            Resolved = true; return true;
        }
    }
}
