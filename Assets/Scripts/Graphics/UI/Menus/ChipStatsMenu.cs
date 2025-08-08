using System.Collections.Generic;
using System.Linq;
using DLS.Description;
using DLS.Game;
using Seb.Types;
using Seb.Vis;
using Seb.Vis.UI;
using UnityEngine;

namespace DLS.Graphics
{
	public static class ChipStatsMenu
	{
		const float entrySpacing = 0.5f;
		const float menuWidth = 55;
		const float verticalOffset = 22;

		static readonly Vector2 entrySize = new(menuWidth, DrawSettings.SelectorWheelHeight);
		public static readonly Vector2 settingFieldSize = new(entrySize.x / 3, entrySize.y);

		static string chip;

		// ---- Stats ----
		static readonly string usesLabel = "Uses";

		static readonly string totalUsesLabel = "Total uses";

		static readonly string usedByLabel = "Used by";

		static readonly string numOfChipsInChipLabel = "Number of chips in this chip";


		public static void SetChip(string chip) {
			ChipStatsMenu.chip = chip;
			isChipBuiltIn = Project.ActiveProject.chipLibrary.IsBuiltinChip(chip);
		}
		static bool isChipBuiltIn;
		public static void DrawMenu()
		{
			DrawSettings.UIThemeDLS theme = DrawSettings.ActiveUITheme;
			MenuHelper.DrawBackgroundOverlay();
			Draw.ID panelID = UI.ReservePanel();

			const int inputTextPad = 1;
			Color labelCol = Color.white;
			Color headerCol = new(0.46f, 1, 0.54f);
			Vector2 topLeft = UI.Centre + new Vector2(-menuWidth / 2, verticalOffset);
			Vector2 labelPosCurr = topLeft;

			using (UI.BeginBoundsScope(true))
			{
				// Draw stats
				Vector2 usesLabelRight = MenuHelper.DrawLabelSectionOfLabelInputPair(labelPosCurr, entrySize, usesLabel, labelCol * 0.75f, true);
				UI.DrawPanel(usesLabelRight, settingFieldSize, new Color(0.18f, 0.18f, 0.18f), Anchor.CentreRight);
				UI.DrawText(GetChipUses().ToString(), theme.FontBold, theme.FontSizeRegular, usesLabelRight + new Vector2(inputTextPad - settingFieldSize.x, 0), Anchor.TextCentreLeft, Color.white);
				AddSpacing();

				Vector2 usedByLabelRight = MenuHelper.DrawLabelSectionOfLabelInputPair(labelPosCurr, entrySize, usedByLabel, labelCol * 0.75f, true);
				UI.DrawPanel(usedByLabelRight, settingFieldSize, new Color(0.18f, 0.18f, 0.18f), Anchor.CentreRight);
				UI.DrawText(GetChipUsedBy().ToString(), theme.FontBold, theme.FontSizeRegular, usedByLabelRight + new Vector2(inputTextPad - settingFieldSize.x, 0), Anchor.TextCentreLeft, Color.white);
				AddSpacing();
				
				Vector2 totalUsesLabelRight = MenuHelper.DrawLabelSectionOfLabelInputPair(labelPosCurr, entrySize, totalUsesLabel, labelCol * 0.75f, true);
				UI.DrawPanel(totalUsesLabelRight, settingFieldSize, new Color(0.18f, 0.18f, 0.18f), Anchor.CentreRight);
				UI.DrawText(GetChipUsesTotal().ToString(), theme.FontBold, theme.FontSizeRegular, totalUsesLabelRight + new Vector2(inputTextPad - settingFieldSize.x, 0), Anchor.TextCentreLeft, Color.white);
				
				if (!isChipBuiltIn) {
					AddSpacing();
					Vector2 numOfChipsInChipLabelRight = MenuHelper.DrawLabelSectionOfLabelInputPair(labelPosCurr, entrySize, numOfChipsInChipLabel, labelCol * 0.75f, true);
					UI.DrawPanel(numOfChipsInChipLabelRight, settingFieldSize, new Color(0.18f, 0.18f, 0.18f), Anchor.CentreRight);
					UI.DrawText(GetNumOfChipsInChip().ToString(), theme.FontBold, theme.FontSizeRegular, numOfChipsInChipLabelRight + new Vector2(inputTextPad - settingFieldSize.x, 0), Anchor.TextCentreLeft, Color.white);
				}

				// Draw close
				Vector2 buttonTopLeft = new(50, UI.PrevBounds.Bottom - 1 * (DrawSettings.DefaultButtonSpacing * 6));
				bool result = UI.Button("CLOSE", MenuHelper.Theme.ButtonTheme, buttonTopLeft, new Vector2(menuWidth / 1.118f, 0));

				// Draw menu background
				Bounds2D menuBounds = UI.GetCurrentBoundsScope();
				MenuHelper.DrawReservedMenuPanel(panelID, menuBounds);

				// Close
				if (result)
					UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
			}

            if (KeyboardShortcuts.CancelShortcutTriggered)
            {
                UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
            }

            return;

			void AddSpacing()
			{
				labelPosCurr.y -= entrySize.y + entrySpacing;
			}
		}

		private static uint GetChipUses() {
			uint uses = 0;
			foreach (ChipDescription chip in Project.ActiveProject.chipLibrary.allChips)
				if (chip.Name != ChipStatsMenu.chip)
					foreach (SubChipDescription subChip in chip.SubChips)
						if (subChip.Name == ChipStatsMenu.chip) uses++;

			return uses;
		}
		private static int GetChipUsesTotal() {
			Dictionary<ChipDescription, int> usesByChip = new();
			foreach (ChipDescription chip in Project.ActiveProject.chipLibrary.allChips)
			{
				usesByChip.Add(chip, 0);
				foreach (SubChipDescription subChip in chip.SubChips)
					if (subChip.Name == ChipStatsMenu.chip) usesByChip[chip]++;
					else if (usesByChip.Any(e => e.Key.Name == subChip.Name)) usesByChip[chip] += usesByChip.First(e => e.Key.Name == subChip.Name).Value;
			}

			return usesByChip.Values.ToArray().Sum();
		}
		private static int GetNumOfChipsInChip() =>
			Project.ActiveProject.chipLibrary.GetChipDescription(chip).SubChips.Length;

		private static int GetChipUsedBy() =>
			Project.ActiveProject.chipLibrary.GetDirectParentChips(chip).Length;
	}
}