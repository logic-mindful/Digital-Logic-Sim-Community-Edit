using System.Collections.Generic;
using System.Linq;
using DLS.Description;
using DLS.Game;
using UnityEngine;

namespace DLS.SaveSystem
{
	public static class UpgradeHelper
	{

		public static void ApplyVersionChanges(ChipDescription[] customChips, ChipDescription[] builtinChips)
		{
			Main.Version defaultVersion = new(2, 0, 0);
			Main.Version version_2_1_4 = new(2, 1, 4);

			foreach (ChipDescription chipDesc in customChips)
			{
				if (!Main.Version.TryParse(chipDesc.DLSVersion, out Main.Version chipVersion))
				{
					chipVersion = defaultVersion;
				}

				if (chipVersion.ToInt() <= version_2_1_4.ToInt())
				{
					UpdateChipPre_2_1_5(chipDesc);
					chipDesc.DLSVersion = version_2_1_4.ToString();
				}
			}
		}

		public static void ApplyVersionChangesToProject(ref ProjectDescription projectDescription)
		{
			Main.Version defaultModdedVersion = new(1, 0, 0);
			Main.Version moddedVersion_1_1_0 = new(1, 1, 0); // Custom IN and OUTS version
			Main.Version moddedVersion_1_1_1 = new(1, 1, 1); // New 16 and 32 bit pins


			bool canParseModdedVersion = Main.Version.TryParse(projectDescription.DLSVersion_LastSavedModdedVersion, out Main.Version projectVersion);

			bool isVersionEarlierThan_1_1_0 = (!canParseModdedVersion) || projectVersion.ToInt() < moddedVersion_1_1_0.ToInt();
			bool isVersionEarlierThan_1_1_1 = (!canParseModdedVersion) || projectVersion.ToInt() < moddedVersion_1_1_1.ToInt();

			bool isSplitMergeInvalid = projectDescription.SplitMergePairs == null || projectDescription.SplitMergePairs.Count == 0;
			bool isPinBitCountInvalid = projectDescription.pinBitCounts == null || projectDescription.pinBitCounts.Count == 0;

			if (isVersionEarlierThan_1_1_0 | isPinBitCountInvalid)
			{
				projectDescription.DLSVersion_LastSavedModdedVersion = Main.DLSVersion_ModdedID.ToString();
				projectDescription.pinBitCounts = Project.PinBitCounts;
				projectDescription.SplitMergePairs = Project.SplitMergePairs;
			}

			if (isVersionEarlierThan_1_1_0 | isSplitMergeInvalid)
			{
				projectDescription.DLSVersion_LastSavedModdedVersion = Main.DLSVersion_ModdedID.ToString();
				projectDescription.SplitMergePairs = Project.SplitMergePairs;
			}
			
			if (isVersionEarlierThan_1_1_1)
			{
				projectDescription.DLSVersion_LastSavedModdedVersion = Main.DLSVersion_ModdedID.ToString();
				projectDescription.pinBitCounts.Union(Project.PinBitCounts);
				projectDescription.SplitMergePairs.Union(Project.SplitMergePairs);
			}
        }

        static void UpdateChipPre_2_1_5(ChipDescription chipDesc)
		{
			string ledName = ChipTypeHelper.GetName(ChipType.DisplayLED);

			// Update input pin cols
			for (int i = 0; i < chipDesc.InputPins.Length; i++)
			{
				chipDesc.InputPins[i].Colour = GetNewPinColour(chipDesc.InputPins[i].Colour);
			}

			// ---- Added LED colour option (requires instance data array size of 1 for led subchips) ----
			for (int i = 0; i < chipDesc.SubChips.Length; i++)
			{
				SubChipDescription subChipDesc = chipDesc.SubChips[i];

				// Ensure LED type has colour data
				if (ChipDescription.NameMatch(subChipDesc.Name, ledName) && (subChipDesc.InternalData == null || subChipDesc.InternalData.Length == 0))
				{
					chipDesc.SubChips[i].InternalData = DescriptionCreator.CreateDefaultInstanceData(ChipType.DisplayLED);
				}

				// Update subchip output pin cols
				if (subChipDesc.OutputPinColourInfo == null) continue;
				for (int j = 0; j < subChipDesc.OutputPinColourInfo.Length; j++)
				{
					subChipDesc.OutputPinColourInfo[j].PinColour = GetNewPinColour(subChipDesc.OutputPinColourInfo[j].PinColour);
				}
			}

			// ---- Inserted ORANGE as colour option at index 1, so update old indices to correct values ----
			static PinColour GetNewPinColour(PinColour colOld)
			{
				int colourIndex = (int)colOld;
				if (colourIndex > 0) colourIndex++;

				return (PinColour)colourIndex;
			}
		}
	}
}