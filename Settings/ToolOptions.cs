using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace NFS_Minimap_Maker
{
    public class ToolOptions
    {
        private readonly Form form;
        private readonly ContextMenuStrip themeMenu;
        private readonly ToolStripMenuItem toggleThemeMenuItem;
        private readonly ToolStripMenuItem openFolderItem;
        private readonly ToolStripMenuItem checkUpdatesItem;
        private readonly ToolStripMenuItem gameSelectionMenuItem;

        private ToolStripMenuItem underground2MenuItem;
        private ToolStripMenuItem mostWantedMenuItem;

        private readonly ToolStripMenuItem processingModeMenuItem;
        private readonly ToolStripMenuItem fastModeItem;
        private readonly ToolStripMenuItem slowModeItem;

        private static bool isDarkThemeActive;
        private static NfsGame selectedGame = NfsGame.MostWanted;
        public static bool IsFastMode { get; private set; } = true;

        public static bool IsDarkTheme => isDarkThemeActive;
        public static NfsGame SelectedGame => selectedGame;

        private readonly string configFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FiLixsi", "NFS-Minimap-Maker");
        private readonly string configFile;
        private readonly string tempFolderPath;

        public ToolOptions(Form targetForm)
        {
            form = targetForm ?? throw new ArgumentNullException(nameof(targetForm));

            configFile = Path.Combine(configFolderPath, "settings.json");
            tempFolderPath = Path.Combine(configFolderPath, "Temp");
            LoadConfig();

            themeMenu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowCheckMargin = false
            };

            toggleThemeMenuItem = new ToolStripMenuItem(GetThemeToggleLabel());
            toggleThemeMenuItem.Click += ThemeToggleItem_Click;
            themeMenu.Items.Add(toggleThemeMenuItem);

            themeMenu.Items.Add(new ToolStripSeparator());

            gameSelectionMenuItem = new ToolStripMenuItem("For NFS");

            underground2MenuItem = new ToolStripMenuItem("Need for Speed - Underground 2")
            {
                Tag = NfsGame.Underground2,
                Enabled = false
            };

            mostWantedMenuItem = new ToolStripMenuItem("Need for Speed - Most Wanted")
            {
                Tag = NfsGame.MostWanted
            };
            mostWantedMenuItem.Click += (s, e) =>
            {
                selectedGame = (NfsGame)((ToolStripMenuItem)s).Tag;
                UpdateGameSelection();
                SaveConfig();
            };

            gameSelectionMenuItem.DropDownItems.Add(mostWantedMenuItem);
            gameSelectionMenuItem.DropDownItems.Add(underground2MenuItem);

            themeMenu.Items.Add(gameSelectionMenuItem);

            // --- Новый блок "Processing Mode" ---
            processingModeMenuItem = new ToolStripMenuItem("Processing Mode");

            fastModeItem = new ToolStripMenuItem("Fast Mode") { Tag = true };
            slowModeItem = new ToolStripMenuItem("Slow Mode") { Tag = false };

            fastModeItem.Click += (s, e) =>
            {
                IsFastMode = true;
                UpdateProcessingMode();
                SaveConfig();
            };

            slowModeItem.Click += (s, e) =>
            {
                IsFastMode = false;
                UpdateProcessingMode();
                SaveConfig();
            };

            processingModeMenuItem.DropDownItems.Add(fastModeItem);
            processingModeMenuItem.DropDownItems.Add(slowModeItem);
            themeMenu.Items.Add(processingModeMenuItem);
            // -----------------------------------

            openFolderItem = new ToolStripMenuItem("Open Temp Folder");
            openFolderItem.Click += OpenFolderItem_Click;
            themeMenu.Items.Add(openFolderItem);

            checkUpdatesItem = new ToolStripMenuItem("Check for Updates");
            checkUpdatesItem.Click += CheckUpdatesItem_Click;
            themeMenu.Items.Add(checkUpdatesItem);

            form.HandleCreated += (s, e) => new SysMenuHook(form, ShowThemeMenu);
            form.Load += (s, e) =>
            {
                ApplyTheme(isDarkThemeActive);
                UpdateGameSelection();
                UpdateProcessingMode();
            };
        }

        private string GetThemeToggleLabel() => isDarkThemeActive ? "Light Theme" : "Dark Theme";

        private void ShowThemeMenu()
        {
            toggleThemeMenuItem.Text = GetThemeToggleLabel();
            UpdateGameSelection();
            UpdateProcessingMode();
            themeMenu.Show(Cursor.Position);
        }

        private void ThemeToggleItem_Click(object sender, EventArgs e)
        {
            isDarkThemeActive = !isDarkThemeActive;
            ApplyTheme(isDarkThemeActive);
            SaveConfig();
        }

        private void UpdateGameSelection()
        {
            string check = "✓ ";

            mostWantedMenuItem.Text = (selectedGame == NfsGame.MostWanted)
                ? $"{check}Need for Speed - Most Wanted"
                : "Need for Speed - Most Wanted";

            underground2MenuItem.Text = (selectedGame == NfsGame.Underground2)
                ? $"{check}Need for Speed - Underground 2"
                : "Need for Speed - Underground 2";
        }

        private void UpdateProcessingMode()
        {
            string check = "✓ ";
            fastModeItem.Text = IsFastMode ? $"{check}Fast Mode" : "Fast Mode";
            slowModeItem.Text = !IsFastMode ? $"{check}Slow Mode" : "Slow Mode";
        }

        private void OpenFolderItem_Click(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(configFolderPath);
                Directory.CreateDirectory(tempFolderPath);
                System.Diagnostics.Process.Start("explorer.exe", tempFolderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open folder:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CheckUpdatesItem_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/FiLixsi/NFS-Minimap-Maker",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open browser:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyTheme(bool dark)
        {
            var theme = dark ? Theme.Dark : Theme.Light;
            form.BackColor = theme.Background;
            form.ForeColor = theme.Foreground;
            ApplyThemeToControls(form.Controls, theme);
        }

        private void ApplyThemeToControls(Control.ControlCollection controls, Theme theme)
        {
            foreach (Control control in controls)
            {
                if (control is Panel or Label or GroupBox)
                {
                    control.BackColor = theme.Background;
                    control.ForeColor = theme.Foreground;
                }
                else if (control is Button btn)
                {
                    btn.BackColor = theme.ButtonBack;
                    btn.ForeColor = theme.ButtonFore;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = theme.ButtonBorder;
                    btn.FlatAppearance.MouseOverBackColor = theme.ButtonHover;
                    btn.FlatAppearance.MouseDownBackColor = theme.ButtonPressed;
                }
                else if (control is TextBox or NumericUpDown)
                {
                    control.BackColor = theme.InputBack;
                    control.ForeColor = theme.InputFore;
                }
                else if (control is ComboBox cb)
                {
                    cb.BackColor = theme.InputBack;
                    cb.ForeColor = theme.InputFore;
                }

                if (control.HasChildren)
                    ApplyThemeToControls(control.Controls, theme);
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configFile))
                {
                    string json = File.ReadAllText(configFile);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);

                    isDarkThemeActive = cfg?.DarkTheme ?? false;
                    selectedGame = Enum.TryParse(cfg?.SelectedGame, out NfsGame parsedGame)
                        ? parsedGame
                        : NfsGame.MostWanted;
                    IsFastMode = cfg?.FastMode ?? false;
                }
                else
                {
                    isDarkThemeActive = false;
                    selectedGame = NfsGame.MostWanted;
                    IsFastMode = false;
                    SaveConfig();
                }
            }
            catch
            {
                isDarkThemeActive = false;
                selectedGame = NfsGame.MostWanted;
                IsFastMode = true;
            }
        }

        private void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(configFolderPath);
                var cfg = new AppConfig
                {
                    DarkTheme = isDarkThemeActive,
                    SelectedGame = selectedGame.ToString(),
                    FastMode = IsFastMode
                };
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
                };
                File.WriteAllText(configFile, JsonSerializer.Serialize(cfg, options));
            }
            catch { }
        }

        private class AppConfig
        {
            public bool DarkTheme { get; set; }
            public string SelectedGame { get; set; }
            public bool FastMode { get; set; }
        }

        private class SysMenuHook : NativeWindow
        {
            private const int WM_NCLBUTTONDOWN = 0x00A1;
            private const int HTSYSMENU = 3;
            private readonly Action onSysIconClick;

            public SysMenuHook(Form f, Action callback)
            {
                onSysIconClick = callback;
                AssignHandle(f.Handle);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_NCLBUTTONDOWN && (int)m.WParam == HTSYSMENU)
                {
                    onSysIconClick?.Invoke();
                    return;
                }
                base.WndProc(ref m);
            }
        }

        public enum NfsGame
        {
            MostWanted,
            Underground2
        }

        private record Theme(
            Color Background,
            Color Foreground,
            Color InputBack,
            Color InputFore,
            Color ButtonBack,
            Color ButtonFore,
            Color ButtonBorder,
            Color ButtonHover,
            Color ButtonPressed)
        {
            public static readonly Theme Dark = new(
                Color.FromArgb(40, 40, 45),
                Color.FromArgb(230, 230, 230),
                Color.FromArgb(55, 55, 60),
                Color.White,
                Color.FromArgb(70, 70, 75),
                Color.White,
                Color.FromArgb(90, 90, 95),
                Color.FromArgb(85, 85, 90),
                Color.FromArgb(60, 60, 65)
            );

            public static readonly Theme Light = new(
                Color.FromArgb(240, 240, 240),
                Color.Black,
                Color.White,
                Color.Black,
                Color.FromArgb(230, 230, 230),
                Color.Black,
                Color.LightGray,
                Color.FromArgb(210, 210, 210),
                Color.FromArgb(180, 180, 180)
            );
        }
    }
}
