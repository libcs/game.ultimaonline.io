using System;
using System.IO;

namespace UltimaOnline
{
    public static class ItemBounds
    {
        static ItemBounds()
        {
            Table = new Rectangle2D[TileData.ItemTable.Length];
            if (File.Exists("Data/Binary/Bounds.bin"))
                using (var fs = new FileStream("Data/Binary/Bounds.bin", FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var br = new BinaryReader(fs))
                {
                    var count = Math.Min(Table.Length, (int)(fs.Length / 8));
                    for (var i = 0; i < count; ++i)
                    {
                        var xMin = br.ReadInt16();
                        var yMin = br.ReadInt16();
                        var xMax = br.ReadInt16();
                        var yMax = br.ReadInt16();
                        Table[i].Set(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
                    }
                }
            else Console.WriteLine("Warning: Data/Binary/Bounds.bin does not exist");
        }

        public static Rectangle2D[] Table { get; private set; }
    }
}