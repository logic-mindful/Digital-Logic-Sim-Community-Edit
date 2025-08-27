using System.Linq;
using DLS.Description;

namespace DLS.Game
{
	public static class BuiltinCollectionCreator
	{
		public static StarredItem[] GetDefaultStarredList()
		{
			return new StarredItem[]
			{
				new("IN/OUT", true),
				new(ChipTypeHelper.GetName(ChipType.Nand), false)
			};
		}

		public static ChipCollection[] CreateDefaultChipCollections()
		{
			return new[]
			{
				CreateChipCollection("BASIC",
					ChipType.Nand,
					ChipType.Clock,
					ChipType.Pulse,
					ChipType.Key,
					ChipType.TriStateBuffer,
					ChipType.Constant_8Bit
				),
				CreateChipCollection("IN/OUT",
					ChipType.Button,
					ChipType.Toggle
				).AddNames
				(
					"IN-1",
					"IN-4",
					"IN-8",
					"OUT-1",
					"OUT-4",
					"OUT-8"
				)
				
				,
				CreateByNames("MERGE/SPLIT",
					"1-4BIT",
					"1-8BIT",
					"4-8BIT",
					"4-1BIT",
					"8-4BIT",
					"8-1BIT"
				),
				CreateByNames("BUS",
					"BUS-1",
					"BUS-4",
					"BUS-8"
				),
				CreateChipCollection("DISPLAY",
					ChipType.SevenSegmentDisplay,
					ChipType.DisplayDot,
					ChipType.DisplayRGB,
					ChipType.DisplayRGBTouch,
					ChipType.DisplayLED
				),
				CreateChipCollection("MEMORY",
					ChipType.Rom_256x16,
					ChipType.EEPROM_256x16,
					ChipType.dev_Ram_8Bit
				)
			};
		}

		static ChipCollection CreateChipCollection(string name, params ChipType[] chipTypes)
		{
			return new ChipCollection(name, chipTypes.Select(t => ChipTypeHelper.GetName(t)).ToArray());
		}

		static ChipCollection CreateByNames(string name, params string[] chipNames)
		{
			return new ChipCollection(name, chipNames);
		}

		static ChipCollection AddNames(this ChipCollection chipCollection, params string[] chipNames)
		{
			chipCollection.Chips.AddRange(chipNames);
			return chipCollection;
		}
	}
}