using System.Numerics;
using UnityEngine;

namespace Seb.Helpers
{

    public static class TypeHelper
    {
        public static System.Numerics.Vector2 ToNumerics(this UnityEngine.Vector2 value)
        {
            return new System.Numerics.Vector2(value.x, value.y);
        }

        public static System.Numerics.Vector2 ToUnity(this System.Numerics.Vector2 value)
        {
            return new System.Numerics.Vector2(value.X, value.Y);
        }

    }

}