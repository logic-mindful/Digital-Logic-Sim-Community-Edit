using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;

namespace DLS.Simulation
{
	// Helper class for dealing with pin state.
	// Pin state is stored as a uint32, with format:
	// Tristate flags (most significant 16 bits) | Bit states (least significant 16 bits)
	public static class BitArrayHelper
	{
        public static uint[] PreComputedUINTMasks = {
            0b0000_0000_0000_0000__0000_0000_0000_0000,

            0b0000_0000_0000_0001__0000_0000_0000_0001,
            0b0000_0000_0000_0011__0000_0000_0000_0011,
            0b0000_0000_0000_0111__0000_0000_0000_0111,
            0b0000_0000_0000_1111__0000_0000_0000_1111,

            0b0000_0000_0001_1111__0000_0000_0001_1111,
            0b0000_0000_0011_1111__0000_0000_0011_1111,
            0b0000_0000_0111_1111__0000_0000_0111_1111,
            0b0000_0000_1111_1111__0000_0000_1111_1111,

            0b0000_0001_1111_1111__0000_0001_1111_1111,
            0b0000_0011_1111_1111__0000_0011_1111_1111,
            0b0000_0111_1111_1111__0000_0111_1111_1111,
            0b0000_1111_1111_1111__0000_1111_1111_1111,

            0b0001_1111_1111_1111__0001_1111_1111_1111,
            0b0011_1111_1111_1111__0011_1111_1111_1111,
            0b0111_1111_1111_1111__0111_1111_1111_1111,
            0b1111_1111_1111_1111__1111_1111_1111_1111,
        };

        public static ushort[] PreComputedSHORTMasks = {
            0000_0000_0000_0000,

            0b0000_0000_0000_0001,
            0b0000_0000_0000_0011,
            0b0000_0000_0000_0111,
            0b0000_0000_0000_1111,

            0b0000_0000_0001_1111,
            0b0000_0000_0011_1111,
            0b0000_0000_0111_1111,
            0b0000_0000_1111_1111,

            0b0000_0001_1111_1111,
            0b0000_0011_1111_1111,
            0b0000_0111_1111_1111,
            0b0000_1111_1111_1111,

            0b0001_1111_1111_1111,
            0b0011_1111_1111_1111,
            0b0111_1111_1111_1111,
            0b1111_1111_1111_1111,
        };

        public static int[] PreComputedINTMasks = {
            0000_0000_0000_0000,

            0b0000_0000_0000_0001,
            0b0000_0000_0000_0011,
            0b0000_0000_0000_0111,
            0b0000_0000_0000_1111,

            0b0000_0000_0001_1111,
            0b0000_0000_0011_1111,
            0b0000_0000_0111_1111,
            0b0000_0000_1111_1111,

            0b0000_0001_1111_1111,
            0b0000_0011_1111_1111,
            0b0000_0111_1111_1111,
            0b0000_1111_1111_1111,

            0b0001_1111_1111_1111,
            0b0011_1111_1111_1111,
            0b0111_1111_1111_1111,
            0b1111_1111_1111_1111,
        };


        public static BitArray NonMutativeOR(BitArray A, BitArray B)
        {
            BitArray temp = new BitArray(A);
            temp.Or(B);
            return temp;
        }
        
        public static BitArray NonMutativeAND(BitArray A, BitArray B)
        {
            BitArray temp = new BitArray(A);
            temp.And(B);
            return temp;
        }

        public static BitArray NonMutativeNOT(BitArray A)
        {
            BitArray temp = new BitArray(A);
            temp.Not();
            return temp;
        }

        public static BitArray TrueBitArray(int length)
        {
            BitArray array = new BitArray(length);
            array.SetAll(true);
            return array;
        }

        public static ushort GetUShortAtIndexOfMaxLength(BitArray state, int index, int maxLength)
        {
            int len = Mathf.Min(maxLength + index, state.Count, 16 + index);
            ushort n = 0;
            for (int i = index; i < len ; i++)
            {
                if (state.Get(i))
                    n |= (ushort)(1 << (i-index));
            }
            return n;
        }

        public static uint GetUIntAtIndexOfMaxLength(BitArray state, int index, int maxLength)
        {
            int len = Mathf.Min(maxLength + index, state.Count, 32 + index);
            uint n = 0;
            for (int i = index; i < len; i++)
            {
                if (state.Get(i))
                    n |= (uint)(1 << (i - index));
            }
            return n;
        }


        public static uint GetFirstUIntFromByteArray(BitArray state)
        {
            int len = Math.Min(32, state.Count);
            uint n = 0;
            for (byte i = 0; i < len; i++)
            {
                if (state.Get(i))
                    n |= (uint)(1 << i);
            }
            return n;
        }

        public static BitArray GetBitArrayOfMaxLengthStartingAtIndex(BitArray state, int index, int length)
        {
            BitArray bitArray = new BitArray(length);
            int len = Mathf.Min(length + index, state.Length);
            for(int i = index;i < len;i++)
            {
                bitArray[i - index] = state[i];
            }
            return bitArray;
        }

        public static void SetNBitsAtIndex(ref BitArray state, BitArray source, int index, int length)
        {
            int len = Mathf.Min(index + length, state.Length, index + source.Length);
            for (int i = index; i < len; i++)
            {
                state.Set(i, source[i-index]);
            }
        }

        public static void SetUShortOfMaxLengthAtIndex(ref BitArray state, ushort value, int index, int length)
        {
            int len = Mathf.Min(index + length, state.Length, index + 16);
            for(int i = index; i < len; i++)
            {
                state.Set(i, ((value >> (i-index)) & 1) == 1);
            }
        }

        public static void SetUIntOfMaxLengthAtIndex(ref BitArray state, uint value, int index, int length)
        {
            int len = Mathf.Min(index + length, state.Length, index + 32);
            for (int i = index; i < len; i++)
            {
                state.Set(i, ((value >> (i - index)) & 1) == 1);
            }
        }

	}
}