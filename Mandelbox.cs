using System;

namespace Mandelmesh
{
    public static class Mandelbox
    {
        const double _folding_limit = 1.0;
        const double _fixed_radius_2 = 1.0;
        const double _min_radius_2 = 0.25;
        const double _scale = -2.0;
        const double _bailout = 1024.0;
        const int _max_iters = 32;

        private static void Boxfold(ref Vector z, ref double dz) => z = z.Clamp(-_folding_limit, _folding_limit) * 2.0f - z;

        private static void Spherefold(ref Vector z, ref double dz)
        {
            var factor = _fixed_radius_2 / z.Length2().Clamp(_min_radius_2, _fixed_radius_2);
            z *= factor;
            dz *= factor;
        }

        private static void Scale(ref Vector z, ref double dz)
        {
            z *= _scale;
            dz *= Math.Abs(_scale);
        }

        private static void Offset(ref Vector z, ref double dz, Vector offset)
        {
            z += offset;
            dz += 1;
        }

        private static void SingleMandelbox(ref Vector z, ref double dz, Vector offset)
        {
            Boxfold(ref z, ref dz);
            Spherefold(ref z, ref dz);
            Scale(ref z, ref dz);
            Offset(ref z, ref dz, offset);
        }

        public static double De(Vector offset)
        {
            var z = offset;
            var dz = 0.0;
            var n = _max_iters;
            do
            {
                SingleMandelbox(ref z, ref dz, offset);
            } while (z.Length2() < _bailout && --n > 0);
            var res =  z.Length() / dz;
            if (double.IsNaN(res))
            {
                Console.WriteLine("Mandelbox NaN");
                return 0.0;
            }
            return res;
        }

        public static Vector Normal(Vector offset)
        {
            var delta = 1e-6; // aprox. 8.3x float epsilon
            var dnpp = De(offset + new Vector(-delta, delta, delta));
            var dpnp = De(offset + new Vector(delta, -delta, delta));
            var dppn = De(offset + new Vector(delta, delta, -delta));
            var dnnn = De(offset + new Vector(-delta, -delta, -delta));
            var normal = new Vector((dppn + dpnp) - (dnpp + dnnn),
                (dppn + dnpp) - (dpnp + dnnn),
                (dpnp + dnpp) - (dppn + dnnn));
            //normal->x += (dot(*normal, *normal) == 0.0f); // ensure nonzero
            if (normal.Length2() == 0)
            {
                return new Vector(1, 0, 0);
            }
            return normal.Normalized();
        }
    }
}
