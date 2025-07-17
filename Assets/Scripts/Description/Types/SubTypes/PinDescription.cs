using System;
using System.Collections;
using UnityEngine;

namespace DLS.Description
{
	public struct PinDescription
	{
		public string Name;
		public int ID;
		public Vector2 Position;
		public PinBitCount BitCount;
		public PinColour Colour;
		public PinValueDisplayMode ValueDisplayMode;
        public int face; // Which edge of the chip the pin is on: 0 = top, 1 = right, 2 = bottom, 3 = left
        public float LocalOffset; //offset on chip edge for pin location
		

        public PinDescription(string name, int id, Vector2 position, PinBitCount bitCount, PinColour colour, PinValueDisplayMode valueDisplayMode, float localoff = 0)
		{
			Name = name;
			ID = id;
			Position = position;
			BitCount = bitCount;
			Colour = colour;
			ValueDisplayMode = valueDisplayMode;
            LocalOffset = localoff;
			face = 1;
        }
	}

	public struct PinBitCount
	{
		public const int Bit1 = 1;
		public const int Bit4 = 4;
		public const int Bit8 = 8;
		public const int Bit16 = 16;
		public const int Bit32 = 32;

		public int BitCount;

		public override string ToString()
		{
			return BitCount.ToString();
		}
		public PinBitCount(ushort BitCount = 1)
		{
			this.BitCount = BitCount;
		}
		public readonly BitArray GetEmptyBitArray()
		{
			return new BitArray(length: BitCount<<1);
		}

        public override bool Equals(object @object)
        {
			if(@object is uint number)
			{
				return BitCount == number;
			}
            return base.Equals(@object);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BitCount);
        }
		
		public int GetTier()
		{
			if (BitCount <= 64) return 0;
			else if (BitCount <= 512) return 1;
			else if (BitCount <= 4096) return 2;
			else return 3;
		}

        public static bool operator ==(PinBitCount a, PinBitCount b) => a.BitCount == b.BitCount;
        public static bool operator !=(PinBitCount a, PinBitCount b) => a.BitCount != b.BitCount;

        public static bool operator ==(uint number, PinBitCount b) => number == b.BitCount;
        public static bool operator !=(uint number, PinBitCount b) => number != b.BitCount;
        public static bool operator ==(PinBitCount a, uint number) => number == a.BitCount;
        public static bool operator !=(PinBitCount a, uint number) => number != a.BitCount;

        public static bool operator ==(PinBitCount a, int number) => number == a.BitCount;
        public static bool operator !=(PinBitCount a, int number) => number != a.BitCount;


        public static implicit operator uint(PinBitCount b) => (uint)b.BitCount;
        public static implicit operator int(PinBitCount b) => b.BitCount;
		public static implicit operator ushort(PinBitCount b) => (ushort)b.BitCount;
		public static implicit operator PinBitCount(long b) => new PinBitCount((ushort)b);


        public static explicit operator PinBitCount(ushort number) => new PinBitCount(number);
		public static explicit operator PinBitCount(int number) => new PinBitCount((ushort)number);

    } 

    public enum PinColour
	{
		Red,
		Orange,
		Yellow,
		Green,
		Blue,
		Violet,
		Pink,
		White
	}

	public enum PinValueDisplayMode
	{
		Off,
		UnsignedDecimal,
		SignedDecimal,
		HEX
	}
}