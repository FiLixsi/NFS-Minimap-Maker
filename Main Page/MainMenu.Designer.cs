namespace NFS_Minimap_Maker
{
    partial class MainMenu
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.Button selectFileButton;
        private System.Windows.Forms.Button buildMinimapButton;
        private System.Windows.Forms.Panel buttonPanel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainMenu));
            pictureBox = new PictureBox();
            buttonPanel = new Panel();
            selectFileButton = new Button();
            buildMinimapButton = new Button();
            ((System.ComponentModel.ISupportInitialize)pictureBox).BeginInit();
            buttonPanel.SuspendLayout();
            SuspendLayout();
            // 
            // pictureBox
            // 
            pictureBox.BorderStyle = BorderStyle.Fixed3D;
            pictureBox.Location = new Point(150, 12);
            pictureBox.Name = "pictureBox";
            pictureBox.Size = new Size(400, 400);
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.TabIndex = 0;
            pictureBox.TabStop = false;
            pictureBox.Click += pictureBox_Click;
            // 
            // buttonPanel
            // 
            buttonPanel.Controls.Add(selectFileButton);
            buttonPanel.Controls.Add(buildMinimapButton);
            buttonPanel.Location = new Point(0, 434);
            buttonPanel.Name = "buttonPanel";
            buttonPanel.Size = new Size(702, 51);
            buttonPanel.TabIndex = 1;
            // 
            // selectFileButton
            // 
            selectFileButton.Location = new Point(185, 12);
            selectFileButton.Name = "selectFileButton";
            selectFileButton.Size = new Size(130, 30);
            selectFileButton.TabIndex = 0;
            selectFileButton.Text = "Select File";
            selectFileButton.UseVisualStyleBackColor = true;
            selectFileButton.Click += selectFileButton_Click;
            // 
            // buildMinimapButton
            // 
            buildMinimapButton.Enabled = false;
            buildMinimapButton.Location = new Point(370, 12);
            buildMinimapButton.Name = "buildMinimapButton";
            buildMinimapButton.Size = new Size(130, 30);
            buildMinimapButton.TabIndex = 1;
            buildMinimapButton.Text = "Build Minimap";
            buildMinimapButton.UseVisualStyleBackColor = true;
            buildMinimapButton.Click += buildMinimapButton_Click;
            // 
            // MainMenu
            // 
            ClientSize = new Size(702, 487);
            Controls.Add(pictureBox);
            Controls.Add(buttonPanel);
            Name = "MainMenu";
            ((System.ComponentModel.ISupportInitialize)pictureBox).EndInit();
            buttonPanel.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
