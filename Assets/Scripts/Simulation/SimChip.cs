using System;
using System.Collections.Generic;
using System.Linq;
using DLS.Description;

namespace DLS.Simulation
{
	public class SimChip
	{
		public readonly ChipType ChipType;
		public readonly int ID;
		public readonly string Name;

		// Some builtin chips, such as RAM, require an internal state for memory
		// (can also be used for other arbitrary chip-specific data)
		public readonly uint[] InternalState = Array.Empty<uint>();
		public readonly bool IsBuiltin;
		public SimPin[] InputPins = Array.Empty<SimPin>();
		public int numConnectedInputs;
		public bool shouldBeCached; // True, if the user specifically wanted this chip to be cached
		public bool isCached; // True for cached chips, whose outputs get determined by a LUT

		public int numInputsReady;
		public SimPin[] OutputPins = Array.Empty<SimPin>();
		public SimChip[] SubChips = Array.Empty<SimChip>();


		public SimChip()
		{
			ID = -1;
		}

		public SimChip(ChipDescription desc, int id, uint[] internalState, SimChip[] subChips)
		{
			SubChips = subChips;
			ID = id;
			Name = desc.Name;
			ChipType = desc.ChipType;
			IsBuiltin = ChipType != ChipType.Custom;

			// ---- Create pins (don't allocate unnecessarily as very many sim chips maybe created!) ----
			if (desc.InputPins.Length > 0)
			{
				InputPins = new SimPin [desc.InputPins.Length];
				for (int i = 0; i < InputPins.Length; i++)
				{
					InputPins[i] = CreateSimPinFromDescription(desc.InputPins[i], true, this);
				}
			}

			if (desc.OutputPins.Length > 0)
			{
				OutputPins = new SimPin [desc.OutputPins.Length];
				for (int i = 0; i < OutputPins.Length; i++)
				{
					OutputPins[i] = CreateSimPinFromDescription(desc.OutputPins[i], false, this);
				}
			}

			// ---- Initialize internal state ----
			const int addressSize_8Bit = 256;

			if (ChipType is ChipType.DisplayRGB)
			{
				// first 256 bits = display buffer, next 256 bits = back buffer, last bit = clock state (to allow edge-trigger behaviour)
				InternalState = new uint[addressSize_8Bit * 2 + 1];
			}
			else if (ChipType is ChipType.DisplayDot)
			{
				// first 256 bits = display buffer, next 256 bits = back buffer, last bit = clock state (to allow edge-trigger behaviour)
				InternalState = new uint[addressSize_8Bit * 2 + 1];
			}
			else if (ChipType is ChipType.dev_Ram_8Bit)
			{
				InternalState = new uint[addressSize_8Bit + 1]; // +1 for clock state (to allow edge-trigger behaviour)

				// Initialize memory contents to random state
				Span<byte> randomBytes = stackalloc byte[4];
				for (int i = 0; i < InternalState.Length - 1; i++)
				{
					Simulator.rng.NextBytes(randomBytes);
					InternalState[i] = BitConverter.ToUInt32(randomBytes);
				}
			}
			// Load in serialized persistent state (rom data, etc.)
			else if (internalState is { Length: > 0 })
			{
				InternalState = new uint[internalState.Length];
				UpdateInternalState(internalState);
			}
		}

		// Returns true, when this chip is purely combinational / stateless. This is the case, when the outputs of this chip depend entirely on the inputs and on nothing else.
		public bool IsCombinational()
		{
			// Handle built in chips
			switch (ChipType)
			{
				case ChipType.Nand:
				case ChipType.TriStateBuffer:
				case ChipType.Merge_1To4Bit:
				case ChipType.Merge_1To8Bit:
				case ChipType.Merge_4To8Bit:
				case ChipType.Split_4To1Bit:
				case ChipType.Split_8To4Bit:
				case ChipType.Split_8To1Bit:
					return true;
				case ChipType.Clock:
				case ChipType.Pulse:
				case ChipType.dev_Ram_8Bit:
				case ChipType.Rom_256x16:
				case ChipType.SevenSegmentDisplay:
				case ChipType.DisplayRGB:
				case ChipType.DisplayDot:
				case ChipType.DisplayLED:
				case ChipType.Key:
				case ChipType.Buzzer:
					return false;
			}

			// Chip isn't combinational, if any of the inputPins has more than one connection
			foreach (SimPin inputPin in InputPins)
			{
				if (inputPin.numInputConnections > 1) return false;
			}

			// Can only be combinational if all subchips are combinational
			foreach (SimChip subChip in SubChips)
			{
				// recursively make sure, that subchip is combinational
				if (!subChip.IsCombinational()) return false;
			}

			// Check for loops in wiring using DFS
			HashSet<int> visited = new();
			HashSet<int> recStack = new();

			bool Dfs(SimChip chip)
			{
				if (!visited.Add(chip.ID))
					return false;

				recStack.Add(chip.ID);

				foreach (SimPin output in chip.OutputPins)
				{
					foreach (SimPin connectedPin in output.ConnectedTargetPins)
					{
						SimChip neighbor = connectedPin.parentChip;
						if (neighbor == null || neighbor == chip) continue;

						if (!visited.Contains(neighbor.ID))
						{
							if (Dfs(neighbor))
								return true;
						}
						else if (recStack.Contains(neighbor.ID))
						{
							// Cycle detected
							return true;
						}
					}
				}

				recStack.Remove(chip.ID);
				return false;
			}

			// Run DFS from all subchips, in case some are disconnected
			foreach (SimChip subChip in SubChips)
			{
				if (!visited.Contains(subChip.ID))
				{
					if (Dfs(subChip))
						return false; // Cycle found
				}
			}

			return true;
		}

		public int CalculateNumberOfInputBits()
		{
			int numberOfBits = 0;
			foreach (SimPin pin in InputPins)
			{
				numberOfBits += (int)pin.numberOfBits;
			}
			return numberOfBits;
		}

		public void ResetReceivedFlagsOnAllPins()
		{
			foreach(SimPin pin in InputPins)
			{
				pin.numInputsReceivedThisFrame = 0;
			}
			foreach (SimPin pin in OutputPins)
			{
				pin.numInputsReceivedThisFrame = 0;
			}
			foreach (SimChip subChip in SubChips)
			{
				subChip.ResetReceivedFlagsOnAllPins();
			}
		}

		public void UpdateInternalState(uint[] source) => Array.Copy(source, InternalState, InternalState.Length);


		public void Sim_PropagateInputs()
		{
			int length = InputPins.Length;

			for (int i = 0; i < length; i++)
			{
				InputPins[i].PropagateSignal();
			}
		}

		public void Sim_PropagateOutputs()
		{
			int length = OutputPins.Length;

			for (int i = 0; i < length; i++)
			{
				OutputPins[i].PropagateSignal();
			}

			numInputsReady = 0; // Reset for next frame
		}

		public bool Sim_IsReady() => numInputsReady == numConnectedInputs;
		
		public (bool success, SimChip chip) TryGetSubChipFromID(int id)
		{
			// Todo: address possible errors if accessing from main thread while being modified on sim thread?
			foreach (SimChip s in SubChips)
			{
				if (s?.ID == id)
				{
					return (true, s);
				}
			}

			return (false, null);
		}

		public SimChip GetSubChipFromID(int id)
		{
			(bool success, SimChip chip) = TryGetSubChipFromID(id);
			if (success) return chip;

			throw new Exception("Failed to find subchip with id " + id);
		}

		public (SimPin pin, SimChip chip) GetSimPinFromAddressWithChip(PinAddress address)
		{
			foreach (SimChip s in SubChips)
			{
				if (s.ID == address.PinOwnerID)
				{
					foreach (SimPin pin in s.InputPins)
					{
						if (pin.ID == address.PinID) return (pin, s);
					}

					foreach (SimPin pin in s.OutputPins)
					{
						if (pin.ID == address.PinID) return (pin, s);
					}
				}
			}

			foreach (SimPin pin in InputPins)
			{
				if (pin.ID == address.PinOwnerID) return (pin, null);
			}

			foreach (SimPin pin in OutputPins)
			{
				if (pin.ID == address.PinOwnerID) return (pin, null);
			}

			throw new Exception("Failed to find pin with address: " + address.PinID + ", " + address.PinOwnerID);
		}

		public SimPin GetSimPinFromAddress(PinAddress address)
		{
			// Todo: address possible errors if accessing from main thread while being modified on sim thread?
			foreach (SimChip s in SubChips)
			{
				if (s.ID == address.PinOwnerID)
				{
					foreach (SimPin pin in s.InputPins)
					{
						if (pin.ID == address.PinID) return pin;
					}

					foreach (SimPin pin in s.OutputPins)
					{
						if (pin.ID == address.PinID) return pin;
					}
				}
			}

			foreach (SimPin pin in InputPins)
			{
				if (pin.ID == address.PinOwnerID) return pin;
			}

			foreach (SimPin pin in OutputPins)
			{
				if (pin.ID == address.PinOwnerID) return pin;
			}

			throw new Exception("Failed to find pin with address: " + address.PinID + ", " + address.PinOwnerID);
		}


		public void RemoveSubChip(int id)
		{
			SubChips = SubChips.Where(s => s.ID != id).ToArray();
		}


		public void AddPin(SimPin pin, bool isInput)
		{
			if (isInput)
			{
				Array.Resize(ref InputPins, InputPins.Length + 1);
				InputPins[^1] = pin;
			}
			else
			{
				Array.Resize(ref OutputPins, OutputPins.Length + 1);
				OutputPins[^1] = pin;
			}
		}

		static SimPin CreateSimPinFromDescription(PinDescription desc, bool isInput, SimChip parent) => new(desc.ID, isInput, desc.BitCount, parent);

		public void RemovePin(int removePinID)
		{
			InputPins = InputPins.Where(p => p.ID != removePinID).ToArray();
			OutputPins = OutputPins.Where(p => p.ID != removePinID).ToArray();
		}

		public void AddSubChip(SimChip subChip)
		{
			Array.Resize(ref SubChips, SubChips.Length + 1);
			SubChips[^1] = subChip;
		}

		public void AddConnection(PinAddress sourcePinAddress, PinAddress targetPinAddress)
		{
			try
			{
				SimPin sourcePin = GetSimPinFromAddress(sourcePinAddress);
				(SimPin targetPin, SimChip targetChip) = GetSimPinFromAddressWithChip(targetPinAddress);


				Array.Resize(ref sourcePin.ConnectedTargetPins, sourcePin.ConnectedTargetPins.Length + 1);
				sourcePin.ConnectedTargetPins[^1] = targetPin;
				targetPin.numInputConnections++;
				if (targetPin.numInputConnections == 1 && targetChip != null) targetChip.numConnectedInputs++;
			}
			catch (Exception)
			{
				// Can fail to find pin if player has edited an existing chip to remove the pin, and then a chip is opened which uses the old version of that modified chip.
				// In that case we just ignore the failure and no connection is made.
			}
		}

		public void RemoveConnection(PinAddress sourcePinAddress, PinAddress targetPinAddress)
		{
			SimPin sourcePin = GetSimPinFromAddress(sourcePinAddress);
			(SimPin removeTargetPin, SimChip targetChip) = GetSimPinFromAddressWithChip(targetPinAddress);

			// Remove first matching connection
			for (int i = 0; i < sourcePin.ConnectedTargetPins.Length; i++)
			{
				if (sourcePin.ConnectedTargetPins[i] == removeTargetPin)
				{
					SimPin[] newArray = new SimPin[sourcePin.ConnectedTargetPins.Length - 1];
					Array.Copy(sourcePin.ConnectedTargetPins, 0, newArray, 0, i);
					Array.Copy(sourcePin.ConnectedTargetPins, i + 1, newArray, i, sourcePin.ConnectedTargetPins.Length - i - 1);
					sourcePin.ConnectedTargetPins = newArray;

					removeTargetPin.numInputConnections -= 1;
					if (removeTargetPin.numInputConnections == 0)
					{
						PinState.SetAllDisconnected(ref removeTargetPin.State);
						removeTargetPin.latestSourceID = -1;
						removeTargetPin.latestSourceParentChipID = -1;
						if (targetChip != null) removeTargetPin.parentChip.numConnectedInputs--;
					}

					break;
				}
			}
		}
	}
}