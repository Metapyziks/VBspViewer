using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VBspViewer.Importing.VBsp.Structures
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector : IEquatable<Vector>
    {
        public static Vector operator -(Vector vector)
        {
            return new Vector(-vector.X, -vector.Y, -vector.Z);
        }

        public static implicit operator Vector3(Vector vector)
        {
            return new Vector3(vector.X, vector.Z, vector.Y);
        }

        public float X;
        public float Y;
        public float Z;

        public Vector(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(Vector other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector && Equals((Vector) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                hashCode = (hashCode * 397) ^ Z.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.Format("{0:F2}, {1:F2}, {2:F2}", X, Y, Z);
        }
    }
}
