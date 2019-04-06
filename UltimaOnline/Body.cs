using System;
using System.IO;

namespace UltimaOnline
{
    public enum BodyType : byte
    {
        Empty,
        Monster,
        Sea,
        Animal,
        Human,
        Equipment
    }

    public struct Body
    {
        static BodyType[] _types;

        static Body()
        {
            if (File.Exists("Data/bodyTable.cfg"))
                using (var sr = new StreamReader("Data/bodyTable.cfg"))
                {
                    _types = new BodyType[0x1000];
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Length == 0 || line.StartsWith("#"))
                            continue;
                        var split = line.Split('\t');
                        if (int.TryParse(split[0], out int bodyID) && Enum.TryParse(split[1], true, out BodyType type) && bodyID >= 0 && bodyID < _types.Length)
                            _types[bodyID] = type;
                        else
                        {
                            Console.WriteLine("Warning: Invalid bodyTable entry:");
                            Console.WriteLine(line);
                        }
                    }
                }
            else
            {
                Console.WriteLine("Warning: Data/bodyTable.cfg does not exist");
                _types = new BodyType[0];
            }
        }
        public Body(int bodyID) => BodyID = bodyID;

        public BodyType Type => BodyID >= 0 && BodyID < _types.Length ? _types[BodyID] : BodyType.Empty;

        public bool IsHuman
            => BodyID >= 0
            && BodyID < _types.Length
            && _types[BodyID] == BodyType.Human
            && BodyID != 402
            && BodyID != 403
            && BodyID != 607
            && BodyID != 608
            && BodyID != 694
            && BodyID != 695
            && BodyID != 970;

        public bool IsGargoyle
            => BodyID == 666
            || BodyID == 667
            || BodyID == 694
            || BodyID == 695;

        public bool IsMale
            => BodyID == 183
            || BodyID == 185
            || BodyID == 400
            || BodyID == 402
            || BodyID == 605
            || BodyID == 607
            || BodyID == 666
            || BodyID == 694
            || BodyID == 750;

        public bool IsFemale
            => BodyID == 184
            || BodyID == 186
            || BodyID == 401
            || BodyID == 403
            || BodyID == 606
            || BodyID == 608
            || BodyID == 667
            || BodyID == 695
            || BodyID == 751;

        public bool IsGhost
            => BodyID == 402
            || BodyID == 403
            || BodyID == 607
            || BodyID == 608
            || BodyID == 694
            || BodyID == 695
            || BodyID == 970;

        public bool IsMonster
            => BodyID >= 0
            && BodyID < _types.Length
            && _types[BodyID] == BodyType.Monster;

        public bool IsAnimal
            => BodyID >= 0
            && BodyID < _types.Length
            && _types[BodyID] == BodyType.Animal;

        public bool IsEmpty
            => BodyID >= 0
            && BodyID < _types.Length
            && _types[BodyID] == BodyType.Empty;

        public bool IsSea
            => BodyID >= 0
            && BodyID < _types.Length
            && _types[BodyID] == BodyType.Sea;

        public bool IsEquipment
            => BodyID >= 0
            && BodyID < _types.Length
            && _types[BodyID] == BodyType.Equipment;

        public int BodyID { get; }

        public static implicit operator int(Body a) => a.BodyID;
        public static implicit operator Body(int a) => new Body(a);
        public override string ToString() => $"0x{BodyID:X}";
        public override int GetHashCode() => BodyID;
        public override bool Equals(object o) => o == null || !(o is Body) ? false : ((Body)o).BodyID == BodyID;
        public static bool operator ==(Body l, Body r) => l.BodyID == r.BodyID;
        public static bool operator !=(Body l, Body r) => l.BodyID != r.BodyID;
        public static bool operator >(Body l, Body r) => l.BodyID > r.BodyID;
        public static bool operator >=(Body l, Body r) => l.BodyID >= r.BodyID;
        public static bool operator <(Body l, Body r) => l.BodyID < r.BodyID;
        public static bool operator <=(Body l, Body r) => l.BodyID <= r.BodyID;
    }
}