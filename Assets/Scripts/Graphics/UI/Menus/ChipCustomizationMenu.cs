using System;
using System.Collections.Generic;
using System.Linq;
using DLS.Description;
using DLS.Game;
using Seb.Helpers;
using Seb.Vis;
using Seb.Vis.UI;
using UnityEngine;

namespace DLS.Graphics
{
	public static class ChipCustomizationMenu
	{
		static readonly string[] nameDisplayOptions =
		{
			"Name: Middle",
			"Name: Top",
			"Name: Hidden"
		};
        static readonly string[] layoutOptions =
        {
            "Layout: Default",
            "Layout: Custom"
        };


        // ---- State ----
        static SubChipInstance[] subChipsWithDisplays;
		static string displayLabelString;
		static string colHexCodeString;

		static readonly UIHandle ID_DisplaysScrollView = new("CustomizeMenu_DisplaysScroll");
		static readonly UIHandle ID_ColourPicker = new("CustomizeMenu_ChipCol");
		static readonly UIHandle ID_ColourHexInput = new("CustomizeMenu_ChipColHexInput");
		static readonly UIHandle ID_NameDisplayOptions = new("CustomizeMenu_NameDisplayOptions");
        static readonly UIHandle ID_LayoutOptions = new("CustomizeMenu_LayoutOptions");
        static readonly UI.ScrollViewDrawElementFunc drawDisplayScrollEntry = DrawDisplayScroll;
		static readonly Func<string, bool> hexStringInputValidator = ValidateHexStringInput;
		public static bool isCustomLayout;
		public static bool isDraggingPin;
		static float pinDragStartY;
        static float pinDragMouseStartY;
        public static bool isPinPositionValid;
		static float lastValidOffset;
        static int lastValidFace; 
        static readonly float minPinSpacing = 0.025f;
        public static PinInstance selectedPin;
        public static void OnMenuOpened()
		{
			DevChipInstance chip = Project.ActiveProject.ViewedChip;
			subChipsWithDisplays = chip.GetSubchips().Where(c => c.Description.HasDisplay()).OrderBy(c => c.Position.x).ThenBy(c => c.Position.y).ToArray();
			CustomizationSceneDrawer.OnCustomizationMenuOpened();
			displayLabelString = $"DISPLAYS ({subChipsWithDisplays.Length}):";
            isCustomLayout = false;
            isDraggingPin = false;
            selectedPin = null;

            InitUIFromChipDescription();
		}

		public static void DrawMenu()
		{
			// Don't draw menu when placing display
			if (CustomizationSceneDrawer.IsPlacingDisplay) return;

			const float width = 20;
			const float pad = UILayoutHelper.DefaultSpacing;
			const float pw = width - pad * 2;

			DrawSettings.UIThemeDLS theme = DrawSettings.ActiveUITheme;
			UI.DrawPanel(UI.TopLeft, new Vector2(width, UI.Height), theme.MenuPanelCol, Anchor.TopLeft);
			HandlePinDragging();
            // ---- Cancel/confirm buttons ----
            int cancelConfirmButtonIndex = MenuHelper.DrawButtonPair("CANCEL", "CONFIRM", UI.TopLeft + Vector2.down * pad, pw, false);

			// ---- Chip name UI ----
			int nameDisplayMode = UI.WheelSelector(ID_NameDisplayOptions, nameDisplayOptions, NextPos(), new Vector2(pw, DrawSettings.ButtonHeight), theme.OptionsWheel, Anchor.TopLeft);
			ChipSaveMenu.ActiveCustomizeDescription.NameLocation = (NameDisplayLocation)nameDisplayMode;
            // ---- Chip layout UI ----
            int layoutMode = UI.WheelSelector(ID_LayoutOptions, layoutOptions, NextPos(), new Vector2(pw, DrawSettings.ButtonHeight), theme.OptionsWheel, Anchor.TopLeft);
            if (layoutMode == 0 && isCustomLayout)
            {
                // Switch to default layout
                isCustomLayout = false;
                ChipSaveMenu.ActiveCustomizeChip.SetCustomLayout(false);
                // Reset pins on the preview instance
                foreach (PinInstance pin in ChipSaveMenu.ActiveCustomizeChip.InputPins)
                {
                    pin.face = 3;
                    pin.LocalPosY = 0;
                }
                foreach (PinInstance pin in ChipSaveMenu.ActiveCustomizeChip.OutputPins)
                {
                    pin.face = 1;
                    pin.LocalPosY = 0;
                    //Reset layout
                    
                }
                ChipSaveMenu.ActiveCustomizeChip.updateMinSize();
                if (ChipSaveMenu.ActiveCustomizeChip.MinSize.x > ChipSaveMenu.ActiveCustomizeChip.Description.Size.x)
                {
                    ChipSaveMenu.ActiveCustomizeChip.Description.Size.x = ChipSaveMenu.ActiveCustomizeChip.MinSize.x;
                }
                if (ChipSaveMenu.ActiveCustomizeChip.MinSize.y > ChipSaveMenu.ActiveCustomizeChip.Description.Size.y)
                {
                    ChipSaveMenu.ActiveCustomizeChip.Description.Size.y = ChipSaveMenu.ActiveCustomizeChip.MinSize.y;
                }
                ChipSaveMenu.ActiveCustomizeChip.UpdatePinLayout();
            }
            else if (layoutMode == 1 && !isCustomLayout)
            {
                // Switch to custom layout
                isCustomLayout = true;
                ChipSaveMenu.ActiveCustomizeChip.SetCustomLayout(true);
            }

            // ---- Chip colour UI ----
            Color newCol = UI.DrawColourPicker(ID_ColourPicker, NextPos(), pw, Anchor.TopLeft);
			InputFieldTheme inputTheme = MenuHelper.Theme.ChipNameInputField;
			inputTheme.fontSize = MenuHelper.Theme.FontSizeRegular;

			InputFieldState hexColInput = UI.InputField(ID_ColourHexInput, inputTheme, NextPos(), new Vector2(pw, DrawSettings.ButtonHeight), "#", Anchor.TopLeft, 1, hexStringInputValidator);

			if (newCol != ChipSaveMenu.ActiveCustomizeDescription.Colour)
			{
				ChipSaveMenu.ActiveCustomizeDescription.Colour = newCol;
				UpdateChipColHexStringFromColour(newCol);
			}
			else if (colHexCodeString != hexColInput.text)
			{
				UpdateChipColFromHexString(hexColInput.text);
			}

			// ---- Displays UI ----
			Color labelCol = ColHelper.Darken(theme.MenuPanelCol, 0.01f);
			Vector2 labelPos = NextPos(1);
			UI.TextWithBackground(labelPos, new Vector2(pw, DrawSettings.ButtonHeight), Anchor.TopLeft, displayLabelString, theme.FontBold, theme.FontSizeRegular, Color.white, labelCol);

			float scrollViewHeight = 20;
			float scrollViewSpacing = UILayoutHelper.DefaultSpacing;
			UI.DrawScrollView(ID_DisplaysScrollView, NextPos(), new Vector2(pw, scrollViewHeight), scrollViewSpacing, Anchor.TopLeft, theme.ScrollTheme, drawDisplayScrollEntry, subChipsWithDisplays.Length);

			Vector2 NextPos(float extraPadding = 0)
			{
				return UI.PrevBounds.BottomLeft + Vector2.down * (pad + extraPadding);
			}

			// Cancel
			if (cancelConfirmButtonIndex == 0)
			{
				RevertChanges();
				UIDrawer.SetActiveMenu(UIDrawer.MenuType.ChipSave);
			}
			// Confirm
			else if (cancelConfirmButtonIndex == 1)
			{
				UpdateCustomizeDescription();
				UIDrawer.SetActiveMenu(UIDrawer.MenuType.ChipSave);
			}
		}

		static void DrawDisplayScroll(Vector2 pos, float width, int i, bool isLayoutPass)
		{
			DrawSettings.UIThemeDLS theme = DrawSettings.ActiveUITheme;

			SubChipInstance subChip = subChipsWithDisplays[i];
			ChipDescription chipDesc = subChip.Description;
			string label = subChip.Label;
			string displayName = string.IsNullOrWhiteSpace(label) ? chipDesc.Name : label;

			// Don't allow adding same display multiple times
			bool enabled = CustomizationSceneDrawer.SelectedDisplay == null || subChip.ID != CustomizationSceneDrawer.SelectedDisplay.Desc.SubChipID; // display is removed from list when selected, so check manually here
			foreach (DisplayInstance d in ChipSaveMenu.ActiveCustomizeChip.Displays)
			{
				if (d.Desc.SubChipID == subChip.ID)
				{
					enabled = false;
					break;
				}
			}

			// Display selected, start placement
			if (UI.Button(displayName, theme.ButtonTheme, pos, new Vector2(width, 0), enabled, false, true, Anchor.TopLeft))
			{
				SubChipDescription subChipDesc = new(chipDesc.Name, subChipsWithDisplays[i].ID, string.Empty, Vector2.zero, null);
				SubChipInstance instance = new(chipDesc, subChipDesc);
				CustomizationSceneDrawer.StartPlacingDisplay(instance);
			}
		}

		static void RevertChanges()
		{
			ChipSaveMenu.RevertCustomizationStateToBeforeEnteringCustomizeMenu();
			InitUIFromChipDescription();
		}

		static void InitUIFromChipDescription()
		{
			// Init col picker to chip colour
			ColourPickerState chipColourPickerState = UI.GetColourPickerState(ID_ColourPicker);
			Color.RGBToHSV(ChipSaveMenu.ActiveCustomizeDescription.Colour, out chipColourPickerState.hue, out chipColourPickerState.sat, out chipColourPickerState.val);
			UpdateChipColHexStringFromColour(chipColourPickerState.GetRGB());

			// Init name display mode
			WheelSelectorState nameDisplayWheelState = UI.GetWheelSelectorState(ID_NameDisplayOptions);
			nameDisplayWheelState.index = (int)ChipSaveMenu.ActiveCustomizeDescription.NameLocation;

            // Init layout mode by checking if any pins have custom positions
            isCustomLayout = Project.ActiveProject.ViewedChip.HasCustomLayout;

            WheelSelectorState layoutWheelState = UI.GetWheelSelectorState(ID_LayoutOptions);
            layoutWheelState.index = isCustomLayout ? 1 : 0;
        }

		static void UpdateCustomizeDescription()
		{
			List<DisplayInstance> displays = ChipSaveMenu.ActiveCustomizeChip.Displays;
			ChipSaveMenu.ActiveCustomizeDescription.Displays = displays.Select(s => s.Desc).ToArray();
			ChipSaveMenu.ActiveCustomizeDescription.HasCustomLayout = isCustomLayout;

            //Saves pin offset and faces
            for (int i = 0; i < ChipSaveMenu.ActiveCustomizeChip.Description.InputPins.Length; i++)
				{
                ChipSaveMenu.ActiveCustomizeChip.Description.InputPins[i].LocalOffset = ChipSaveMenu.ActiveCustomizeChip.InputPins[i].LocalPosY;
                ChipSaveMenu.ActiveCustomizeChip.Description.InputPins[i].face = ChipSaveMenu.ActiveCustomizeChip.InputPins[i].face;
            }
            for (int i = 0; i < ChipSaveMenu.ActiveCustomizeChip.Description.OutputPins.Length; i++)
            {
                ChipSaveMenu.ActiveCustomizeChip.Description.OutputPins[i].LocalOffset = ChipSaveMenu.ActiveCustomizeChip.OutputPins[i].LocalPosY;
                ChipSaveMenu.ActiveCustomizeChip.Description.OutputPins[i].face = ChipSaveMenu.ActiveCustomizeChip.OutputPins[i].face;
                
            }
        }

		static void UpdateChipColHexStringFromColour(Color col)
		{
			int colInt = (byte)(col.r * 255) << 16 | (byte)(col.g * 255) << 8 | (byte)(col.b * 255);
			colHexCodeString = "#" + $"{colInt:X6}";
			UI.GetInputFieldState(ID_ColourHexInput).SetText(colHexCodeString, false);
		}

		static void UpdateChipColFromHexString(string hexString)
		{
			colHexCodeString = hexString;
			hexString = hexString.Replace("#", "");
			hexString = hexString.PadRight(6, '0');

			if (ColHelper.TryParseHexCode(hexString, out Color col))
			{
				UI.GetColourPickerState(ID_ColourPicker).SetRGB(col);
				ChipSaveMenu.ActiveCustomizeDescription.Colour = col;
			}
		}

		static bool ValidateHexStringInput(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return true;

			int numHexDigits = 0;

			for (int i = 0; i < text.Length; i++)
			{
				if (i == 0 && text[i] == '#') continue;

				if (Uri.IsHexDigit(text[i]))
				{
					numHexDigits++;
				}
				else return false;
			}

			return numHexDigits <= 6;
		}

        static void HandlePinDragging()
        {
            if (!InteractionState.MouseIsOverUI)
            {
                // Start dragging a pin
                if (InputHelper.IsMouseDownThisFrame(MouseButton.Left))
                {
                    if (InteractionState.ElementUnderMouse is PinInstance pin)
                    {
                        selectedPin = pin;
                        isDraggingPin = true;
                        lastValidOffset = pin.LocalPosY;
                        lastValidFace = pin.face;
                    }
                }

                if (isDraggingPin && selectedPin?.parent is SubChipInstance chip)
                {
                    Vector2 mouseWorld = InputHelper.MousePosWorld;
                    Vector2 chipCenter = chip.Position;
                    Vector2 localMouse = mouseWorld - chipCenter;
                    Vector2 chipHalfSize = chip.Size / 2f;

                    // Determine closest edge
                    float distTop = Mathf.Abs(localMouse.y - chipHalfSize.y);
                    float distBottom = Mathf.Abs(localMouse.y + chipHalfSize.y);
                    float distRight = Mathf.Abs(localMouse.x - chipHalfSize.x);
                    float distLeft = Mathf.Abs(localMouse.x + chipHalfSize.x);

                    int closestFace = 0;
                    float minDist = distTop;

                    if (distRight < minDist) { closestFace = 1; minDist = distRight; }
                    if (distBottom < minDist) { closestFace = 2; minDist = distBottom; }
                    if (distLeft < minDist) { closestFace = 3; }

                    selectedPin.face = closestFace;

                    float pinHeight = SubChipInstance.PinHeightFromBitCount(selectedPin.bitCount);

                    float maxOffset;
                    float offsetAlongFace;

                    if (closestFace == 0 || closestFace == 2)
                    {
                        // Horizontal face - move along X axis
                        maxOffset = chipHalfSize.x - pinHeight / 2f;
                        offsetAlongFace = Mathf.Clamp(localMouse.x, -maxOffset, maxOffset);
                    }
                    else
                    {
                        // Vertical face - move along Y axis
                        maxOffset = chipHalfSize.y - pinHeight / 2f;
                        offsetAlongFace = Mathf.Clamp(localMouse.y, -maxOffset, maxOffset);
                    }

                    selectedPin.LocalPosY = offsetAlongFace;

                    PinInstance overlappedPin;
                    isPinPositionValid = !DoesPinOverlap(selectedPin, out overlappedPin);

                    // End drag on mouse release
                    if (InputHelper.IsMouseUpThisFrame(MouseButton.Left))
                    {
                        if (isPinPositionValid)
                        {
                            lastValidOffset = offsetAlongFace;
                            lastValidFace = selectedPin.face;

                            if (!isCustomLayout)
                            {
                                isCustomLayout = true;
                                UI.GetWheelSelectorState(ID_LayoutOptions).index = 1;
                                ChipSaveMenu.ActiveCustomizeChip.SetCustomLayout(true);
                            }
                        }
                        else
                        {
                            selectedPin.LocalPosY = lastValidOffset;
                            selectedPin.face = lastValidFace;
                        }

                        isDraggingPin = false;
                        selectedPin = null;
                        isPinPositionValid = true;
                    }
                }
            }
        }

        public static bool DoesPinOverlap(PinInstance pin, out PinInstance overlappedPin)
        {
            overlappedPin = null;
            if (!(pin.parent is SubChipInstance chip)) return false;

            // Get all pins on the same chip to check pins on the same face as selectedpin
            List<PinInstance> pinsToCheck = new List<PinInstance>();
            pinsToCheck.AddRange(chip.InputPins);
            pinsToCheck.AddRange(chip.OutputPins);

            foreach (PinInstance otherPin in pinsToCheck)
            {
                if (otherPin == pin) continue;

                // Only check pins on the same face
                if (otherPin.face != pin.face) continue;

                float distanceAlongFace = Mathf.Abs(pin.LocalPosY - otherPin.LocalPosY);

                // Calculate minimum required spacing based on pin sizes
                float pinHeight =  SubChipInstance.PinHeightFromBitCount(pin.bitCount);

                float otherPinHeight = SubChipInstance.PinHeightFromBitCount(otherPin.bitCount);

                // Required space is half each pin's height plus some buffer
                float requiredSpacing = (pinHeight + otherPinHeight) / 2f + minPinSpacing;

                if (distanceAlongFace < requiredSpacing)
                {
                    overlappedPin = otherPin;
                    return true;
                }
            }

            return false;
        }

        static void FaceSnapping(PinInstance pin, float mouseY)
            {
                if (pin.parent is SubChipInstance chip)
                {
                    Vector2 chipSize = chip.Size;
                    Vector2 chipPos = chip.Position;

                    //Calculate distances to top and bottom edges
                    float distanceToTop = Mathf.Abs(mouseY - (chipPos.y + chipSize.y / 2));
                    float distanceToBottom = Mathf.Abs(mouseY - (chipPos.y - chipSize.y / 2));

                    //Calculate distances to left and right edges
                    float distanceToLeft = Mathf.Abs(chipPos.x - chipSize.x / 2);
                    float distanceToRight = Mathf.Abs(chipPos.x + chipSize.x / 2);

                    //Determine the closest vertical edge (top or bottom)
                    float closestVerticalDistance = Mathf.Min(distanceToTop, distanceToBottom);
                    bool isTopCloser = closestVerticalDistance == distanceToTop;

                    //Determine the closest horizontal edge (left or right)
                    float closestHorizontalDistance = Mathf.Min(distanceToLeft, distanceToRight);
                    bool isLeftCloser = closestHorizontalDistance == distanceToLeft;
                    //Compare the closest of the 2 previously closests
                    if (closestVerticalDistance < closestHorizontalDistance)
                    {
                        if (isTopCloser) {pin.face = 0;}
                        else {pin.face = 2;}
                    }
                    else
                    {
                        if (isLeftCloser) {pin.face = 3;}
                        else{ pin.face = 1; }
                    }
                }
            }
        
	}
}