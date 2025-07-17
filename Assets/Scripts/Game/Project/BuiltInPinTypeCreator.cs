using System;
using System.Collections.Generic;
using DLS.Description;
using UnityEngine;
using static DLS.Graphics.DrawSettings;

namespace DLS.Game
{
    public static class BuiltinPinTypeCreator
    {
        public static PinBitCount[] CreateBuiltInPinType()
        {
            return new PinBitCount[]
            {
                1,  4,  8
            };
        }        
    }
}