using CoreExtensions.IO;
using Nikki.Reflection.Enum;
using Nikki.Support.MostWanted.Class;
using Nikki.Support.MostWanted.Framework;
using Nikki.Support.Shared.Parts.TPKParts;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;

namespace NFS_Minimap_Maker
{
    public static class Binary
    {
        private static void SetPrivateFieldValue(object instance, string fieldName, object value)
        {
            if (instance == null || string.IsNullOrWhiteSpace(fieldName))
                return;

            var type = instance.GetType();
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            var property = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);

            try
            {
                Type targetType = field != null ? field.FieldType : property?.PropertyType;

                if (targetType == null)
                    throw new MissingFieldException($"Field or property '{fieldName}' not found in class {type.Name}.");

                object convertedValue = value;

                if (targetType.IsEnum)
                {
                    if (value is string s)
                        convertedValue = Enum.Parse(targetType, s, ignoreCase: true);
                    else
                        convertedValue = Enum.ToObject(targetType, value);
                }
                else if (targetType != value?.GetType())
                {
                    convertedValue = Convert.ChangeType(value, targetType);
                }

                if (field != null)
                    field.SetValue(instance, convertedValue);
                else if (property != null && property.CanWrite)
                    property.SetValue(instance, convertedValue);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set '{fieldName}' on {instance.GetType().Name}: {ex.Message}");
            }
        }

        public static async Task StartAsync(string tempDir, string miniMapName)
        {
            try
            {
                var manager = new TPKBlockManager(null);
                var tpkBlocks = new TPKBlock[64];

                for (int i = 0; i < 64; i++)
                {
                    string cname;

                    if (i == 0)
                        cname = "TEMP";
                    else if (i == 1)
                        cname = miniMapName;
                    else
                        cname = $"TPK{i}";

                    var block = new TPKBlock(cname, manager);
                    manager.Add(block);
                    tpkBlocks[i] = block;
                }

                foreach (var tpk in tpkBlocks)
                {
                    SetPrivateFieldValue(tpk, "CompressionType", "CompressedMiniMap");
                }

                var ddsFiles = Directory.GetFiles(tempDir, "*.dds", SearchOption.TopDirectoryOnly)
                                        .OrderBy(x => x)
                                        .ToArray();

                if (ddsFiles.Length == 0)
                    throw new FileNotFoundException($"DDS files not found in {tempDir}");

                for (int i = 0; i < ddsFiles.Length && i < 64; i++)
                {
                    string ddsPath = ddsFiles[i];
                    string texName = $"{miniMapName}_CHOP{i}";
                    var tpk = tpkBlocks[Math.Min(i, tpkBlocks.Length - 1)];

                    var texture = new Texture(texName, ddsPath, tpk);

                    SetPrivateFieldValue(texture, "RenderingOrder", (byte)0);
                    SetPrivateFieldValue(texture, "CompressionValue3", 2);

                    tpk.Textures.Add(texture);

                    var texturePage = new TexturePage()
                    {
                        TextureName = texName,
                        U0 = 0,
                        V0 = 0,
                        U1 = 1,
                        V1 = 1,
                        Flags = 0
                    };
                    tpk.TexturePages.Add(texturePage);
                }

                string binOutput = Path.Combine(tempDir, "MINIMAP.BIN");

                using (var fs = new FileStream(binOutput, FileMode.Create, FileAccess.Write))
                using (var bw = new BinaryWriter(fs))
                {
                    manager.Assemble(bw, "MINIMAP");
                }
            }
            catch
            {
                throw;
            }
        }
    }
}
