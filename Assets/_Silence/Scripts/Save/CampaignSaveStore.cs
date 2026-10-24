using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Silence.Core;
using Silence.Player;

namespace Silence.Save
{
    [Serializable] public sealed class SavedPlayer { public int capacity, batteryPacks, batteryPackCapacity; public string[] items; }
    [Serializable] public sealed class CampaignSave
    {
        public int version = 2, chapter;
        public bool helped, research;
        public SavedPlayer[] players;
    }
    public static class CampaignSaveStore
    {
        private static string PathName => Path.Combine(Application.persistentDataPath, "silence-checkpoint-v1.json");
        public static bool Exists => File.Exists(PathName);
        public static void Save(CampaignModel campaign, IReadOnlyList<PlayerContext> actors)
        {
            var save = new CampaignSave { chapter = (int)campaign.Chapter, helped = campaign.HelpedSurvivors,
                research = campaign.PreservedResearch, players = actors.Select(a => new SavedPlayer {
                    capacity = a.Inventory.Capacity, batteryPacks = a.Inventory.BatteryPacks, batteryPackCapacity = a.Inventory.BatteryPackCapacity, items = a.Inventory.Items.Where(id => id != "held_prop").ToArray() }).ToArray() };
            string temporary = PathName + ".tmp";
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
            if (File.Exists(PathName)) File.Replace(temporary, PathName, PathName + ".bak");
            else File.Move(temporary, PathName);
        }
        public static bool TryLoad(out CampaignSave save)
        {
            save = null;
            try
            {
                if (!File.Exists(PathName)) return false;
                var parsed = JsonUtility.FromJson<CampaignSave>(File.ReadAllText(PathName));
                if (parsed == null || (parsed.version != 1 && parsed.version != 2) || parsed.chapter < 0 || parsed.chapter > 6 ||
                    parsed.players == null || parsed.players.Length < 1 || parsed.players.Length > 4) return false;
                foreach (var player in parsed.players)
                {
                    if (player == null || player.batteryPacks < 0 || player.batteryPacks > 12 || player.batteryPackCapacity < 0 || player.batteryPackCapacity > 12 || player.batteryPacks > player.batteryPackCapacity || player.items == null || player.items.Any(id =>
                        id != "battery" && id != "medicine" && id != "rescue_kit")) return false;
                    new InventoryModel().ApplySnapshot(player.capacity, player.items);
                }
                save = parsed; return true;
            }
            catch (Exception exception) { Debug.LogWarning("SILENCE: invalid checkpoint: " + exception.Message); return false; }
        }
    }
}
