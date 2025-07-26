using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using DLS.Description;
using DLS.Game;
using NUnit.Framework.Interfaces;
using Random = System.Random;
using System.Security.Cryptography;

namespace DLS.Simulation
{
	public static class Constants
	{
		public const uint LOGIC_LOW = 0;
        public const uint LOGIC_HIGH = 1;
        public const uint LOGIC_DISCONNECTED = 2;
    }
    public static class Simulator
	{
    // Constant for when the chip caching shouldn't happen -- simply because we lack type to store bigger values.
		// If a chip has a bigger input than that, it will not be cache
		public const int MAX_INPUT_WIDTH_WHEN_AUTO_CACHING = 16;

		// Constants, for when a chip should be cached. If a chip is purely combinational and has at most AUTO_CACHING number of input bits, it will always be cached.
		// Otherwise, a user can specifie a chip to be cached, if the chip is combinational and has at most USER_CACHING number of input bits.
		// If a chip has more than USER_CACHING input bits, it will never be cached. (This is, because memory requirements grow exponentially with number of input bits.)
		public const int MAX_NUM_INPUT_BITS_WHEN_AUTO_CACHING = 12;
		public const int MAX_NUM_INPUT_BITS_WHEN_USER_CACHING = 24;

		// Small, purely combinational chips use a LUT for fast calculations. These are stored here. Maps the name of a chip to its LUT.
		public static readonly Dictionary<string, uint[][]> combinationalChipCaches = new();
		public static readonly HashSet<string> chipsKnowToNotBeCombinational = new();
		public static bool useCaching = true;

		// Variables for the creating cache info popup
		public static bool isCreatingACache = false;
		public static string nameOfChipWhoseCacheIsBeingCreated;
		public static float cacheCreatingProgress;

		public static readonly Random rng = new();
		static readonly Stopwatch stopwatch = Stopwatch.StartNew();
		public static int stepsPerClockTransition;
		public static int simulationFrame;
		static uint pcg_rngState;

		// When sim is first built, or whenever modified, it needs to run a less efficient pass in which the traversal order of the chips is determined
		public static bool needsOrderPass;

		// Every n frames the simulation permits some random modifications to traversal order of sequential chips (to randomize outcome of race conditions)
		public static bool canDynamicReorderThisFrame;

		static SimChip prevRootSimChip;
		static double elapsedSecondsOld;
		static double deltaTime;
		static SimAudio audioState;

		// Modifications to the sim are made from the main thread, but only applied on the sim thread to avoid conflicts
		static readonly ConcurrentQueue<SimModifyCommand> modificationQueue = new();

		// ---- Simulation outline ----
		// 1) Forward the initial player-controlled input states to all connected pins.
		// 2) Loop over all subchips not yet processed this frame, and process them if they are ready (i.e. all input pins have received all their inputs)
		//    * Note: this means that the input pins must be aware of how many input connections they have (pins choose randomly between conflicting inputs)
		//    * Note: if a pin has zero input connections, it should be considered as always ready
		// 3) Forward the outputs of the processed subchips to their connected pins, and repeat steps 2 & 3 until no more subchips are ready for processing.
		// 4) If all subchips have now been processed, then we're done. This is not necessarily the case though, since if an input pin depends on the output of its parent chip
		//    (directly or indirectly), then it won't receive all its inputs until the chip has already been run, meaning that the chip must be processed before it is ready.
		//    In this case we process one of the remaining unprocessed (and non-ready) subchips at random, and return to step 3.
		//
		// Optimization ideas (todo):
		// * Compute lookup table for combinational chips
		// * Ignore chip if inputs are same as last frame, and no internal pins changed state last frame.
		//   (would have to make exception for chips containing things like clock or key chip, which can activate 'spontaneously')
		// * Create simplified connections network allowing only builtin chips to be processed during simulation

		public static void RunSimulationStep(SimChip rootSimChip, DevPinInstance[] inputPins, SimAudio audioState)
		{
			Simulator.audioState = audioState;
			audioState.InitFrame();

			if (rootSimChip != prevRootSimChip)
			{
				needsOrderPass = true;
				prevRootSimChip = rootSimChip;
			}

			pcg_rngState = (uint)rng.Next();
			canDynamicReorderThisFrame = simulationFrame % 100 == 0;
			simulationFrame++; //

			// Step 1) Get player-controlled input states and copy values to the sim
			foreach (DevPinInstance input in inputPins)
			{
				try
				{
					SimPin simPin = rootSimChip.GetSimPinFromAddress(input.Pin.Address);
					input.Pin.State = input.Pin.PlayerInputState;
					simPin.State.CopyFrom(input.Pin.PlayerInputState);
				}
				catch (Exception)
				{
					// Possible for sim to be temporarily out of sync since running on separate threads, so just ignore failure to find pin.
				}
			}
			
			// Process
			if (needsOrderPass)
			{
				StepChipReorder(rootSimChip);
				needsOrderPass = false;
			}
			else
			{
				StepChip(rootSimChip);
			}

			UpdateAudioState();
			Project.ActiveProject.description.StepsRanSinceCreated++;
		}

		public static void UpdateInPausedState()
		{
			if (audioState != null)
			{
				audioState.InitFrame();
				UpdateAudioState();
			}
		}

		static void UpdateAudioState()
		{
			double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
			if (simulationFrame <= 1) deltaTime = 0;
			else deltaTime = elapsedSeconds - elapsedSecondsOld;
			elapsedSecondsOld = stopwatch.Elapsed.TotalSeconds;
			audioState.NotifyAllNotesRegistered(deltaTime);
		}

		// Recursively propagate signals through this chip and its subchips
		static void StepChip(SimChip chip)
		{
			// Propagate signal from all input dev-pins to all their connected pins
			chip.Sim_PropagateInputs();

			// NOTE: subchips are assumed to have been sorted in reverse order of desired visitation
			for (int i = chip.SubChips.Length - 1; i >= 0; i--)
			{
				SimChip nextSubChip = chip.SubChips[i];

				// Every n frames (for performance reasons) the simulation permits some random modifications to the chip traversal order.
				// Here two chips may be swapped if they are not 'ready' (i.e. all inputs have not yet been received for this
				// frame; indicating that the input relies on the output). The purpose of this reordering is to allow some variety in
				// the outcomes of race-conditions (such as an SR latch having both inputs enabled, and then released).
				if (canDynamicReorderThisFrame && i > 0 && !nextSubChip.Sim_IsReady() && RandomBool())
				{
					SimChip potentialSwapChip = chip.SubChips[i - 1];
					if (!ChipTypeHelper.IsBusOriginType(potentialSwapChip.ChipType))
					{
						nextSubChip = potentialSwapChip;
						(chip.SubChips[i], chip.SubChips[i - 1]) = (chip.SubChips[i - 1], chip.SubChips[i]);
					}
				}

				if (nextSubChip.IsBuiltin)
				{
					ProcessBuiltinChip(nextSubChip); // We've reached a built-in chip, so process it directly
				}
				else if (combinationalChipCaches.ContainsKey(nextSubChip.Name) && useCaching)
				{
					bool wasSuccessful = ProcessCachedChip(nextSubChip); // We found a cached chip, so use LUT to process it directly
					if (!wasSuccessful) StepChip(nextSubChip); // Fallback to normal simulation, if lookup failed
				}
				else if (!chipsKnowToNotBeCombinational.Contains(nextSubChip.Name) && useCaching)
				{
					RecalculateCachedLUTs(nextSubChip); // We found a chip that isn't cached but might be cachable, so we try to cache it
					StepChip(nextSubChip);
				}
				else
				{
					StepChip(nextSubChip); // Recursively process custom chip
				}

				// Step 3) Forward the outputs of the processed subchip to connected pins
				nextSubChip.Sim_PropagateOutputs();
			}
		}

		// Recursively propagate signals through this chip and its subchips
		// In the process, reorder all subchips based on order in which they become ready for processing (have received all their inputs)
		// Note: the order here is reversed, so those ready first will be at the end of the array
		static void StepChipReorder(SimChip chip)
		{
			chip.Sim_PropagateInputs();

			SimChip[] subChips = chip.SubChips;
			int numRemaining = subChips.Length;

			while (numRemaining > 0)
			{
				int nextSubChipIndex = ChooseNextSubChip(subChips, numRemaining);
				SimChip nextSubChip = subChips[nextSubChipIndex];

				// "Remove" the chosen subchip from remaining sub chips.
				// This is done by moving it to the end of the array and reducing the length of the span by one.
				// This also places the subchip into (reverse) order, so that the traversal order need to be determined again on the next pass.
				(subChips[nextSubChipIndex], subChips[numRemaining - 1]) = (subChips[numRemaining - 1], subChips[nextSubChipIndex]);
				numRemaining--;

				// Process chosen subchip
				if (nextSubChip.ChipType == ChipType.Custom) StepChipReorder(nextSubChip); // Recursively process custom chip
				else ProcessBuiltinChip(nextSubChip); // We've reached a built-in chip, so process it directly 

				// Step 3) Forward the outputs of the processed subchip to connected pins
				nextSubChip.Sim_PropagateOutputs();
			}
		}

		// Recalculates the caches of this chip and and all of its subChips.
		static void RecalculateCachedLUTs(SimChip chip)
		{
			// Skip this chip, if its cache status is already known
			if (combinationalChipCaches.ContainsKey(chip.Name) || chipsKnowToNotBeCombinational.Contains(chip.Name)) return;

			// Recalculate caches for the subChips of the passed chip recursively
			foreach (SimChip subChip in chip.SubChips)
			{
				RecalculateCachedLUTs(subChip);
			}

			// Don't cache this chip, if it isn't cachable
			if (chip.ChipType != ChipType.Custom
				|| (!chip.shouldBeCached && chip.CalculateNumberOfInputBits() > MAX_NUM_INPUT_BITS_WHEN_AUTO_CACHING)
				|| !chip.IsCombinational()
				|| chip.CalculateBiggestPinWidth() > MAX_INPUT_WIDTH_WHEN_AUTO_CACHING
				)
			{
				chipsKnowToNotBeCombinational.Add(chip.Name);
				return;
			}

			nameOfChipWhoseCacheIsBeingCreated = chip.Name;
			cacheCreatingProgress = 0;
			isCreatingACache = true;

			// Buffer current Input
			uint[] bufferedInput = new uint[chip.InputPins.Length];
			for (int i = 0; i < bufferedInput.Length; i++)
			{
				bufferedInput[i] = chip.InputPins[i].State.GetShort();
			}

			// Cache this chip
			int numberOfPossibleInputs = 1 << chip.CalculateNumberOfInputBits();
			uint[][] LUT = new uint[numberOfPossibleInputs][];
			for (int input = 0; input < numberOfPossibleInputs; input++)
			{
				chip.ResetReceivedFlagsOnAllPins(); // Make sure the chip only recieves our new input
				// Set all inputPins to their part of the input
				int tempInput = input;
				for (int i = 0; i < chip.InputPins.Length; i++)
				{
					uint mask = ((uint)1 << chip.InputPins[i].State.size) - 1;
					chip.InputPins[i].State.SetShort((uint)(tempInput & mask));
					tempInput >>= (int)chip.InputPins[i].State.size;
				}
				StepChip(chip); // Calculate Result

				// Store output into cache
				int numberOfOutputPins = chip.OutputPins.Length;
				uint[] outputs = new uint[numberOfOutputPins];
				for (int i = 0; i < numberOfOutputPins; i++)
				{
					outputs[i] = chip.OutputPins[i].State.GetShortValues();
				}
				LUT[input] = outputs;

				cacheCreatingProgress = (float)input / numberOfPossibleInputs;
				if (!isCreatingACache) return; // Cancel the caching, if something ordered the caching to be stopped
			}
			combinationalChipCaches[chip.Name] = LUT;

			// Reload buffered Input
			chip.ResetReceivedFlagsOnAllPins(); // Make sure the chip only recieves our new input
			for (int i = 0; i < bufferedInput.Length; i++)
			{
				chip.InputPins[i].State.SetShort(bufferedInput[i]);
			}
			StepChip(chip); // make sure the outputs are also correct again

			isCreatingACache = false;
		}
		
		static int ChooseNextSubChip(SimChip[] subChips, int num)
		{
			bool noSubChipsReady = true;
			bool isNonBusChipRemaining = false;
			int nextSubChipIndex = -1;

			// Step 2) Loop over all subchips not yet processed this frame, and process them if they are ready
			for (int i = 0; i < num; i++)
			{
				SimChip subChip = subChips[i];
				if (subChip.Sim_IsReady())
				{
					noSubChipsReady = false;
					nextSubChipIndex = i;
					break;
				}

				isNonBusChipRemaining |= !ChipTypeHelper.IsBusOriginType(subChip.ChipType);
			}

			// Step 4) if no sub chip is ready to be processed, pick one at random (but save buses for last)
			if (noSubChipsReady)
			{
				nextSubChipIndex = rng.Next(0, num);

				// If processing in random order, save buses for last (since we must know all their inputs to display correctly)
				if (isNonBusChipRemaining)
				{
					for (int i = 0; i < num; i++)
					{
						if (!ChipTypeHelper.IsBusOriginType(subChips[nextSubChipIndex].ChipType)) break;
						nextSubChipIndex = (nextSubChipIndex + 1) % num;
					}
				}
			}

			return nextSubChipIndex;
		}

		public static void UpdateKeyboardInputFromMainThread()
		{
			SimKeyboardHelper.RefreshInputState();
		}

		public static bool RandomBool()
		{
			pcg_rngState = pcg_rngState * 747796405 + 2891336453;
			uint result = ((pcg_rngState >> (int)((pcg_rngState >> 28) + 4)) ^ pcg_rngState) * 277803737;
			result = (result >> 22) ^ result;
			return result < uint.MaxValue / 2;
		}

		// Sets the output pins by using an LUT. Returns true if successful, false otherwise.
		static bool ProcessCachedChip(SimChip chip)
		{
			int input = 0;
			for (int i = chip.InputPins.Length - 1; i >= 0; i--)
			{
				if (chip.InputPins[i].State.GetShort() >> 16 != 0) return false; // Fails if at least one input is in TriState (as these are not cached)
				input <<= (int)chip.InputPins[i].State.size;
				input |= (int)chip.InputPins[i].State.GetShort();
			}
			uint[][] LUT = combinationalChipCaches[chip.Name];
			uint[] outputs = LUT[input];
			for (int i = 0; i < outputs.Length; i++)
			{
				chip.OutputPins[i].State.SetShort(outputs[i]);
			}
			return true;
		}

		static void ProcessBuiltinChip(SimChip chip)
		{
			switch (chip.ChipType)
			{
				// ---- Process Built-in chips ----
				case ChipType.Nand:
				{
                        uint nandOp = 1 ^ (chip.InputPins[0].State.a & chip.InputPins[1].State.a);

                        chip.OutputPins[0].State.a = (nandOp & 1);

                        break;
                }
				case ChipType.Clock:
				{
                        bool high = stepsPerClockTransition != 0 && ((simulationFrame / stepsPerClockTransition) & 1) == 0;
						chip.OutputPins[0].State.SmallSet((high ? Constants.LOGIC_HIGH : Constants.LOGIC_LOW));
                        break;
                }
				case ChipType.Pulse:
				{
                        const int pulseDurationIndex = 0;
                        const int pulseTicksRemainingIndex = 1;
                        const int pulseInputOldIndex = 2;

                        uint inputState = chip.InputPins[0].State.a;
                        bool pulseInputHigh = (inputState & 1) == 1;
                        uint pulseTicksRemaining = chip.InternalState[pulseTicksRemainingIndex];

                        if (pulseTicksRemaining == 0)
                        {
                            bool isRisingEdge = pulseInputHigh && chip.InternalState[pulseInputOldIndex] == 0;
                            if (isRisingEdge)
                            {
                                pulseTicksRemaining = chip.InternalState[pulseDurationIndex];
                                chip.InternalState[pulseTicksRemainingIndex] = pulseTicksRemaining;
                            }
                        }

                        uint outputState = 0;
                        if (pulseTicksRemaining > 0)
                        {
                            chip.InternalState[1]--;
                            outputState = 1;
                        }
                        else if ((inputState >> 1) != 0)
                        {
                            outputState = 2;
                        }

                        chip.OutputPins[0].State.a = outputState;
                        chip.InternalState[pulseInputOldIndex] = pulseInputHigh ? 1u : 0;

                        break;
                    }

                /*case ChipType.Split_Pin:
                    {
                        BitArray[] bitArrays = new BitArray[chip.OutputPins.Length];
                        for (int i = 0; i < chip.OutputPins.Length; i++)
                        {
                            bitArrays[i] = chip.OutputPins[i].State;
                        }
                        PinState.SplitOneToMany(ref bitArrays, chip.InputPins[0].State);
                        break;
                    }
                case ChipType.Merge_Pin:
                    {
                        BitArray[] bitArrays = new BitArray[chip.InputPins.Length];
                        for (int i = 0; i < chip.InputPins.Length; i++)
                        {
                            bitArrays[i] = chip.InputPins[i].State;
                        }
                        PinState.MergeManyToOne(ref chip.OutputPins[0].State, bitArrays);
                        break;
                    }*/

                case ChipType.TriStateBuffer:
				{
					SimPin outputPin = chip.OutputPins[0];	

					if (chip.InputPins[1].State.SmallHigh()) outputPin.State = chip.InputPins[0].State;
					else outputPin.State.a = 0b_0000_0000_0000_0001___0000_0000_0000_0000;

					break;
				}
				case ChipType.Key:
				{
					bool isHeld = SimKeyboardHelper.KeyIsHeld((char)chip.InternalState[0]);
					chip.OutputPins[0].State.SmallSet(isHeld ? Constants.LOGIC_HIGH : Constants.LOGIC_LOW);
					break;
				}
				case ChipType.DisplayRGB:
				{
					const uint addressSpace = 256;
					uint addressPin = chip.InputPins[0].State.GetShortValues();
					bool writePin = chip.InputPins[5].State.SmallHigh();
					bool clockPin = chip.InputPins[7].State.SmallHigh();

					bool isRisingEdge = clockPin && chip.InternalState[^1] == 0;
					chip.InternalState[^1] = clockPin ? 1u : 0;

					if (isRisingEdge)
					{
						// Clear back buffer
						if (chip.InputPins[4].State.SmallHigh())
						{
							for (int i = 0; i < addressSpace; i++)
							{
								chip.InternalState[i + addressSpace] = 0;
							}
						}
						// Write to back-buffer
						else if (writePin)
						{
							uint data = (chip.InputPins[1].State.GetShortValues() | (chip.InputPins[2].State.GetShortValues() << 4) | (chip.InputPins[3].State.GetShortValues() << 8));
							chip.InternalState[addressPin + addressSpace] = data;
						}

						// Copy back-buffer to display buffer
						if (chip.InputPins[6].State.SmallHigh())
						{
							for (int i = 0; i < addressSpace; i++)
							{
								chip.InternalState[i] = chip.InternalState[i + addressSpace];
							}
						}
					}

					// Output current pixel colour
					uint colData = chip.InternalState[addressPin];
					chip.OutputPins[0].State.SetShort((ushort)(colData & 0b1111));//red
					chip.OutputPins[1].State.SetShort((ushort)((colData >> 4) & 0b1111));//green
					chip.OutputPins[2].State.SetShort((ushort)((colData >> 8) & 0b1111)	);//blue


                    break;
				}
				case ChipType.DisplayDot:
				{
					const uint addressSpace = 256;
					uint addressPin = chip.InputPins[0].State.GetShortValues();
					bool clockPin = chip.InputPins[5].State.SmallHigh();

					// Detect clock rising edge
					bool isRisingEdge = clockPin && chip.InternalState[^1] == 0;
					chip.InternalState[^1] = clockPin ? 1u : 0;

					if (isRisingEdge)
					{
						// Clear back buffer
						if (chip.InputPins[2].State.SmallHigh())
						{
							for (int i = 0; i < addressSpace; i++)
							{
								chip.InternalState[i + addressSpace] = 0;
							}
						}
						// Write to back-buffer
						else if (chip.InputPins[3].State.SmallHigh())
						{
							uint data = (uint)(chip.InputPins[1].State.SmallHigh() ? 1 : 0);
							chip.InternalState[addressPin + addressSpace] = data;
						}

						// Copy back-buffer to display buffer
						if (chip.InputPins[4].State.SmallHigh())
						{
							for (int i = 0; i < addressSpace; i++)
							{
								chip.InternalState[i] = chip.InternalState[i + addressSpace];
							}
						}
					}

					// Output current pixel colour
					chip.OutputPins[0].State.SmallSet(chip.InternalState[addressPin]);
					break;
				}
				case ChipType.dev_Ram_8Bit:
				{
					uint addressPin = chip.InputPins[0].State.GetShortValues();

					// Detect clock rising edge
					bool clockHigh = chip.InputPins[4].State.SmallHigh();
					bool isRisingEdge = clockHigh && chip.InternalState[^1] == 0;
					chip.InternalState[^1] = clockHigh ? 1u : 0;

					// Write/Reset on rising edge
					if (isRisingEdge)
					{
						if (chip.InputPins[3].State.SmallHigh())
						{
							for (int i = 0; i < 256; i++)
							{
								chip.InternalState[i] = 0;
							}
						}
						else if (chip.InputPins[2].State.SmallHigh())
						{
							chip.InternalState[addressPin] = chip.InputPins[1].State.GetShortValues();
						}
					}

					// Output data at current address
					chip.OutputPins[0].State.SetShort((ushort)chip.InternalState[addressPin]);

					break;
				}
				case ChipType.Rom_256x16:
				{
					const uint mask = 0x00ff;
					uint address = chip.InputPins[0].State.GetShortValues();
					uint data = chip.InternalState[address];

					chip.OutputPins[0].State.SetShort((data>>8) & mask);
					chip.OutputPins[1].State.SetShort(data & mask);
			
                    break;
				}

				case ChipType.EEPROM_256x16:
				{
                    const uint mask = 0x00ff;
					uint address = chip.InputPins[0].State.GetShortValues();
                    bool isWriting = chip.InputPins[3].State.SmallHigh();
                    bool clockHigh = chip.InputPins[4].State.SmallHigh();
                    bool isRisingEdge = clockHigh && chip.InternalState[^1] == 0;
                    chip.InternalState[^1] = clockHigh ? 1u : 0;


					if (isWriting && isRisingEdge)
					{
							uint writeData = (ushort)(((chip.InputPins[1].State.GetShortValues() & mask) << 8) | (chip.InputPins[2].State.GetShortValues() & mask));
						chip.InternalState[address] = writeData;
						Project.ActiveProject.NotifyRomContentsEditedRuntime(chip);
					}
          
            		uint data = chip.InternalState[address];
            		chip.OutputPins[0].State.SetShort((data >> 8) & mask);
					chip.OutputPins[1]. State.SetShort(data & mask);
            		break;
                }

				case ChipType.Buzzer:
				{
					int freqIndex = (int)chip.InputPins[0].State.GetShortValues();
					uint volumeIndex = chip.InputPins[1].State.GetShortValues();
					audioState.RegisterNote(freqIndex, volumeIndex);
					break;
				}
				case ChipType.SPS:
				{
					double tps = Project.ActiveProject.simAvgTicksPerSec;
					ushort sps = (ushort)tps;
					ushort spc = (ushort)stepsPerClockTransition;

					chip.OutputPins[3].State.SmallSet((uint)(tps >= 65536 ? 1 : 0));
					chip.OutputPins[2].State.SmallSet((uint)(stepsPerClockTransition > 65535 ? 1 : 0));
					chip.OutputPins[1].State.SetShort(sps);
					chip.OutputPins[0].State.SetShort(spc);
					break;
				}
				case ChipType.RTC:
				{
					uint unixTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
					chip.OutputPins[0].State.SetMedium(unixTime, 0);
					break;
				}
				case ChipType.Constant_8Bit:
				{
					chip.OutputPins[0].State.SetShort((ushort)chip.InternalState[0]);
					break;
				}

				case ChipType.Split_Pin:
				{ 
					chip.InputPins[0].State.HandleSplit(ref chip.OutputPins);
						break;
				}
				case ChipType.Detector:
				{
					uint state = chip.InputPins[0].State.GetSmallTristatedValue();
					chip.OutputPins[0].State.SmallSet(1 << 16);
                    chip.OutputPins[1].State.SmallSet(1 << 16);
					chip.OutputPins[2].State.SmallSet(1 << 16);
					chip.OutputPins[state].State.SmallSet(1);
					break;
				}

				case ChipType.Merge_Pin:
				{	
					chip.OutputPins[0].State.HandleMerge(chip.InputPins);
					break;
				}

				// ---- Bus types ----
				default:
				{
					if (ChipTypeHelper.IsBusOriginType(chip.ChipType))
					{
						SimPin inputPin = chip.InputPins[0];
						chip.OutputPins[0].State.CopyFrom(inputPin.State);
					}

					break;
				}
			}
		}

		public static SimChip BuildSimChip(ChipDescription chipDesc, ChipLibrary library)
		{
			return BuildSimChip(chipDesc, library, -1, null);
		}

		public static SimChip BuildSimChip(ChipDescription chipDesc, ChipLibrary library, int subChipID, uint[] internalState)
		{
			SimChip simChip = BuildSimChipRecursive(chipDesc, library, subChipID, internalState);
			return simChip;
		}

		// Recursively build full representation of chip from its description for simulation.
		static SimChip BuildSimChipRecursive(ChipDescription chipDesc, ChipLibrary library, int subChipID, uint[] internalState)
		{
			// Recursively create subchips
			SimChip[] subchips = chipDesc.SubChips.Length == 0 ? Array.Empty<SimChip>() : new SimChip[chipDesc.SubChips.Length];

			for (int i = 0; i < chipDesc.SubChips.Length; i++)
			{
				SubChipDescription subchipDesc = chipDesc.SubChips[i];
				ChipDescription subchipFullDesc = library.GetChipDescription(subchipDesc.Name);
				SimChip subChip = BuildSimChipRecursive(subchipFullDesc, library, subchipDesc.ID, subchipDesc.InternalData);
				subchips[i] = subChip;
			}

			SimChip simChip = new(chipDesc, subChipID, internalState, subchips);


			// Create connections
			for (int i = 0; i < chipDesc.Wires.Length; i++)
			{
				simChip.AddConnection(chipDesc.Wires[i].SourcePinAddress, chipDesc.Wires[i].TargetPinAddress);
			}

			return simChip;
		}

		public static void AddPin(SimChip simChip, int pinID, bool isInputPin, PinBitCount pinBitCount)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.AddPin,
				modifyTarget = simChip,
				simPinToAdd = new SimPin(pinID, isInputPin, simChip, pinBitCount),
				pinIsInputPin = isInputPin
			};
			modificationQueue.Enqueue(command);
		}

		public static void RemovePin(SimChip simChip, int pinID)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.RemovePin,
				modifyTarget = simChip,
				removePinID = pinID
			};
			modificationQueue.Enqueue(command);
		}

		public static void AddSubChip(SimChip simChip, ChipDescription desc, ChipLibrary chipLibrary, int subChipID, uint[] subChipInternalData)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.AddSubchip,
				modifyTarget = simChip,
				chipDesc = desc,
				lib = chipLibrary,
				subChipID = subChipID,
				subChipInternalData = subChipInternalData
			};
			modificationQueue.Enqueue(command);
		}

		public static void AddConnection(SimChip simChip, PinAddress source, PinAddress target)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.AddConnection,
				modifyTarget = simChip,
				sourcePinAddress = source,
				targetPinAddress = target
			};
			modificationQueue.Enqueue(command);
		}

		public static void RemoveConnection(SimChip simChip, PinAddress source, PinAddress target)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.RemoveConnection,
				modifyTarget = simChip,
				sourcePinAddress = source,
				targetPinAddress = target
			};
			modificationQueue.Enqueue(command);
		}

		public static void RemoveSubChip(SimChip simChip, int id)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.RemoveSubChip,
				modifyTarget = simChip,
				removeSubChipID = id
			};
			modificationQueue.Enqueue(command);
		}

		// Note: this should only be called from the sim thread
		public static void ApplyModifications()
		{
			while (modificationQueue.Count > 0)
			{
				needsOrderPass = true;

				if (modificationQueue.TryDequeue(out SimModifyCommand cmd))
				{
					if (cmd.type == SimModifyCommand.ModificationType.AddSubchip)
					{
						SimChip newSubChip = BuildSimChip(cmd.chipDesc, cmd.lib, cmd.subChipID, cmd.subChipInternalData);
						cmd.modifyTarget.AddSubChip(newSubChip);
					}
					else if (cmd.type == SimModifyCommand.ModificationType.RemoveSubChip)
					{
						cmd.modifyTarget.RemoveSubChip(cmd.removeSubChipID);
					}
					else if (cmd.type == SimModifyCommand.ModificationType.AddConnection)
					{
						cmd.modifyTarget.AddConnection(cmd.sourcePinAddress, cmd.targetPinAddress);
					}
					else if (cmd.type == SimModifyCommand.ModificationType.RemoveConnection)
					{
						cmd.modifyTarget.RemoveConnection(cmd.sourcePinAddress, cmd.targetPinAddress); //
					}
					else if (cmd.type == SimModifyCommand.ModificationType.AddPin)
					{
						cmd.modifyTarget.AddPin(cmd.simPinToAdd, cmd.pinIsInputPin);
					}
					else if (cmd.type == SimModifyCommand.ModificationType.RemovePin)
					{
						cmd.modifyTarget.RemovePin(cmd.removePinID);
					}
				}
			}
		}

		public static void Reset()
		{
			simulationFrame = 0;
			modificationQueue?.Clear();
			stopwatch.Restart();
			elapsedSecondsOld = 0;
		}

		struct SimModifyCommand
		{
			public enum ModificationType
			{
				AddSubchip,
				RemoveSubChip,
				AddConnection,
				RemoveConnection,
				AddPin,
				RemovePin
			}

			public ModificationType type;
			public SimChip modifyTarget;
			public ChipDescription chipDesc;
			public ChipLibrary lib;
			public int subChipID;
			public uint[] subChipInternalData;
			public PinAddress sourcePinAddress;
			public PinAddress targetPinAddress;
			public SimPin simPinToAdd;
			public bool pinIsInputPin;
			public int removePinID;
			public int removeSubChipID;
		}
	}
}
