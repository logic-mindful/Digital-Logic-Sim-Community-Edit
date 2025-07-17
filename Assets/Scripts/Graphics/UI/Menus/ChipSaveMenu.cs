using System;
using DLS.Description;
using DLS.Game;
using DLS.SaveSystem;
using Seb.Types;
using Seb.Vis;
using Seb.Vis.UI;
using UnityEngine;
using Random = System.Random;

namespace DLS.Graphics
{
	public static class ChipSaveMenu
	{
		public const string MaxLengthChipName = "MY VERY LONG CHIP NAME";

		const int CancelButtonIndex = 0;
		const int CustomizeButtonIndex = 1;
		const int SaveButtonIndex = 2;
		const int SaveAsButtonIndex = 3;
		static readonly UIHandle ID_ChipNameField = new("SaveMenu_ChipNameField");
		static readonly Func<string, bool> chipNameValidator = ValidateChipNameInput;
		static readonly Random rng = new();

		public static SubChipInstance ActiveCustomizeChip;
		static SubChipInstance CustomizeStateBeforeEnteringCustomizeMenu;

		static readonly string[] CancelSaveButtonNames =
		{
			"CANCEL", "CUSTOMIZE", "SAVE"
		};

		static readonly string[] CancelRenameSaveButtonNames =
		{
			"CANCEL", "CUSTOMIZE", "RENAME", "SAVE AS"
		};

		static readonly bool[] ButtonGroupInteractStates = { true, true, true, true };
		public static ChipDescription ActiveCustomizeDescription => ActiveCustomizeChip.Description;

		public static void OnMenuOpened()
		{
			ActiveCustomizeChip ??= CreateCustomizationState();
			InitUIFromDescription(ActiveCustomizeChip.Description);
		}

		public static (Vector2 size, float pad) GetTextInputSize()
		{
			const float textPad = 2;
			InputFieldTheme inputTheme = DrawSettings.ActiveUITheme.ChipNameInputField;
			Vector2 inputFieldSize = UI.CalculateTextSize(MaxLengthChipName, inputTheme.fontSize, inputTheme.font) + new Vector2(textPad * 2, 3);
			return (inputFieldSize, textPad);
		}

		public static void DrawMenu()
		{
			MenuHelper.DrawBackgroundOverlay();

			DrawSettings.UIThemeDLS theme = DrawSettings.ActiveUITheme;
			InputFieldTheme inputTheme = DrawSettings.ActiveUITheme.ChipNameInputField;
			InputFieldState inputFieldState;

			using (UI.BeginBoundsScope(true))
			{
				Draw.ID panelID = UI.ReservePanel();

				// -- Chip name input field --
				(Vector2 inputFieldSize, float inputFieldTextPad) = GetTextInputSize();
				inputFieldState = UI.InputField(ID_ChipNameField, inputTheme, new Vector2(50, 33), inputFieldSize, "Name", Anchor.Centre, inputFieldTextPad, chipNameValidator, true);

				Vector2 buttonTopLeft = UI.PrevBounds.BottomLeft + Vector2.down * (DrawSettings.DefaultButtonSpacing * 2);
				bool renaming = Project.ActiveProject.ChipHasBeenSavedBefore && !ChipDescription.NameMatch(inputFieldState.text, Project.ActiveProject.ViewedChip.LastSavedDescription.Name);

				bool saveButtonEnabled = IsValidSaveName(inputFieldState.text);
				ButtonGroupInteractStates[SaveButtonIndex] = saveButtonEnabled;
				ButtonGroupInteractStates[SaveAsButtonIndex] = saveButtonEnabled;
				string[] buttonGroupNames = renaming ? CancelRenameSaveButtonNames : CancelSaveButtonNames;
				int buttonIndex = UI.HorizontalButtonGroup(buttonGroupNames, ButtonGroupInteractStates, theme.ButtonTheme, buttonTopLeft, UI.PrevBounds.Width, DrawSettings.DefaultButtonSpacing, 0, Anchor.TopLeft);
				bool confirmShortcut = !renaming && KeyboardShortcuts.ConfirmShortcutTriggered;

				if (buttonIndex == CancelButtonIndex || KeyboardShortcuts.CancelShortcutTriggered)
				{
					Cancel();
				}
				else if (buttonIndex == CustomizeButtonIndex)
				{
					OpenCustomizationMenu();
				}
				else if (buttonIndex == SaveButtonIndex || confirmShortcut)
				{
					Save(renaming ? Project.SaveMode.Rename : Project.SaveMode.Normal);
				}
				else if (buttonIndex == SaveAsButtonIndex)
				{
					Save(Project.SaveMode.SaveAs);
				}

				Bounds2D uiBounds = UI.GetCurrentBoundsScope();
				MenuHelper.DrawReservedMenuPanel(panelID, uiBounds);

				// Update customization state
				if (ActiveCustomizeChip != null)
				{
					string newName = inputFieldState.text;
					if (ActiveCustomizeDescription.Name != newName)
					{
						ActiveCustomizeDescription.Name = newName;
						Vector2 minChipSize = SubChipInstance.CalculateMinChipSize(ActiveCustomizeDescription.InputPins, ActiveCustomizeDescription.OutputPins, newName);
						Vector2 chipSizeNew = Vector2.Max(minChipSize, ActiveCustomizeDescription.Size);
						ActiveCustomizeDescription.Size = chipSizeNew;
					}
				}
			}
		}

        // Create a subchip instance based on the current dev chip (we need a subchip instance to be able to draw a preview of the chip in the customization menu)
        // The description on this subchip holds potential customizations, such as name changes, resizing, colour etc.
		// This will load custom pin layouts if available to support editting existing chips. if no custom layout will use default behaviours.
        static SubChipInstance CreateCustomizationState()
        {
            DevChipInstance viewedChip = Project.ActiveProject.ViewedChip;
            ChipDescription desc = DescriptionCreator.CreateChipDescription(viewedChip);

            desc.HasCustomLayout = viewedChip.HasCustomLayout;

            // Copy layout if it exists
            if (desc.HasCustomLayout && viewedChip.LastSavedDescription != null)
            {
                var savedDesc = viewedChip.LastSavedDescription;

                for (int i = 0; i < desc.InputPins.Length && i < savedDesc.InputPins.Length; i++)
                {
                    desc.InputPins[i].face = savedDesc.InputPins[i].face;
                    desc.InputPins[i].LocalOffset = savedDesc.InputPins[i].LocalOffset;
                }

                for (int i = 0; i < desc.OutputPins.Length && i < savedDesc.OutputPins.Length; i++)
                {
                    desc.OutputPins[i].face = savedDesc.OutputPins[i].face;
                    desc.OutputPins[i].LocalOffset = savedDesc.OutputPins[i].LocalOffset;
                }
            }

            return CreatePreviewSubChipInstance(desc);
        }

        static void OpenCustomizationMenu()
		{
			ActiveCustomizeChip = CreatePreviewSubChipInstance(ActiveCustomizeDescription);
			CustomizeStateBeforeEnteringCustomizeMenu = CreatePreviewSubChipInstance(Saver.CloneChipDescription(ActiveCustomizeDescription));
			UIDrawer.SetActiveMenu(UIDrawer.MenuType.ChipCustomization);
		}

		public static void RevertCustomizationStateToBeforeEnteringCustomizeMenu()
		{
			ActiveCustomizeChip = CustomizeStateBeforeEnteringCustomizeMenu;
			CustomizeStateBeforeEnteringCustomizeMenu = CreatePreviewSubChipInstance(Saver.CloneChipDescription(ActiveCustomizeDescription));
		}

		static SubChipInstance CreatePreviewSubChipInstance(ChipDescription desc)
		{
			SubChipDescription subChipDesc = new(desc.Name, 0, string.Empty, Vector2.zero, Array.Empty<OutputPinColourInfo>());
			return new SubChipInstance(desc, subChipDesc);
		}

		public static bool ValidateChipNameInput(string nameInput) => nameInput.Length <= MaxLengthChipName.Length && !SaveUtils.NameContainsForbiddenChar(nameInput);

		static bool IsValidSaveName(string chipName)
		{
			Project project = Project.ActiveProject;

			bool validName = !string.IsNullOrWhiteSpace(chipName) && SaveUtils.ValidFileName(chipName);
			bool nameAlreadyUsed = project.chipLibrary.HasChip(chipName);
			bool isNameOfActiveChip = ChipDescription.NameMatch(project.ActiveDevChipName, chipName);

			bool isValid = validName && (!nameAlreadyUsed || isNameOfActiveChip);

			return isValid;
		}

		static void InitUIFromDescription(ChipDescription chipDesc)
		{
			// Set input field to current chip name
			InputFieldState inputFieldState = UI.GetInputFieldState(ID_ChipNameField);
			inputFieldState.SetText(chipDesc.Name);
		}


		static void Save(Project.SaveMode mode)
		{
			Project.ActiveProject.SaveFromDescription(ActiveCustomizeDescription, mode);
			CloseMenu();
		}

		static void Cancel()
		{
			CloseMenu();
		}

		static void CloseMenu()
		{
			ActiveCustomizeChip = null;
			UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
		}

		public static void Reset()
		{
			ActiveCustomizeChip = null;
			CustomizeStateBeforeEnteringCustomizeMenu = null;
		}

		static Color RandomInitialColour()
		{
			float h = (float)rng.NextDouble();
			float s = Mathf.Lerp(0.2f, 1, (float)rng.NextDouble());
			float v = Mathf.Lerp(0.2f, 1, (float)rng.NextDouble());
			return Color.HSVToRGB(h, s, v);
		}
	}
}