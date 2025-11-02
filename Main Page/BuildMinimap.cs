using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFS_Minimap_Maker
{
    public partial class MainMenu
    {
        private const int TileGridSize = 8;
        private int processedTiles = 0;

        private async void buildMinimapButton_Click(object sender, EventArgs e)
        {
            if (originalImage == null)
            {
                MessageBox.Show("Please load an image first.", "No Image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetButtonsEnabled(false);
            EnsureProgressUi();
            progressBar.Value = 0;
            progressLabel.Text = "Building minimap...";

            displayImage = new Bitmap(originalImage.Width, originalImage.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(displayImage))
            {
                g.DrawImage(originalImage, 0, 0);
                using (Brush dark = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                    g.FillRectangle(dark, 0, 0, displayImage.Width, displayImage.Height);
            }

            pictureBox.Image = displayImage;
            pictureBox.Refresh();

            try
            {
                if (ToolOptions.IsFastMode)
                    await ProcessMinimapParallelAsync();
                else
                    await ProcessMinimapSequentialAsync();

                await VerifyAndFixTilesAsync();
                CleanupTempPngFiles();

                ToggleProgressUi(false);
                Invoke(() => pictureBox.Image = originalImage);

                string miniMapName = PromptForMiniMapName();

                if (string.IsNullOrEmpty(miniMapName))
                {
                    MessageBox.Show("Operation canceled.", "Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                MinimapConfig.MiniMapName = miniMapName;

                progressLabel.Text = "Creating BIN file...";
                await Binary.StartAsync(tempDir, miniMapName);

                string binTempPath = Path.Combine(tempDir, "MINIMAP.BIN");
                if (!File.Exists(binTempPath))
                {
                    MessageBox.Show("MINIMAP.BIN file not found in temp folder.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string renamedTempPath = Path.Combine(tempDir, miniMapName + ".BIN");
                try
                {
                    if (File.Exists(renamedTempPath))
                        File.Delete(renamedTempPath);
                    File.Move(binTempPath, renamedTempPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error renaming BIN file:\n" + ex.Message,
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FiLixsi", "NFS-Minimap-Maker", "settings.json");

                string lastLocal = null;

                try
                {
                    if (File.Exists(configPath))
                    {
                        string json = File.ReadAllText(configPath);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("LastLocal", out var element))
                            lastLocal = element.GetString();
                    }
                }
                catch { }

                if (string.IsNullOrWhiteSpace(lastLocal) || !Directory.Exists(lastLocal))
                    lastLocal = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Save MINI_MAP file";
                    saveDialog.Filter = "Binary files (*.BIN)|*.BIN";
                    saveDialog.FileName = miniMapName + ".BIN";
                    saveDialog.InitialDirectory = lastLocal;

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            File.Copy(renamedTempPath, saveDialog.FileName, true);

                            string lastDir = Path.GetDirectoryName(saveDialog.FileName);
                            SaveLastLocal(configPath, lastDir);

                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, true);
                                Directory.CreateDirectory(tempDir);
                            }

                            Task.Run(async () => await FadeOutPictureAsync());

                            MessageBox.Show("File saved successfully!", "Success",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error saving file:\n" + ex.Message,
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        try
                        {
                            if (Directory.Exists(tempDir))
                                Directory.Delete(tempDir, true);
                        }
                        catch { }

                        Invoke(() =>
                        {
                            pictureBox.Image = null;
                            buildMinimapButton.Enabled = false;
                        });

                        MessageBox.Show("Operation canceled.", "Canceled",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Invoke(() =>
                    {
                        progressBar.Visible = false;
                        progressLabel.Visible = false;
                        pictureBox.Image = null;
                    });

                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch { }

                MessageBox.Show("Compilation error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                displayImage?.Dispose();
                SetButtonsEnabled(true);

                buildMinimapButton.Enabled = false;
            }
        }

        private static void SaveLastLocal(string configPath, string lastDir)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));

                string json = File.Exists(configPath) ? File.ReadAllText(configPath) : "{}";
                var doc = JsonSerializer.Deserialize<JsonElement>(json);

                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();

                    if (doc.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.EnumerateObject())
                        {
                            if (prop.NameEquals("LastLocal"))
                                continue;
                            prop.WriteTo(writer);
                        }
                    }

                    writer.WriteString("LastLocal", lastDir);
                    writer.WriteEndObject();
                }

                File.WriteAllBytes(configPath, stream.ToArray());
            }
            catch { }
        }

        private async Task ProcessMinimapParallelAsync()
        {
            string texconvExePath = Path.Combine(tempDir, "texconv.exe");
            if (!File.Exists(texconvExePath) && !TryExtractTexconvFromResources(texconvExePath))
                throw new FileNotFoundException("texconv.exe not found in resources");

            Directory.CreateDirectory(tempDir);

            using Bitmap srcBmp = new Bitmap(originalImage);
            int tileW = srcBmp.Width / TileGridSize;
            int tileH = srcBmp.Height / TileGridSize;
            int totalTiles = TileGridSize * TileGridSize;

            if (tileW == 0 || tileH == 0)
                throw new InvalidOperationException("Image is too small for 8x8 split.");

            processedTiles = 0;
            var updateRequested = false;

            using (Graphics displayGraphics = Graphics.FromImage(displayImage))
            {
                displayGraphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                displayGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                displayGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                var tasks = Enumerable.Range(0, totalTiles)
                    .Select(i => ProcessTileAsync(i, srcBmp, texconvExePath, tileW, tileH, totalTiles, displayGraphics, () => updateRequested = true))
                    .ToList();

                const int maxConcurrent = 6;
                var activeTasks = new System.Collections.Generic.List<Task>();

                var uiUpdater = Task.Run(async () =>
                {
                    while (processedTiles < totalTiles)
                    {
                        if (updateRequested)
                        {
                            updateRequested = false;
                            Invoke(() =>
                            {
                                pictureBox.Invalidate();
                                UpdateProgressUi(processedTiles, totalTiles);
                            });
                        }
                        await Task.Delay(80);
                    }
                });

                foreach (var task in tasks)
                {
                    activeTasks.Add(task);
                    if (activeTasks.Count >= maxConcurrent)
                    {
                        var finished = await Task.WhenAny(activeTasks);
                        activeTasks.Remove(finished);
                    }
                }

                await Task.WhenAll(activeTasks);
                await uiUpdater;
            }
        }

        private async Task ProcessMinimapSequentialAsync()
        {
            string texconvExePath = Path.Combine(tempDir, "texconv.exe");
            if (!File.Exists(texconvExePath) && !TryExtractTexconvFromResources(texconvExePath))
                throw new FileNotFoundException("texconv.exe not found in resources");

            Directory.CreateDirectory(tempDir);

            using Bitmap srcBmp = new Bitmap(originalImage);
            int tileW = srcBmp.Width / TileGridSize;
            int tileH = srcBmp.Height / TileGridSize;
            int totalTiles = TileGridSize * TileGridSize;

            if (tileW == 0 || tileH == 0)
                throw new InvalidOperationException("Image is too small for 8x8 split.");

            processedTiles = 0;

            using (Graphics displayGraphics = Graphics.FromImage(displayImage))
            {
                displayGraphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                displayGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                displayGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                for (int i = 0; i < totalTiles; i++)
                {
                    await ProcessTileAsync(i, srcBmp, texconvExePath, tileW, tileH, totalTiles, displayGraphics, null);

                    Invoke(() =>
                    {
                        pictureBox.Invalidate();
                        UpdateProgressUi(processedTiles, totalTiles);
                    });
                }
            }
        }

        private async Task ProcessTileAsync(
            int index, Bitmap srcBmp, string texconvExePath,
            int tileW, int tileH, int totalTiles, Graphics displayGraphics, Action requestUiUpdate)
        {
            int y = index / TileGridSize;
            int x = index % TileGridSize;

            Rectangle rect = new Rectangle(x * tileW, y * tileH, tileW, tileH);
            string tileBasePath = Path.Combine(tempDir, $"tile_{index + 1:00}");
            string pngPath = tileBasePath + ".png";
            string ddsPath = Path.Combine(tempDir, $"image{index + 1:00}.dds");

            using (Bitmap tile = srcBmp.Clone(rect, PixelFormat.Format32bppArgb))
                tile.Save(pngPath, ImageFormat.Png);

            await ConvertTileToDdsAsync(texconvExePath, pngPath);
            await WaitForFileStableAsync(tileBasePath + ".dds");

            string expected = tileBasePath + ".dds";
            if (File.Exists(expected))
            {
                if (File.Exists(ddsPath)) File.Delete(ddsPath);
                File.Move(expected, ddsPath);
            }

            lock (displayImage)
            {
                displayGraphics.DrawImage(originalImage, rect, rect, GraphicsUnit.Pixel);
            }

            processedTiles++;
            requestUiUpdate?.Invoke();
        }

        private static async Task ConvertTileToDdsAsync(string exePath, string pngPath)
        {
            var psi = new ProcessStartInfo(exePath,
                $"-dx9 -srgb -y -m 1 -if BOX_DITHER_DIFFUSION -f BC2_UNORM_SRGB -o \"{Path.GetDirectoryName(pngPath)}\" \"{pngPath}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();
            await proc.WaitForExitAsync();
        }

        private static async Task WaitForFileStableAsync(string filePath, int checkInterval = 50, int stableTime = 100)
        {
            long lastSize = -1;
            long stableDuration = 0;
            var start = DateTime.Now;

            while (File.Exists(filePath))
            {
                long newSize = new FileInfo(filePath).Length;
                if (newSize == lastSize)
                {
                    stableDuration += checkInterval;
                    if (stableDuration >= stableTime)
                        return;
                }
                else
                {
                    stableDuration = 0;
                    lastSize = newSize;
                }

                await Task.Delay(checkInterval);
                if ((DateTime.Now - start).TotalSeconds > 5)
                    return;
            }
        }

        private async Task VerifyAndFixTilesAsync()
        {
            var missingTiles = Enumerable.Range(1, 64)
                .Where(i =>
                {
                    string ddsPath = Path.Combine(tempDir, $"image{i:00}.dds");
                    return !File.Exists(ddsPath) || new FileInfo(ddsPath).Length == 0;
                })
                .ToList();

            if (missingTiles.Count == 0)
                return;

            string texconvExePath = Path.Combine(tempDir, "texconv.exe");
            if (!File.Exists(texconvExePath))
                throw new FileNotFoundException("texconv.exe missing during verification.");

            using Bitmap srcBmp = new Bitmap(originalImage);
            int tileW = srcBmp.Width / TileGridSize;
            int tileH = srcBmp.Height / TileGridSize;

            foreach (int index in missingTiles)
            {
                int y = (index - 1) / TileGridSize;
                int x = (index - 1) % TileGridSize;
                Rectangle rect = new Rectangle(x * tileW, y * tileH, tileW, tileH);

                string tileBasePath = Path.Combine(tempDir, $"tile_{index:00}");
                string pngPath = tileBasePath + ".png";
                string ddsPath = Path.Combine(tempDir, $"image{index:00}.dds");

                using (Bitmap tile = srcBmp.Clone(rect, PixelFormat.Format32bppArgb))
                    tile.Save(pngPath, ImageFormat.Png);

                await ConvertTileToDdsAsync(texconvExePath, pngPath);
                await WaitForFileStableAsync(tileBasePath + ".dds");

                string expected = tileBasePath + ".dds";
                if (File.Exists(expected))
                {
                    if (File.Exists(ddsPath)) File.Delete(ddsPath);
                    File.Move(expected, ddsPath);
                }

                lock (displayImage)
                {
                    using (Graphics g = Graphics.FromImage(displayImage))
                        g.DrawImage(originalImage, rect, rect, GraphicsUnit.Pixel);
                }

                Invoke(() => pictureBox.Invalidate(rect));
            }
        }

        private void CleanupTempPngFiles()
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(tempDir, "*.png", SearchOption.TopDirectoryOnly))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch { }
        }

        private void UpdateProgressUi(int processedTiles, int totalTiles)
        {
            int percent = (int)((processedTiles / (float)totalTiles) * 100);
            progressBar.Value = percent;
            progressLabel.Text = $"Processed {processedTiles}/{totalTiles} ({percent}%)";
        }

        private string PromptForMiniMapName()
        {
            string promptText =
                "Examples:\n" +
                "• MINI_MAP\n" +
                "• MINI_MAP_15_1_1\n" +
                "• MINI_MAP_UNLOCK_1\n" +
                "• MINI_MAP_18_10_1_BC";

            while (true)
            {
                string input = ShowInputDialog(promptText, "Enter MINI_MAP name");

                if (input == null)
                    return null;

                input = input.Trim().ToUpper().Replace(' ', '_');

                if (IsMiniMapNameValid(input))
                    return input;

                MessageBox.Show("Invalid name format!\nPlease follow the naming rules.",
                    "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool IsMiniMapNameValid(string name)
        {
            if (!name.StartsWith("MINI_MAP")) return false;
            string pattern = @"^MINI_MAP(_[A-Z0-9]{1,6})?(_[0-9]{1,2})?(_[0-9]{1,2})?(_[A-Z0-9]{1,2})?$";
            return Regex.IsMatch(name, pattern);
        }

        public static string ShowInputDialog(string text, string caption)
        {
            Form prompt = new Form()
            {
                Width = 420,
                Height = 220,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label textLabel = new Label() { Left = 20, Top = 20, Width = 360, Height = 80, Text = text };
            TextBox textBox = new TextBox() { Left = 20, Top = 110, Width = 360, Text = "MINI_MAP" };
            Button okButton = new Button() { Text = "OK", Left = 200, Width = 80, Top = 150, DialogResult = DialogResult.OK };
            Button cancelButton = new Button() { Text = "Cancel", Left = 300, Width = 80, Top = 150, DialogResult = DialogResult.Cancel };

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(okButton);
            prompt.Controls.Add(cancelButton);
            prompt.AcceptButton = okButton;
            prompt.CancelButton = cancelButton;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }
    }
}
