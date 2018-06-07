using System;

namespace Mandelmesh
{
    public struct Vector
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Vector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector operator +(Vector l, Vector r) => new Vector(l.X + r.X, l.Y + r.Y, l.Z + r.Z);
        public static Vector operator -(Vector l, Vector r) => new Vector(l.X - r.X, l.Y - r.Y, l.Z - r.Z);
        public static Vector operator -(Vector v) => new Vector(-v.X, -v.Y, -v.Z);
        public static Vector operator *(Vector l, Vector r) => new Vector(l.X * r.X, l.Y * r.Y, l.Z * r.Z);
        public static Vector operator *(Vector l, double r) => new Vector(l.X * r, l.Y * r, l.Z * r);
        public static Vector operator /(Vector l, double r) => new Vector(l.X / r, l.Y / r, l.Z / r);
        public static Vector operator /(Vector l, Vector r) => new Vector(l.X / r.X, l.Y / r.Y, l.Z / r.Z);
        public static double Dot(Vector l, Vector r) => l.X * r.X + l.Y * r.Y + l.Z * r.Z;
        public double Length2() => X * X + Y * Y + Z * Z;
        public double Length() => Math.Sqrt(Length2());
        public Vector Normalized() => this / Length();
        public Vector Clamp(double min, double max) => new Vector(X.Clamp(min, max), Y.Clamp(min, max), Z.Clamp(min, max));
    }

    public static class Ext
    {
        public static double Clamp(this double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }
            else if (value > max)
            {
                return max;
            }
            else
            {
                return value;
            }
        }
    }
}
