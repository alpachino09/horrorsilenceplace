using UnityEngine;
using Silence.Core;
using Silence.Player;
using Silence.Interactions;
using Silence.Audio;

namespace Silence.UI
{
    // Intentionally no crosshair. This view displays replicated state, never mutates the world.
    public sealed class GameplayHud : MonoBehaviour
    {
        private PlayerContext actor;
        private InteractionScanner scanner;
        private ActorGameplayInput commands;
        private MicrophoneNoiseMeter microphone;
        private bool inventory, journal, voteSent;
        private bool wasAlive = true;
        private RoundPhase previous;
        private Vector2 scroll;
        private void Awake()
        {
            actor = GetComponent<PlayerContext>(); scanner = GetComponent<InteractionScanner>();
            commands = GetComponent<ActorGameplayInput>(); microphone = GetComponent<MicrophoneNoiseMeter>();
        }
        private static string T(string ru, string en) => PrototypePreferences.English ? en : ru;
        private void Update()
        {
            if (!actor.IsLocalOwner || actor.Game == null || !actor.Game.Started) return;
            if (wasAlive != actor.Vitals.Alive)
            {
                wasAlive = actor.Vitals.Alive; inventory = journal = false;
                PlayerLook.Capture(wasAlive && actor.Game.Phase == RoundPhase.Playing);
            }
            if (actor.Input.InventoryPressed) { inventory = !inventory; journal = false; PlayerLook.Capture(!inventory && actor.Vitals.Alive); }
            if (previous != actor.Game.Phase)
            {
                previous = actor.Game.Phase; voteSent = false; inventory = journal = false;
                PlayerLook.Capture(previous == RoundPhase.Playing && actor.Vitals.Alive);
            }
        }
        private void OnGUI()
        {
            var game = actor.Game;
            if (!actor.IsLocalOwner || game == null || !game.Started) return;
            float scale = Mathf.Clamp(Screen.height / 900f, .6f, 1.5f);
            Matrix4x4 old = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            var wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.BeginArea(new Rect(25, 20, 390, 170));
            GUILayout.Label(T(game.Chapter.titleRu, game.Chapter.titleEn));
            MissionFlags flags = game.VisibleMission;
            GUILayout.Label(((flags & MissionFlags.Battery) != 0 ? "✓ " : "○ ") + T("Батарейка", "Battery") + "    " +
                ((flags & MissionFlags.Signal) != 0 ? "✓ " : "○ ") + T("Сигнал", "Signal") + "    " +
                ((flags & MissionFlags.Document) != 0 ? "✓ " : "○ ") + T("Документ", "Document"));
            GUILayout.Label(T("Шум", "Noise")); Meter(new Color(.7f, .4f, .2f), actor.NoiseLevel, 170);
            GUILayout.Label(T("Силы", "Stamina")); Meter(new Color(.35f, .55f, .4f), actor.Stamina.Value, 170);
            GUILayout.Label(T("Фонарь: ", "Flashlight: ") + Mathf.RoundToInt(actor.FlashlightCharge * 100) + "%");
            if (actor.Flashlight != null) GUILayout.Label(T("Батарейки: ", "Spare packs: ") + actor.Inventory.BatteryPacks + " · R");
            GUILayout.EndArea();
            if (actor.Vitals.State == VitalState.Wounded)
                GUI.Box(new Rect(width / 2 - 225, 25, 450, 45), T("Ранение. Лекарство поможет; двигайтесь тише.", "Wounded. Use medicine; move quietly."));
            if (!actor.Vitals.Alive && game.Phase == RoundPhase.Playing)
                GUI.Box(new Rect(width / 2 - 250, 25, 500, 70), T("Вы погибли. Наблюдение за группой.\nСоюзник может применить спасательный набор.",
                    "You died. Spectating your group.\nA teammate can use a rescue kit."));
            if (actor.Input.GameplayActive && actor.Vitals.Alive && scanner.Current != null)
                GUI.Box(new Rect(width / 2 - 340, height - 85, 680, 45), scanner.Current is ManualDoor
                    ? scanner.Current.GetPrompt(PrototypePreferences.English)
                    : actor.Input.InteractionBindingLabel + ": " + scanner.Current.GetPrompt(PrototypePreferences.English));
            if (Time.unscaledTime < actor.MessageUntil)
            {
                GUILayout.BeginArea(new Rect(width / 2 - 350, 190, 700, 130), GUI.skin.box);
                GUILayout.Label(actor.Message, wrap); GUILayout.EndArea();
            }
            if (inventory)
            {
                GUILayout.BeginArea(new Rect(25, height - 350, 330, 330), GUI.skin.box);
                GUILayout.Label(T("Карманы · цифры — применить · G — положить", "Pockets · number — use · G — drop"), wrap);
                for (int i = 0; i < actor.Inventory.Capacity; i++)
                {
                    string name = i < actor.Inventory.Items.Count ? ItemName(actor.Inventory.Items[i]) : T("Пусто", "Empty");
                    if (GUILayout.Button((commands.SelectedSlot == i ? "› " : "") + (i + 1) + ". " + name)) commands.SelectedSlot = i;
                }
                if (GUILayout.Button(T("Применить выбранное", "Use selected"))) commands.Send(ActorAction.UseSlot, commands.SelectedSlot);
                if (GUILayout.Button(T("Положить выбранное / отпустить", "Drop selected / release"))) commands.Send(ActorAction.Drop, commands.SelectedSlot);
                GUILayout.Label(T("Предмет в руках: ЛКМ — бросить, G — отпустить", "Held object: left mouse — throw, G — release"), wrap);
                if (GUILayout.Button(T("Запись главы", "Chapter journal"))) journal = !journal;
                GUILayout.EndArea();
            }
            if (journal)
            {
                GUILayout.BeginArea(new Rect(width / 2 - 310, height / 2 - 170, 620, 340), GUI.skin.box);
                GUILayout.Label(T(game.Chapter.titleRu, game.Chapter.titleEn));
                GUILayout.Label((flags & MissionFlags.Document) != 0 ? T(game.Chapter.noteRu, game.Chapter.noteEn)
                    : T("Запись ещё не найдена.", "The document has not been found."), wrap);
                if (GUILayout.Button(T("Закрыть", "Close"))) journal = false;
                GUILayout.EndArea();
            }
            if (game.Phase == RoundPhase.Voting)
            {
                GUILayout.BeginArea(new Rect(width / 2 - 280, height / 2 - 100, 560, 200), GUI.skin.box);
                GUILayout.Label(T(game.Chapter.voteRu, game.Chapter.voteEn), wrap);
                GUILayout.Label(T("Осталось: ", "Remaining: ") + Mathf.CeilToInt(game.PhaseSeconds));
                GUILayout.Label(T("Общее решение группы. При равенстве — оставить путь открытым.", "Group decision. A tie keeps the route open."), wrap);
                if (!voteSent && actor.Vitals.Alive)
                {
                    if (GUILayout.Button(T("Да", "Yes"))) voteSent = commands.Send(ActorAction.Vote, 1);
                    if (GUILayout.Button(T("Нет", "No"))) voteSent = commands.Send(ActorAction.Vote, 0);
                }
                else GUILayout.Label(T("Ожидание остальных…", "Waiting for the group…"));
                GUILayout.EndArea();
            }
            else if (game.Phase == RoundPhase.Wipe || game.Phase == RoundPhase.Transition || game.Phase == RoundPhase.Completed)
            {
                GUI.Box(new Rect(0, 0, width, height), "");
                GUILayout.BeginArea(new Rect(width / 2 - 300, height / 2 - 100, 600, 230), GUI.skin.box);
                GUILayout.Label(game.Phase == RoundPhase.Wipe ? T("Группа погибла. Возврат к началу главы…", "The group is lost. Restarting this chapter…") :
                    game.Phase == RoundPhase.Transition ? T("Переход к следующей главе…", "Travelling to the next chapter…") : EndingText(game.VisibleEnding), wrap);
                GUILayout.EndArea();
            }
            else if (!actor.Input.GameplayActive && actor.Vitals.Alive && !inventory && !journal) Settings(width, height, wrap);
            GUI.matrix = old;
        }
        private void Settings(float width, float height, GUIStyle wrap)
        {
            GUILayout.BeginArea(new Rect(width / 2 - 280, height / 2 - 290, 560, 580), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("SILENCE");
            if (GUILayout.Button(T("Продолжить", "Resume"))) PlayerLook.Capture(true);
            if (GUILayout.Button(T("Язык: русский", "Language: English"))) PrototypePreferences.English = !PrototypePreferences.English;
            GUILayout.Label(T("Чувствительность мыши", "Mouse sensitivity"));
            PrototypePreferences.MouseSensitivity = GUILayout.HorizontalSlider(PrototypePreferences.MouseSensitivity, .02f, .4f);
            QualitySettings.vSyncCount = GUILayout.Toggle(QualitySettings.vSyncCount > 0, "VSync") ? 1 : 0;
            GUILayout.Label(T("Дальность теней", "Shadow distance"));
            QualitySettings.shadowDistance = GUILayout.HorizontalSlider(QualitySettings.shadowDistance, 15, 70);
            GUILayout.Label(T("Шум микрофона для существа — по желанию", "Optional microphone noise for the creature"));
            if (!microphone.Capturing)
            {
                GUILayout.Label(T("При включении измеряется громкость. Голос не передаётся и не сохраняется.",
                    "When enabled, only loudness is measured. Voice is neither transmitted nor saved."), wrap);
                if (GUILayout.Button(T("Включить измерение микрофона", "Enable microphone meter"))) microphone.EnableWithConsent();
            }
            else
            {
                microphone.Muted = GUILayout.Toggle(microphone.Muted, T("Заглушить микрофон для существа", "Mute creature microphone input"));
                GUILayout.Label(T("Чувствительность", "Sensitivity"));
                microphone.Sensitivity = GUILayout.HorizontalSlider(microphone.Sensitivity, 1, 35);
                Meter(Color.green, microphone.Level, 250);
                if (GUILayout.Button(T("Настроить под тишину комнаты", "Calibrate room silence"))) microphone.Calibrate();
                if (GUILayout.Button(T("Выключить микрофон", "Disable microphone"))) microphone.DisableCapture();
            }
            GUILayout.Label(microphone.Status, wrap);
            GUILayout.Label(T("Голосовая связь между игроками пока не подключена.", "Player voice communication is not connected yet."), wrap);
            GUILayout.Label(T("WASD · мышь · Shift — бег · Ctrl — присесть · E — действие · F — фонарь · Tab — карманы · Esc — меню",
                "WASD · mouse · Shift — run · Ctrl — crouch · E — interact · F — flashlight · Tab — pockets · Esc — menu"), wrap);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private static void Meter(Color color, float value, float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 6, GUILayout.Width(width)); Color old = GUI.color;
            GUI.color = new Color(.1f, .12f, .13f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color; rect.width *= Mathf.Clamp01(value); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        private static string ItemName(string id)
        {
            switch (id)
            {
                case "flashlight_cells": return T("Комплект батареек", "Battery pack"); case "battery": return T("Батарейка", "Battery"); case "medicine": return T("Лекарство", "Medicine");
                case "document": return T("Документ", "Document"); case "rescue_kit": return T("Спасательный набор", "Rescue kit");
                case "held_prop": return T("Занято переноской", "Carrying an object"); default: return id;
            }
        }
        private static string EndingText(Ending ending) => ending == Ending.Hope
            ? T("НАДЕЖДА\nВы сохранили данные и не бросили людей. Передача ушла за пределы карантина.", "HOPE\nYou preserved the research and helped survivors. The transmission escaped quarantine.")
            : ending == Ending.Outbreak ? T("ВСПЫШКА\nБез предупреждения маршрут эвакуации стал дорогой для заражения.", "OUTBREAK\nWithout a warning, the evacuation route carried the outbreak onward.")
            : T("НЕИЗВЕСТНОСТЬ\nВы выбрались. Ответ на последнюю передачу так и не пришёл.", "UNCERTAINTY\nYou escaped. No answer came to the final transmission.");
    }
}
