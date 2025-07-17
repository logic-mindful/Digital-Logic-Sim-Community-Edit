using System.Linq;
using DLS.Game;
using Seb.Types;
using Seb.Vis;
using Seb.Vis.UI;
using UnityEngine;

namespace DLS.Graphics
{
	public static class CollectionStatsMenu
	{
		const float entrySpacing = 0.5f;
		const float menuWidth = 55;
		const float verticalOffset = 22;

		static readonly Vector2 entrySize = new(menuWidth, DrawSettings.SelectorWheelHeight);
		public static readonly Vector2 settingFieldSize = new(entrySize.x / 3, entrySize.y);

		static string collection;

		// ---- Stats ----
		static readonly string numOfChipsLabel = "Number of chips";

		public static void SetCollection(string collection) => CollectionStatsMenu.collection = collection;
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
				Vector2 numOfChipsLabelRight = MenuHelper.DrawLabelSectionOfLabelInputPair(labelPosCurr, entrySize, numOfChipsLabel, labelCol * 0.75f, true);
				UI.DrawPanel(numOfChipsLabelRight, settingFieldSize, new Color(0.18f, 0.18f, 0.18f), Anchor.CentreRight);
				UI.DrawText(GetCollectionChipsLength().ToString(), theme.FontBold, theme.FontSizeRegular, numOfChipsLabelRight + new Vector2(inputTextPad - settingFieldSize.x, 0), Anchor.TextCentreLeft, Color.white);

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

			return;

		}

		private static int GetCollectionChipsLength() => 
			Project.ActiveProject.description.ChipCollections.First(e => e.Name == collection).Chips.Count;
	}
}