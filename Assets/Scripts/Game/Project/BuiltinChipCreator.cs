using System;
using System.Collections.Generic;
using System.Linq;
using DLS.Description;
using DLS.Simulation;
using UnityEngine;
using static DLS.Graphics.DrawSettings;

namespace DLS.Game
{
	public static class BuiltinChipCreator
	{
		static readonly Color ChipCol_SplitMerge = new(0.1f, 0.1f, 0.1f); //new(0.8f, 0.8f, 0.8f);
		static bool AllBlack;
		static Color AllBlackColor = Color.black;

		public static ChipDescription[] CreateAllBuiltinChipDescriptions(ProjectDescription description)
		{
			AllBlack = description.ProjectName.Contains("ahic");

			return new[]
			{
				CreateInputKeyChip(),
				CreateInputButtonChip(),
				CreateInputToggleChip(),
				
				// ---- Basic Chips ----
				CreateNand(),
				CreateTristateBuffer(),
				CreateClock(),
				CreatePulse(),
				CreateConstant_8(),
				CreateDetector(),

				// ---- Memory ----
				dev_CreateRAM_8(),
				CreateROM_8(),
				CreateEEPROM_8(),

				// ---- Merge / Split ----

				// ---- Displays ----
				CreateDisplay7Seg(),
				CreateDisplayRGB(),
				CreateDisplayDot(),
				CreateDisplayLED(),
				// ---- Audio ----
				CreateBuzzer(),
				// ---- Clock ----
				CreateSPSChip(),
				// ---- Time ----
				CreateRTC()
			}
			.Concat(CreateInOutPins(description.pinBitCounts))
			.Concat(CreateSplitMergePins(description.SplitMergePairs))
			.Concat(CreateBusAndBusTerminus(description.pinBitCounts))
			.ToArray();
				
			
		}

		static ChipDescription[] CreateInOutPins(List<PinBitCount> pinBitCountsToLoad)
		{
			ChipDescription[] DevPinDescriptions = new ChipDescription[pinBitCountsToLoad.Count * 2];
			

            for (int i = 0; i < pinBitCountsToLoad.Count; i++)
            {
                DevPinDescriptions[i * 2] = CreateInPin(pinBitCountsToLoad[i]);
                DevPinDescriptions[i * 2 + 1] = CreateOutPin(pinBitCountsToLoad[i]);
            }
            return DevPinDescriptions;
        }

		public static ChipDescription CreateInPin(PinBitCount pinBitCount)
		{
            PinDescription[] outPin = new[] { CreatePinDescription("IN", 0, pinBitCount) };
            ChipDescription InChip = CreateBuiltinChipDescription(ChipType.In_Pin, Vector2.zero, Color.clear, null, outPin, null,
                NameDisplayLocation.Hidden, name: ChipTypeHelper.GetDevPinName(true, pinBitCount));
			return InChip;
        }

		public static ChipDescription CreateOutPin(PinBitCount pinBitCount)
		{
            PinDescription[] inPin = new[] { CreatePinDescription("OUT", 0, pinBitCount) };

            ChipDescription OutChip = CreateBuiltinChipDescription(ChipType.Out_Pin, Vector2.zero, Color.clear, inPin, null, null,
                NameDisplayLocation.Hidden, name: ChipTypeHelper.GetDevPinName(false, pinBitCount));

			return OutChip;
        }

        static ChipDescription[] CreateSplitMergePins(List<KeyValuePair<PinBitCount, PinBitCount>> pairs)
		{
			ChipDescription[] SplitMergeDescriptions = new ChipDescription[pairs.Count * 2];

			for (int i = 0; i < pairs.Count; i++)
			{
				SplitMergeDescriptions[i * 2]     = CreateMergeChip(pairs[i]);
				SplitMergeDescriptions[i * 2 + 1] = CreateSplitChip(pairs[i]);
            }

            return SplitMergeDescriptions;
		}

		public static ChipDescription CreateSplitChip(KeyValuePair<PinBitCount, PinBitCount> pair)
		{
            (PinBitCount a, PinBitCount b) counts = (pair.Key, pair.Value);
            int smallInBig = counts.a / counts.b;

            PinDescription[] splitIN = new[] { CreatePinDescription("IN", 0, counts.a) };
            PinDescription[] splitOUT = new PinDescription[smallInBig];

            for (int j = 0; j < smallInBig; j++)
            {
                string letter = " " + (char)('A' + smallInBig -1 - j);
                splitOUT[j] = CreatePinDescription("OUT" + letter, j + 1, counts.b);
            }
            string splitName = counts.a.ToString() + "-" + counts.b.ToString() + "BIT";

            Vector2 minChipSize = SubChipInstance.CalculateMinChipSize(splitIN, splitOUT, splitName);
            float width = Mathf.Max(GridSize * 9, minChipSize.x);

            Vector2 size = new Vector2(width, minChipSize.y);
			return CreateBuiltinChipDescription(ChipType.Split_Pin, size, GetColor(ChipCol_SplitMerge), splitIN, splitOUT, name: splitName);
        }

        public static ChipDescription CreateMergeChip(KeyValuePair<PinBitCount, PinBitCount> pair)
        {
            (PinBitCount a, PinBitCount b) counts = (pair.Key, pair.Value);
            int smallInBig = counts.a / counts.b;

            PinDescription[] mergeIN = new PinDescription[smallInBig];
            PinDescription[] mergeOUT = new[] { CreatePinDescription("OUT", smallInBig, counts.a) };

            for (int j = 0; j < smallInBig; j++)
            {
                string letter = " " + (char)('A' + smallInBig -1 - j);
                mergeIN[j] = CreatePinDescription("IN" + letter, j, counts.b);
            }
            string mergeName = counts.b.ToString() + "-" + counts.a.ToString() + "BIT";

            Vector2 minChipSize = SubChipInstance.CalculateMinChipSize(mergeIN, mergeOUT, mergeName);
            float width = Mathf.Max(GridSize * 9, minChipSize.x);
            Vector2 size = new Vector2(width, minChipSize.y);

            return CreateBuiltinChipDescription(ChipType.Merge_Pin, size, GetColor(ChipCol_SplitMerge), mergeIN, mergeOUT, name: mergeName);
        }


        static ChipDescription[] CreateBusAndBusTerminus(List<PinBitCount> pinBitCountsToLoad)
		{
			ChipDescription[] descriptions = new ChipDescription[pinBitCountsToLoad.Count*2];


			for(int i = 0; i < pinBitCountsToLoad.Count; i++)
			{
				descriptions[i] = CreateBus(pinBitCountsToLoad[i]);
			}
            for (int i = 0; i < pinBitCountsToLoad.Count; i++)
            {
				descriptions[i + pinBitCountsToLoad.Count] = CreateBusTerminus(pinBitCountsToLoad[i]);
            }


            return descriptions;

		}
        static ChipDescription CreateNand()
		{
			Color col = GetColor(new(0.73f, 0.26f, 0.26f));
			Vector2 size = new(CalculateGridSnappedWidth(GridSize * 8), GridSize * 4);

			PinDescription[] inputPins = { CreatePinDescription("IN B", 0), CreatePinDescription("IN A", 1) };
			PinDescription[] outputPins = { CreatePinDescription("OUT", 2) };

			return CreateBuiltinChipDescription(ChipType.Nand, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateBuzzer()
		{
			Color col = GetColor(new(0, 0, 0));

			PinDescription[] inputPins =
			{
				CreatePinDescription("PITCH", 1, PinBitCount.Bit8),
				CreatePinDescription("VOLUME", 0, PinBitCount.Bit4),
			};

			float height = SubChipInstance.MinChipHeightForPins(inputPins, null);
			Vector2 size = new(CalculateGridSnappedWidth(GridSize * 9), height);

			return CreateBuiltinChipDescription(ChipType.Buzzer, size, col, inputPins, null, null);
		}
		static ChipDescription CreateSPSChip()
		{
			Color col = new(0.4f, 0.3f, 0.3f);

			PinDescription[] outputPins =
			{
				CreatePinDescription("SPCT", 3, PinBitCount.Bit16),
				CreatePinDescription("SPS", 2, PinBitCount.Bit16),
				CreatePinDescription("SPCT_OVERFLOW", 1, PinBitCount.Bit1),
				CreatePinDescription("SPS_OVERFLOW", 0, PinBitCount.Bit1),
			};
			
			float height = SubChipInstance.MinChipHeightForPins(outputPins, null);
			Vector2 size = new(CalculateGridSnappedWidth(GridSize * 9), height);

			return CreateBuiltinChipDescription(ChipType.SPS, size, col, null, outputPins);
		}

		static ChipDescription CreateRTC()
		{
			Color col = new(0.4f, 0.4f, 0.4f);

			PinDescription[] outputPins =
			{
				CreatePinDescription("TIME", 0, PinBitCount.Bit32),
			};

			float height = SubChipInstance.MinChipHeightForPins(outputPins, null);
			Vector2 size = new(CalculateGridSnappedWidth(GridSize * 9), height);

			return CreateBuiltinChipDescription(ChipType.RTC, size, col, null, outputPins);
		}

		static ChipDescription dev_CreateRAM_8()
		{
			Color col = GetColor(new(0.85f, 0.45f, 0.3f));

			PinDescription[] inputPins =
			{
				CreatePinDescription("ADDRESS", 0, PinBitCount.Bit8),
				CreatePinDescription("DATA", 1, PinBitCount.Bit8),
				CreatePinDescription("WRITE", 2),
				CreatePinDescription("RESET", 3),
				CreatePinDescription("CLOCK", 4)
			};
			PinDescription[] outputPins = { CreatePinDescription("OUT", 5, PinBitCount.Bit8) };
			Vector2 size = new(GridSize * 10, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(ChipType.dev_Ram_8Bit, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateROM_8()
		{
			PinDescription[] inputPins =
			{
				CreatePinDescription("ADDRESS", 0, PinBitCount.Bit8)
			};
			PinDescription[] outputPins =
			{
                CreatePinDescription("OUT B", 1, PinBitCount.Bit8),
                CreatePinDescription("OUT A", 2, PinBitCount.Bit8)
            };

			Color col = GetColor(new(0.25f, 0.35f, 0.5f));
			Vector2 size = new(GridSize * 12, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(ChipType.Rom_256x16, size, col, inputPins, outputPins);
		}

        static ChipDescription CreateEEPROM_8()
        {
            PinDescription[] inputPins =
            {
                CreatePinDescription("ADDRESS", 0, PinBitCount.Bit8),
				CreatePinDescription("DATA B", 1, PinBitCount.Bit8),
				CreatePinDescription("DATA A", 2, PinBitCount.Bit8),
                CreatePinDescription("WRITE", 3, PinBitCount.Bit1),
				CreatePinDescription("CLOCK", 4, PinBitCount.Bit1)
            };
            PinDescription[] outputPins =
            {
                CreatePinDescription("OUT B", 5, PinBitCount.Bit8),
				CreatePinDescription("OUT A", 6, PinBitCount.Bit8)

            };

            Color col = GetColor(new(0.25f, 0.35f, 0.5f));
            Vector2 size = new(GridSize * 12, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

            return CreateBuiltinChipDescription(ChipType.EEPROM_256x16, size, col, inputPins, outputPins);
        }

		static ChipDescription CreateConstant_8()
		{
			PinDescription[] outputPins =
			{
				CreatePinDescription("VALUE OUT", 0, PinBitCount.Bit8),
			};

			Color col = new(0.1f, 0.1f, 0.1f);
			Vector2 size = Vector2.one * GridSize * 6;

			return CreateBuiltinChipDescription(ChipType.Constant_8Bit, size, col, null, outputPins);
        }

        static ChipDescription CreateDetector()
        {
			PinDescription[] inputPins =
			{
				CreatePinDescription("IN", 0, PinBitCount.Bit1),
			};

            PinDescription[] outputPins =
            {
                CreatePinDescription("0", 1, PinBitCount.Bit1),
                CreatePinDescription("1", 2, PinBitCount.Bit1),
                CreatePinDescription("Z", 3, PinBitCount.Bit1),
            };

            Color col = new(0.1f, 0.1f, 0.3f);
            Vector2 size = new(GridSize * 12, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

            return CreateBuiltinChipDescription(ChipType.Detector, size, col, inputPins, outputPins);
        }


        static ChipDescription CreateInputKeyChip()
		{
			Color col = GetColor(new(0.1f, 0.1f, 0.1f));
			Vector2 size = new Vector2(GridSize, GridSize) * 3;

			PinDescription[] outputPins = { CreatePinDescription("OUT", 0) };

			return CreateBuiltinChipDescription(ChipType.Key, size, col, null, outputPins, null, NameDisplayLocation.Hidden);
		}

        static ChipDescription CreateInputButtonChip()
        {
            Color col = GetColor(new(0.1f, 0.1f, 0.1f));
            Vector2 size = new Vector2(GridSize, GridSize) * 3;
			float displayWidth = size.x - GridSize *0.5f;

            PinDescription[] outputPins = { CreatePinDescription("OUT", 0) };
			DisplayDescription[] displays =
			{
				new()
				{
					Position = Vector2.zero,
					Scale = displayWidth,
					SubChipID = -1
				}
			};

            return CreateBuiltinChipDescription(ChipType.Button, size, col, null, outputPins, displays, NameDisplayLocation.Hidden);
        }

        static ChipDescription CreateInputToggleChip()
        {
            Color col = GetColor(new(70, 130, 180));
            Vector2 size = new Vector2(1f, 2f) * GridSize;
            float displayWidth = size.x;

            PinDescription[] outputPins = { CreatePinDescription("OUT", 0) };
            DisplayDescription[] displays =
            {
                new()
                {
                    Position = Vector2.zero,
                    Scale = displayWidth,
                    SubChipID = -1
                }
            };

            return CreateBuiltinChipDescription(ChipType.Toggle, size, col, null, outputPins, displays, NameDisplayLocation.Hidden);
        }


        static ChipDescription CreateTristateBuffer()
		{
			Color col = GetColor(new(0.1f, 0.1f, 0.1f));
			Vector2 size = new(CalculateGridSnappedWidth(1.5f), GridSize * 5);

			PinDescription[] inputPins = { CreatePinDescription("IN", 0), CreatePinDescription("ENABLE", 1) };
			PinDescription[] outputPins = { CreatePinDescription("OUT", 2) };

			return CreateBuiltinChipDescription(ChipType.TriStateBuffer, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateClock()
		{
			Vector2 size = new(GridHelper.SnapToGrid(1), GridSize * 3);
			Color col = GetColor(new(0.1f, 0.1f, 0.1f));
			PinDescription[] outputPins = { CreatePinDescription("CLK", 0) };

			return CreateBuiltinChipDescription(ChipType.Clock, size, col, null, outputPins);
		}

		static ChipDescription CreatePulse()
		{
			Vector2 size = new(GridHelper.SnapToGrid(1), GridSize * 3);
			Color col = GetColor(new(0.1f, 0.1f, 0.1f));
			PinDescription[] inputPins = { CreatePinDescription("IN", 0) };
			PinDescription[] outputPins = { CreatePinDescription("PULSE", 1) };

			return CreateBuiltinChipDescription(ChipType.Pulse, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateDisplay7Seg()
		{
			PinDescription[] inputPins =
			{
				CreatePinDescription("A", 0),
				CreatePinDescription("B", 1),
				CreatePinDescription("C", 2),
				CreatePinDescription("D", 3),
				CreatePinDescription("E", 4),
				CreatePinDescription("F", 5),
				CreatePinDescription("G", 6),
				CreatePinDescription("COL", 7)
			};

			Color col = GetColor(new(0.1f, 0.1f, 0.1f));
			float height = SubChipInstance.MinChipHeightForPins(inputPins, null);
			Vector2 size = new(GridSize * 10, height);
			float displayWidth = size.x - GridSize * 2;

			DisplayDescription[] displays =
			{
				new()
				{
					Position = Vector2.zero,
					Scale = displayWidth,
					SubChipID = -1
				}
			};
			return CreateBuiltinChipDescription(ChipType.SevenSegmentDisplay, size, col, inputPins, null, displays, NameDisplayLocation.Hidden);
		}

		static ChipDescription CreateDisplayRGB()
		{
			float height = GridSize * 21;
			float width = height;
			float displayWidth = height - GridSize * 2;

			Color col = GetColor(new(0.1f, 0.1f, 0.1f));
			Vector2 size = new(width, height);

			PinDescription[] inputPins =
			{
				CreatePinDescription("ADDRESS", 0, PinBitCount.Bit8),
				CreatePinDescription("RED", 1, PinBitCount.Bit4),
				CreatePinDescription("GREEN", 2, PinBitCount.Bit4),
				CreatePinDescription("BLUE", 3, PinBitCount.Bit4),
				CreatePinDescription("RESET", 4),
				CreatePinDescription("WRITE", 5),
				CreatePinDescription("REFRESH", 6),
				CreatePinDescription("CLOCK", 7)
			};

			PinDescription[] outputPins =
			{
				CreatePinDescription("R OUT", 8, PinBitCount.Bit4),
				CreatePinDescription("G OUT", 9, PinBitCount.Bit4),
				CreatePinDescription("B OUT", 10, PinBitCount.Bit4)
			};

			DisplayDescription[] displays =
			{
				new()
				{
					Position = Vector2.zero,
					Scale = displayWidth,
					SubChipID = -1
				}
			};

			return CreateBuiltinChipDescription(ChipType.DisplayRGB, size, col, inputPins, outputPins, displays, NameDisplayLocation.Hidden);
		}

		static ChipDescription CreateDisplayDot()
		{
			PinDescription[] inputPins =
			{
				CreatePinDescription("ADDRESS", 0, PinBitCount.Bit8),
				CreatePinDescription("PIXEL IN", 1),
				CreatePinDescription("RESET", 2),
				CreatePinDescription("WRITE", 3),
				CreatePinDescription("REFRESH", 4),
				CreatePinDescription("CLOCK", 5)
			};

			PinDescription[] outputPins =
			{
				CreatePinDescription("PIXEL OUT", 6)
			};

			float height = SubChipInstance.MinChipHeightForPins(inputPins, null);
			float width = height;
			float displayWidth = height - GridSize * 2;

			Color col = GetColor(new(0.1f, 0.1f, 0.1f));
			Vector2 size = new(width, height);


			DisplayDescription[] displays =
			{
				new()
				{
					Position = Vector2.zero,
					Scale = displayWidth,
					SubChipID = -1
				}
			};

			return CreateBuiltinChipDescription(ChipType.DisplayDot, size, col, inputPins, outputPins, displays, NameDisplayLocation.Hidden);
		}

		static Vector2 BusChipSize(PinBitCount bitCount)
		{
			return bitCount.BitCount switch
			{
				PinBitCount.Bit1 => new Vector2(GridSize * 2, GridSize * 2),
				PinBitCount.Bit4 => new Vector2(GridSize * 2, GridSize * 3),
				PinBitCount.Bit8 => new Vector2(GridSize * 2, GridSize * 4),
				_ => new Vector2(GridSize * 2, 0.5f * bitCount.BitCount * GridSize)
			};
		}

		public static ChipDescription CreateBus(PinBitCount bitCount)
		{

			string name = ChipTypeHelper.GetBusName(bitCount);

			PinDescription[] inputs = { CreatePinDescription(name + " (Hidden)", 0, bitCount) };
			PinDescription[] outputs = { CreatePinDescription(name, 1, bitCount) };

			Color col = GetColor(new(0.1f, 0.1f, 0.1f));

			return CreateBuiltinChipDescription(ChipType.Bus, BusChipSize(bitCount), col, inputs, outputs, null, NameDisplayLocation.Hidden, name:name);
		}

		static ChipDescription CreateDisplayLED()
		{
			PinDescription[] inputPins =
			{
				CreatePinDescription("IN", 0)
			};

			float height = SubChipInstance.MinChipHeightForPins(inputPins, null);
			float width = height;
			float displayWidth = height - GridSize * 0.5f;

			Color col = GetColor(new(0.1f, 0.1f, 0.1f));
			Vector2 size = new(width, height);


			DisplayDescription[] displays =
			{
				new()
				{
					Position = Vector2.zero,
					Scale = displayWidth,
					SubChipID = -1
				}
			};

			return CreateBuiltinChipDescription(ChipType.DisplayLED, size, col, inputPins, null, displays, NameDisplayLocation.Hidden);
		}


		public static ChipDescription CreateBusTerminus(PinBitCount bitCount)
		{
			string name = ChipTypeHelper.GetBusTerminusName(bitCount);
			ChipDescription busOrigin = CreateBus(bitCount);
			PinDescription[] inputs = { CreatePinDescription(busOrigin.Name, 0, bitCount) };

			return CreateBuiltinChipDescription(ChipType.BusTerminus, BusChipSize(bitCount), busOrigin.Colour, inputs, null, null, NameDisplayLocation.Hidden, name);
		}


		static ChipDescription CreateBuiltinChipDescription(ChipType type, Vector2 size, Color col, PinDescription[] inputs, PinDescription[] outputs, DisplayDescription[] displays = null, NameDisplayLocation nameLoc = NameDisplayLocation.Centre, string name = "")
		{
			if (!ChipTypeHelper.IsDevPin(type) && !ChipTypeHelper.IsMergeSplitChip(type) && !ChipTypeHelper.IsBusType(type)){name = ChipTypeHelper.GetName(type); }
			
			ValidatePinIDs(inputs, outputs, name);

			return new ChipDescription
			{
				Name = name,
				NameLocation = nameLoc,
				Colour = col,
				Size = new Vector2(size.x, size.y),
				InputPins = inputs ?? Array.Empty<PinDescription>(),
				OutputPins = outputs ?? Array.Empty<PinDescription>(),
				SubChips = Array.Empty<SubChipDescription>(),
				Wires = Array.Empty<WireDescription>(),
				Displays = displays,
				ChipType = type
			};
		}

		static PinDescription CreatePinDescription(string name, int id, ushort bitcount = 1) =>
			new(
				name,
				id,
				Vector2.zero,
				new(bitcount),
				PinColour.Red,
				PinValueDisplayMode.Off
			);

		static float CalculateGridSnappedWidth(float desiredWidth) =>
			// Calculate width such that spacing between an input and output pin on chip will align with grid
			GridHelper.SnapToGridForceEven(desiredWidth) - (ChipOutlineWidth - 2 * SubChipPinInset);

		static void ValidatePinIDs(PinDescription[] inputs, PinDescription[] outputs, string chipName)
		{
			HashSet<int> pinIDs = new();

			AddPins(inputs);
			AddPins(outputs);
			return;

			void AddPins(PinDescription[] pins)
			{
				if (pins == null) return;
				foreach (PinDescription pin in pins)
				{
					if (!pinIDs.Add(pin.ID))
					{
						throw new Exception($"Pin has duplicate ID ({pin.ID}) in builtin chip: {chipName}");
					}
				}
			}
		}

		static Color GetColor(Color color)
		{
			return AllBlack ? AllBlackColor : color;
		}
	}
}
