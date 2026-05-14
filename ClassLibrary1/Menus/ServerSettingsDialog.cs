using PeterHan.PLib.UI;
using ONI_MP;
using UnityEngine;
using ONI_MP.DebugTools;

namespace ONI_MP.Menus
{
    internal static class ServerSettingsDialog
    {
        public static void Show()
        {
            var config = Configuration.Instance;

            bool hardSync = config.Host.Server.HardSyncAtCycleStart;

            var dialog = new PDialog("ServerSettings")
            {
                Title = STRINGS.UI.CONFIGURATION.HEADERS.SERVER_SETTINGS,
                Size = new Vector2(340, 160),
                SortKey = 150f,
                DialogClosed = (key) =>
                {
                    if (key == global::STRINGS.UI.CONFIRMDIALOG.OK)
                    {
                        config.Host.Server.HardSyncAtCycleStart = hardSync;
                        config.Save();
                    }
                }
            };

            var checkbox = new PCheckBox("HardSync")
            {
                Text = STRINGS.UI.CONFIGURATION.TITLES.HOST_SETTINGS.SERVER_SETTINGS.HARD_SYNC_AT_CYCLE_START,
                ToolTip = STRINGS.UI.CONFIGURATION.TOOLTIPS.HOST_SETTINGS.SERVER_SETTINGS.HARD_SYNC_AT_CYCLE_START,
                InitialState = hardSync ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED,
                OnChecked = (source, state) =>
                {
                    hardSync = !hardSync;
                    PCheckBox.SetCheckState(source, hardSync ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED);
                }
            }.SetKleiBlueStyle();

            dialog.Body.AddChild(checkbox);
            dialog.AddButton(global::STRINGS.UI.CONFIRMDIALOG.OK, global::STRINGS.UI.CONFIRMDIALOG.OK, null, PUITuning.Colors.ButtonPinkStyle, null);
            dialog.Show();
        }
    }
}