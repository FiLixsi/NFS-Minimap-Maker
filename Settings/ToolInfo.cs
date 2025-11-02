using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace NFS_Minimap_Maker
{
    public static class ToolInfo
    {
        public const string Name = "NFS Minimap Maker";
        public const string Version = "v1.0";
        public const string Authors = "FiLixsi";
        public static string FullTitle => $"{Name} {Version} | By {Authors}";
    }

    public static class ToolInfoInit
    {
        public static void Apply(Form form)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            form.StartPosition = FormStartPosition.CenterScreen;
            form.Text = ToolInfo.FullTitle;
            form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
    }
}
