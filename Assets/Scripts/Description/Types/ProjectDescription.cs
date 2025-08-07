using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DLS.Description
{
    public struct ProjectDescription
    {
        public static readonly List<String> BuiltInSpecialChipNames = new List<string>()
        {
            "IN-1", "IN-4", "IN-8",
            "OUT-1", "OUT-4", "OUT-8",

            "1-4BIT", "1-8BIT", "4-8BIT",
            "4-1BIT", "8-1BIT", "8-4BIT"
        };

        public string ProjectName;
		public string DLSVersion_LastSaved;
		public string DLSVersion_EarliestCompatible;
		public string DLSVersion_LastSavedModdedVersion;
		public DateTime CreationTime;
		public DateTime LastSaveTime;

		// Prefs
		public int Prefs_MainPinNamesDisplayMode;
		public int Prefs_ChipPinNamesDisplayMode;
		public int Prefs_GridDisplayMode;
		public int Prefs_Snapping;
		public int Prefs_StraightWires;
		public bool Prefs_SimPaused;
		public int Prefs_SimTargetStepsPerSecond;
		public int Prefs_SimStepsPerClockTick;
		public int Perfs_PinIndicators;

		// Stats
		public ulong StepsRanSinceCreated;
		public CustomStopwatch /* We should ask Stack Overflow why we cannot access this class from outside its namespace */ TimeSpentSinceCreated;

		// List of all player-created chips (in order of creation -- oldest first)
		public string[] AllCustomChipNames;

		public List<StarredItem> StarredList;
		public List<ChipCollection> ChipCollections;

		// List of all I/O (in order of creation -- oldest first)
		public List<PinBitCount> pinBitCounts;

		// Used both for Merge Chips and Split Chips
		// Dictionnary of  Big Pin and  Small Pin  Ex : (4,1) or (8,4) or (8,1)
		public List<KeyValuePair<PinBitCount, PinBitCount>> SplitMergePairs;

        // ---- Helper functions ----
        public bool IsStarred(string chipName, bool isCollection)
		{
			foreach (StarredItem item in StarredList)
			{
				if (item.IsCollection == isCollection && ChipDescription.NameMatch(chipName, item.Name)) return true;
			}

			return false;
		}

		public void AddChipToCollection(string collectionName, string chipName) {
			if(collectionName == null) throw new ArgumentNullException(collectionName);
			foreach(ChipCollection collection in ChipCollections)
			{
				if(collection.Name.Equals(chipName, StringComparison.OrdinalIgnoreCase))
				{
					collection.Chips.Add(chipName);
				}
			}
			
		}

		public List<String> SplitMergeNames()
		{
			List<String> result = new List<String>();
			foreach(KeyValuePair<PinBitCount, PinBitCount> pair in SplitMergePairs)
			{
				result.Add(pair.Key.ToString()+"-"+pair.Value.ToString()+"BIT");
				result.Add(pair.Value.ToString() + "-" + pair.Key.ToString()+"BIT");
			}
			return result;
		}

		public List<String> InOutNames()
		{
			List<String > result = new List<String>();
			foreach(PinBitCount count in pinBitCounts)
			{
				result.Add("IN-"+count.ToString());
                result.Add("OUT-" + count.ToString());
            }
			return result;
        }

		public List<String> AllSpecialNames()
		{
			List<String> result = new List<string>(SplitMergeNames());
			result.AddRange(InOutNames());

			// Add ulterior special types here

			return result;
		}

		public List<String> AllCustomSpecialNames() {
			List<String> result = new List<string>();
			result.AddRange(AllSpecialNames().Where(name => !BuiltInSpecialChipNames.Contains(name)));
			return result;
		}

        public bool isPlayerAddedSpecialChip(string name)
        {
            return AllCustomSpecialNames().Contains(name);
        }

		public void RemoveSpecial(string name) {
			if(InOutNames().Contains(name))
			{
				RemoveInOut(name);
			}
			if (SplitMergeNames().Contains(name))
			{
				RemoveSplitMerge(name);
			}
		}

		void RemoveInOut(string name) {
			int index = InOutNames().IndexOf(name) / 2;
			pinBitCounts.RemoveAt(index);

		}

		void RemoveSplitMerge(string name) {
			int index = SplitMergeNames().IndexOf(name) / 2;
			SplitMergePairs.RemoveAt(index);
		}

		public List<String> CorrespondingSpecials(string name) {
			List<String> result = new List<string>();
			if(InOutNames().Contains(name))
			{
				result = CorrespondingInOut(name);
			}
			else if (SplitMergeNames().Contains(name))
			{
                result = CorrespondingSplitMerge(name);
			}

			return result;
		}

		public List<String> CorrespondingInOut(string name)
		{
			List<String> result = new List<String>();
			result.Add(name);
			int index = InOutNames().IndexOf(name);
			int indexNext = index % 2 == 0 ? index + 1 : index - 1;
			result.Add(InOutNames()[indexNext]);
			return result;
		}

        public List<String> CorrespondingSplitMerge(string name)
        {
            List<String> result = new List<String>();
            result.Add(name);
            int index = SplitMergeNames().IndexOf(name);
            int indexNext = index % 2 == 0 ? index + 1 : index - 1;
            result.Add(SplitMergeNames()[indexNext]);
            return result;
        }

    }


    public struct StarredItem
	{
		public string Name;
		public bool IsCollection;

		// Cached displayed strings to avoid allocations. (Not serialized, just regenerated on load)
		[JsonIgnore] string bottomBar_collectionDisplayNameOpen;
		[JsonIgnore] string bottomBar_collectionDisplayNameClosed;


		public StarredItem(string name, bool isCollection) : this()
		{
			Name = name;
			IsCollection = isCollection;

			CacheDisplayStrings();
		}

		public void CacheDisplayStrings()
		{
			bottomBar_collectionDisplayNameOpen = "\u25b4<halfSpace>" + Name;
			bottomBar_collectionDisplayNameClosed = "\u25b8<halfSpace>" + Name;
		}

		public string GetDisplayStringForBottomBar(bool open)
		{
			if (IsCollection) return open ? bottomBar_collectionDisplayNameOpen : bottomBar_collectionDisplayNameClosed;
			return Name;
		}
	}

	public class ChipCollection
	{
		public readonly List<string> Chips;
		[JsonIgnore] string displayName_closed;
		[JsonIgnore] string displayName_empty;

		// Cached displayed strings to avoid allocations. (Not serialized, just regenerated on load)
		[JsonIgnore] string displayName_open;
		public bool IsToggledOpen;
		public string Name;

		public ChipCollection(string name, params string[] chips)
		{
			Name = name;
			Chips = new List<string>(chips);
			UpdateDisplayStrings();
		}

		public void UpdateDisplayStrings()
		{
			displayName_open = "\u25bc " + Name;
			displayName_closed = "\u25b6 " + Name;
			displayName_empty = "\u25cc " + Name;
		}

		public string GetDisplayString()
		{
			if (Chips.Count == 0) return displayName_empty;
			return IsToggledOpen ? displayName_open : displayName_closed;
		}
	}
}