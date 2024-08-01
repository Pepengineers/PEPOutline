using System;
using UnityEngine;

namespace Pepengineers.PEPOutline.CustomDepth.Data
{
    [Serializable]
    public enum CustomStencilBit : byte
    {
        First = 0,
        Second = 1,
        Third = 2,
        Four = 3,
        Five = 4,
        Six = 5,
        Seven = 6,
        Eight = 7,

        [HideInInspector] Count = 8
    }

    [Serializable]
    public enum CustomStencilBitValue : sbyte
    {
        NotSet = -1,
        Zero = 0,
        One = 1
    }

    public static class CustomStencilBitExtension
    {
        public static bool HasBit(this uint value, CustomStencilBit bit)
        {
            return ((value >> (byte)bit) & 1) > 0;
        }

        public static bool HasBit(this int value, CustomStencilBit bit)
        {
            return ((value >> (byte)bit) & 1) > 0;
        }

        public static bool HasBit(this byte value, CustomStencilBit bit)
        {
            return ((value >> (byte)bit) & 1) > 0;
        }

        public static byte SetBit(this byte value, CustomStencilBit stencilBit)
        {
            return (byte)(value | (1 << (byte)stencilBit));
        }

        public static byte SetBit(this byte value, CustomStencilBit stencilBit, CustomStencilBitValue stencilBitValue)
        {
            return (byte)((value & ~(1 << (byte)stencilBit)) | ((byte)(stencilBitValue == CustomStencilBitValue.One ? 1 : 0) << (byte)stencilBit));
        }

        public static byte ToggleBit(this byte value, CustomStencilBit stencilBit)
        {
            return (byte)(value ^ (1 << (byte)stencilBit));
        }

        public static byte ClearBit(this byte value, CustomStencilBit stencilBit)
        {
            return (byte)(value & ~(1 << (byte)stencilBit));
        }
    }
}