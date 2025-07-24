using System;
using DLS.Description;
using DLS.Simulation;
using Seb.Helpers;
using Seb.Types;
using UnityEngine;
using static DLS.Graphics.DrawSettings;

namespace DLS.Game
{
	public class DevPinInstance : IMoveable
	{
		public PinBitCount BitCount;
		public readonly char[] decimalDisplayCharBuffer = new char[16];

		// Size/Layout info
		public readonly Vector2 faceDir;

		public readonly bool IsInputPin;
		public readonly string Name;
		public readonly PinInstance Pin;
		public Vector2Int StateGridDimensions;
		public Vector2 StateGridSize;

		public PinValueDisplayMode pinValueDisplayMode;

		public DevPinInstance(PinDescription pinDescription, bool isInput)
		{
			Name = pinDescription.Name;
			ID = pinDescription.ID;
			IsInputPin = isInput;
			Position = pinDescription.Position;
			BitCount = pinDescription.BitCount;

			Pin = new PinInstance(pinDescription, new PinAddress(ID, 0), this, isInput);
			pinValueDisplayMode = pinDescription.ValueDisplayMode;

			// Calculate layout info
			faceDir = new Vector2(IsInputPin ? 1 : -1, 0);
			StateGridDimensions = GridHelper.GetStateGridDimension(BitCount.BitCount);
			StateGridSize = BitCount.BitCount == 1 ? Vector2.one * (DevPinStateDisplayRadius * 2 + DevPinStateDisplayOutline * 2) : (Vector2)StateGridDimensions * MultiBitPinStateDisplaySquareSize + Vector2.one * DevPinStateDisplayOutline;
		
		}

		public Vector2 HandlePosition => Position;
		public Vector2 StateDisplayPosition => HandlePosition + faceDir * (DevPinHandleWidth / 2 + StateGridSize.x / 2 + 0.065f);

		public Vector2 PinPosition
		{
			get	
			{
				if(BitCount.BitCount is 1 or 4 or 8)
				{
                    return HandlePosition + faceDir * (GridSize * (BitCount.BitCount is 1 or 4 ? 6 : 9));
                }

				return StateDisplayPosition + faceDir * (StateGridSize.x / 2 + 2 * GridSize);
			}
		}


		public Vector2 Position { get; set; }
		public Vector2 MoveStartPosition { get; set; }
		public Vector2 StraightLineReferencePoint { get; set; }
		public int ID { get; }

		public bool IsSelected { get; set; }
		public bool HasReferencePointForStraightLineMovement { get; set; }
		public bool IsValidMovePos { get; set; }

		public Bounds2D SelectionBoundingBox => CreateBoundingBox(SelectionBoundsPadding);

		public Bounds2D BoundingBox => CreateBoundingBox(0);


		public Vector2 SnapPoint => Pin.GetWorldPos();

		public bool ShouldBeIncludedInSelectionBox(Vector2 selectionCentre, Vector2 selectionSize)
		{
			Bounds2D selfBounds = SelectionBoundingBox;
			return Maths.BoxesOverlap(selectionCentre, selectionSize, selfBounds.Centre, selfBounds.Size);
		}

		public int GetStateDecimalDisplayValue()
		{
			uint rawValue = Pin.State.GetValue();
			int displayValue = (int)rawValue;

			if (pinValueDisplayMode == PinValueDisplayMode.SignedDecimal)
			{
				displayValue = Maths.TwosComplement(rawValue, Math.Min((int)BitCount,32));
			}

			return displayValue;
		}

		Bounds2D CreateBoundingBox(float pad)
		{
			float x1 = HandlePosition.x - faceDir.x * DevPinHandleWidth / 2;
			float x2 = PinPosition.x + faceDir.x * PinRadius;
			float minX = Mathf.Min(x1, x2);
			float maxX = Mathf.Max(x1, x2);

			Vector2 centre = new((minX + maxX) / 2, HandlePosition.y);
			Vector2 size = new Vector2(maxX - minX, BoundsHeight()) + Vector2.one * pad;
			return Bounds2D.CreateFromCentreAndSize(centre, size);
		}

		public Bounds2D HandleBounds() => Bounds2D.CreateFromCentreAndSize(HandlePosition, GetHandleSize());

		public float BoundsHeight() => StateGridSize.y;

		public Vector2 GetHandleSize() => new(DevPinHandleWidth, BoundsHeight());

		public void ToggleState(int bitIndex)
		{
			Pin.PlayerInputState.ToggleBit(bitIndex);
		}

		public bool PointIsInInteractionBounds(Vector2 point) => PointIsInHandleBounds(point) || PointIsInStateIndicatorBounds(point);

		public bool PointIsInStateIndicatorBounds(Vector2 point) => Maths.PointInCircle2D(point, StateDisplayPosition, DevPinStateDisplayRadius);

		public bool PointIsInHandleBounds(Vector2 point) => HandleBounds().PointInBounds(point);

		public void ChangeBitCount(ushort bitcount)
		{
			BitCount.BitCount = bitcount;
            StateGridDimensions = GridHelper.GetStateGridDimension(BitCount.BitCount);
            StateGridSize = BitCount.BitCount == 1 ? Vector2.one * (DevPinStateDisplayRadius * 2 + DevPinStateDisplayOutline * 2) : (Vector2)StateGridDimensions * MultiBitPinStateDisplaySquareSize + Vector2.one * DevPinStateDisplayOutline;
        }
    }
}