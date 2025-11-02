using NFS_Minimap_Maker.Properties;
using Pfim;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NFS_Minimap_Maker
{
    public partial class MainMenu : Form
    {
        private string loadedFilePath;
        internal Bitmap originalImage, displayImage;
        internal ProgressBar progressBar;
        internal Label progressLabel;
        internal readonly string tempDir;

        private System.Windows.Forms.Timer progressTextTimer;
        private int progressTextDotCount = 1;

        public MainMenu()
        {
            new ToolOptions(this);
            ToolInfoInit.Apply(this);

            InitializeComponent();

            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;

            buildMinimapButton.Enabled = false;
            buildMinimapButton.BackColor = SystemColors.ControlLight;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            tempDir = Path.Combine(localAppData, "FiLixsi", "NFS-Minimap-Maker", "Temp");
            TryRecreateDirectory(tempDir);
        }

        private async Task FadeOutPictureAsync()
        {
            if (pictureBox.Image == null)
                return;

            Bitmap bmp = new Bitmap(pictureBox.Image);
            float opacity = 1.0f;
            int steps = 10;
            int delay = 20;

            for (int i = 0; i < steps; i++)
            {
                opacity -= 1f / steps;

                using (Bitmap faded = new Bitmap(bmp.Width, bmp.Height))
                using (Graphics g = Graphics.FromImage(faded))
                {
                    var cm = new System.Drawing.Imaging.ColorMatrix
                    {
                        Matrix33 = opacity
                    };
                    var ia = new System.Drawing.Imaging.ImageAttributes();
                    ia.SetColorMatrix(cm, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
                    g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
                                0, 0, bmp.Width, bmp.Height,
                                GraphicsUnit.Pixel, ia);
                    pictureBox.Image = new Bitmap(faded);
                }

                await Task.Delay(delay);
            }

            bmp.Dispose();
            pictureBox.Image = null;
        }

        private static void TryRecreateDirectory(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            Directory.CreateDirectory(dir);
        }

        private void SetButtonsEnabled(bool enabled)
        {
            foreach (var btn in new[] { selectFileButton, buildMinimapButton })
                btn.Enabled = enabled;
        }

        private void selectFileButton_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select an Image File",
                Filter = "Image Files|*.png;*.jpg;*.dds"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            loadedFilePath = dialog.FileName;

            try
            {
                string ext = Path.GetExtension(loadedFilePath).ToLower();
                originalImage = ext == ".dds"
                    ? (Bitmap)LoadDDS(loadedFilePath)
                    : new Bitmap(Image.FromFile(loadedFilePath));

                if (!IsValidResolution(originalImage.Width) || !IsValidResolution(originalImage.Height))
                {
                    originalImage.Dispose();
                    originalImage = null;
                    MessageBox.Show("The image size exceeds the allowed limits. Please use files with 1024, 2048, 4096, or 8192.",
                        "Invalid Image Size", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    buildMinimapButton.Enabled = false;
                    return;
                }

                pictureBox.Image = originalImage;
                buildMinimapButton.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading image: " + ex.Message);
                buildMinimapButton.Enabled = false;
            }
        }

        private static bool IsValidResolution(int value)
        {
            return value == 1024 || value == 2048 || value == 4096 || value == 8192;
        }

        private Image LoadDDS(string filePath)
        {
            using var image = Pfim.Pfim.FromFile(filePath);
            var format = image.Format switch
            {
                Pfim.ImageFormat.Rgba32 => PixelFormat.Format32bppArgb,
                Pfim.ImageFormat.Rgb24 => PixelFormat.Format24bppRgb,
                _ => throw new NotSupportedException($"Unsupported DDS format: {image.Format}")
            };

            var handle = System.Runtime.InteropServices.GCHandle.Alloc(image.Data, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                using var bmp = new Bitmap(image.Width, image.Height, image.Stride, format, handle.AddrOfPinnedObject());
                return new Bitmap(bmp);
            }
            finally { handle.Free(); }
        }

        internal void EnsureProgressUi()
        {
            if (progressBar == null)
            {
                progressBar = new ProgressBar
                {
                    Width = pictureBox.Width - 40,
                    Height = 22,
                    Left = pictureBox.Left + 20,
                    Top = pictureBox.Bottom + 10,
                    Minimum = 0,
                    Maximum = 100,
                    Style = ProgressBarStyle.Continuous
                };
                Controls.Add(progressBar);
            }

            if (progressLabel == null)
            {
                progressLabel = new Label
                {
                    AutoSize = false,
                    Width = progressBar.Width,
                    Height = progressBar.Height,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Left = progressBar.Left,
                    Top = progressBar.Top - 11,
                    Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
                    BackColor = Color.Transparent,
                    ForeColor = ToolOptions.IsDarkTheme ? Color.White : Color.Black
                };
                Controls.Add(progressLabel);
                progressLabel.BringToFront();
            }

            ToggleProgressUi(true);
        }

        internal void ToggleProgressUi(bool visible, string text = "")
        {
            progressBar.Visible = progressLabel.Visible = visible;

            if (visible)
            {
                progressLabel.Text = text;
                progressLabel.BringToFront();
            }
            else
            {
                progressLabel.Text = "";
            }
        }

        internal bool TryExtractTexconvFromResources(string targetPath)
        {
            try
            {
                var prop = typeof(Properties.Resources)
                    .GetProperty("texconv", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var val = prop?.GetValue(null);
                if (val is byte[] bytes)
                    File.WriteAllBytes(targetPath, bytes);
                else if (val is UnmanagedMemoryStream ms)
                    using (ms)
                    using (var fs = File.Create(targetPath))
                        ms.CopyTo(fs);
                else
                    return false;

                File.SetAttributes(targetPath, FileAttributes.Normal);
                return true;
            }
            catch { return false; }
        }

        private void pictureBox_Click(object sender, EventArgs e) { }
    }
}
