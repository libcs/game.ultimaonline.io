using System;
using System.Collections.Generic;

namespace UltimaOnline
{
    [Parsable]
    public abstract class Race
    {
        static string[] _RaceNames;
        static Race[] _RaceValues;

        protected Race(int raceID, int raceIndex, string name, string pluralName, int maleBody, int femaleBody, int maleGhostBody, int femaleGhostBody, Expansion requiredExpansion)
        {
            RaceID = raceID;
            RaceIndex = raceIndex;
            Name = name;
            MaleBody = maleBody;
            FemaleBody = femaleBody;
            MaleGhostBody = maleGhostBody;
            FemaleGhostBody = femaleGhostBody;
            RequiredExpansion = requiredExpansion;
            PluralName = pluralName;
        }

        public static Race DefaultRace => Races[0];

        public static Race[] Races { get; } = new Race[0x100];

        public static Race Human => Races[0];
        public static Race Elf => Races[1];
        public static Race Gargoyle => Races[2];

        public static List<Race> AllRaces { get; } = new List<Race>();

        public static string[] GetRaceNames()
        {
            CheckNamesAndValues();
            return _RaceNames;
        }

        public static Race[] GetRaceValues()
        {
            CheckNamesAndValues();
            return _RaceValues;
        }

        public static Race Parse(string value)
        {
            CheckNamesAndValues();
            for (var i = 0; i < _RaceNames.Length; ++i)
                if (Insensitive.Equals(_RaceNames[i], value))
                    return _RaceValues[i];
            if (int.TryParse(value, out int index) && index >= 0 && index < Races.Length && Races[index] != null)
                return Races[index];
            throw new ArgumentException("Invalid race name");
        }

        static void CheckNamesAndValues()
        {
            if (_RaceNames != null && _RaceNames.Length == AllRaces.Count)
                return;
            _RaceNames = new string[AllRaces.Count];
            _RaceValues = new Race[AllRaces.Count];
            for (var i = 0; i < AllRaces.Count; ++i)
            {
                var race = AllRaces[i];
                _RaceNames[i] = race.Name;
                _RaceValues[i] = race;
            }
        }

        public override string ToString() => Name;

        public Expansion RequiredExpansion { get; }

        public int MaleBody { get; }
        public int MaleGhostBody { get; }

        public int FemaleBody { get; }
        public int FemaleGhostBody { get; }

        public virtual bool ValidateHair(Mobile m, int itemID) => ValidateHair(m.Female, itemID);
        public abstract bool ValidateHair(bool female, int itemID);

        public virtual int RandomHair(Mobile m) => RandomHair(m.Female);
        public abstract int RandomHair(bool female);

        public virtual bool ValidateFacialHair(Mobile m, int itemID) => ValidateFacialHair(m.Female, itemID);
        public abstract bool ValidateFacialHair(bool female, int itemID);

        public virtual int RandomFacialHair(Mobile m) => RandomFacialHair(m.Female);
        public abstract int RandomFacialHair(bool female);  //For the *ahem* bearded ladies

        public abstract int ClipSkinHue(int hue);
        public abstract int RandomSkinHue();

        public abstract int ClipHairHue(int hue);
        public abstract int RandomHairHue();

        public virtual int Body(Mobile m) => m.Alive ? AliveBody(m.Female) : GhostBody(m.Female);

        public virtual int AliveBody(Mobile m) => AliveBody(m.Female);
        public virtual int AliveBody(bool female) => female ? FemaleBody : MaleBody;

        public virtual int GhostBody(Mobile m) => GhostBody(m.Female);
        public virtual int GhostBody(bool female) => female ? FemaleGhostBody : MaleGhostBody;

        public int RaceID { get; }
        public int RaceIndex { get; }
        public string Name { get; set; }
        public string PluralName { get; set; }
    }
}