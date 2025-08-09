using DLS.Simulation;
using Seb.Helpers;
using Seb.Vis;
using Seb.Vis.UI;
using UnityEngine;

namespace DLS.Graphics
{
	public static class CreateCacheUI
	{
		public static void DrawCreatingCacheInfo()
		{
			string chipName = SimChip.nameOfChipWhoseCacheIsBeingCreated;
			int percentage = (int)(SimChip.cacheCreatingProgress * 100);
			string text = $"Creating Cache ({percentage}%): {chipName}";
			Vector2 textSize = UI.CalculateTextSize(text, UIThemeLibrary.FontSizeDefault, UIThemeLibrary.DefaultFont);
			UI.TextWithBackground(new Vector2(BottomBarUI.buttonSpacing, BottomBarUI.barHeight + BottomBarUI.buttonSpacing), new Vector2(textSize.x + 1, textSize.y + 1), Anchor.BottomLeft, text, UIThemeLibrary.DefaultFont, UIThemeLibrary.FontSizeDefault, Color.yellow, ColHelper.MakeCol255(40));
		}
	}
}