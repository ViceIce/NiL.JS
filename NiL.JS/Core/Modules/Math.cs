using NiL.JS.Core;
using NiL.JS.Core.BaseTypes;
using System;

namespace NiL.JS.Core.Modules
{
    internal static class Math
    {
        private static class _Random
        {
            public readonly static Random random = new Random((int)DateTime.Now.Ticks);
        }

        [Protected]
        public const double E = System.Math.E;
        [Protected]
        public const double PI = System.Math.PI;
        [Protected]
        public static readonly double LN2 = System.Math.Log(2);
        [Protected]
        public static readonly double LN10 = System.Math.Log(10);
        [Protected]
        public static readonly double LOG2E = 1.0 / LN2;
        [Protected]
        public static readonly double LOG10E = 1.0 / LN10;
        [Protected]
        public static readonly double SQRT1_2 = System.Math.Sqrt(0.5);
        [Protected]
        public static readonly double SQRT2 = System.Math.Sqrt(2);

        public static JSObject abs(JSObject[] args)
        {
            return System.Math.Abs(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null));
        }

        public static JSObject acos(JSObject[] args)
        {
            return System.Math.Acos(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null));
        }

        public static JSObject asin(JSObject[] args)
        {
            return System.Math.Asin(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null));
        }

        public static JSObject atan(JSObject[] args)
        {
            return System.Math.Atan(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null));
        }

        [ParametersCount(2)]
        public static JSObject atan2(JSObject[] args)
        {
            if (args.Length < 2)
                return double.NaN;
            return System.Math.Atan2(Tools.JSObjectToDouble(args[0]), Tools.JSObjectToDouble(args[1]));
        }

        public static JSObject ceil(JSObject[] args)
        {
            return System.Math.Ceiling(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null));
        }

        public static JSObject cos(JSObject[] args)
        {
            return System.Math.Cos(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null));
        }

        public static JSObject exp(JSObject[] args)
        {
            return System.Math.Exp(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null));
        }

        public static JSObject floor(JSObject[] args)
        {
            var a = Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null);
            if (a < 0 && a > -1e-15)
                a = 0;
            if (a > 0 && a < -1e-15)
                a = 0;
            JSObject result = 0;
            result.dValue = System.Math.Floor(a);
            result.ValueType = JSObjectType.Double;
            return result;
        }

        public static JSObject log(JSObject[] args)
        {
            return System.Math.Log(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null));
        }

        [ParametersCount(2)]
        public static JSObject max(JSObject[] args)
        {
            double res = double.NegativeInfinity;
            for (int i = 0; i < args.Length; i++)
            {
                var t = Tools.JSObjectToDouble(args[i]);
                if (double.IsNaN(t))
                    return double.NaN;
                res = System.Math.Max(res, t);
            }
            return res;
        }

        [ParametersCount(2)]
        public static JSObject min(JSObject[] args)
        {
            double res = double.PositiveInfinity;
            for (int i = 0; i < args.Length; i++)
            {
                var t = Tools.JSObjectToDouble(args[i]);
                if (double.IsNaN(t))
                    return double.NaN;
                res = System.Math.Min(res, t);
            }
            return res;
        }

        [ParametersCount(2)]
        public static JSObject pow(JSObject[] args)
        {
            JSObject result = 0;
            if (args.Length < 2)
                result.dValue = double.NaN;
            else
            {
                var @base = Tools.JSObjectToDouble(args[0]);
                var degree = Tools.JSObjectToDouble(args[1]);
                if (@base == 1 && double.IsInfinity(degree))
                    result.dValue = double.NaN;
                else if (double.IsNaN(@base) && degree == 0.0)
                    result.dValue = 1;
                else
                {
                    result.dValue = System.Math.Pow(@base, degree);
                }
            }
            result.ValueType = JSObjectType.Double;
            return result;
        }

        public static JSObject random()
        {
            return _Random.random.NextDouble();
        }

        public static JSObject round(JSObject[] args)
        {
            return System.Math.Round(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null) + 0.001);
        }

        public static JSObject sin(JSObject[] args)
        {
            return System.Math.Sin(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null));
        }

        public static JSObject sqrt(JSObject[] args)
        {
            return System.Math.Sqrt(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null));
        }

        public static JSObject tan(JSObject[] args)
        {
            return System.Math.Tan(Tools.JSObjectToDouble(args.Length > 0 ? args[0] : null));
        }

        #region Exclusives

        [ParametersCount(2)]
        public static JSObject IEEERemainder(JSObject[] args)
        {
            if (args.Length < 2)
                return double.NaN;
            return System.Math.IEEERemainder(Tools.JSObjectToDouble(args[0]), Tools.JSObjectToDouble(args[1]));
        }

        public static JSObject sign(JSObject[] args)
        {
            if (args.Length < 1)
                return double.NaN;
            return System.Math.Sign(Tools.JSObjectToDouble(args[0]));
        }

        public static JSObject sinh(JSObject[] args)
        {
            if (args.Length < 1)
                return double.NaN;
            return System.Math.Sinh(Tools.JSObjectToDouble(args[0]));
        }

        public static JSObject tanh(JSObject[] args)
        {
            if (args.Length < 1)
                return double.NaN;
            return System.Math.Tanh(Tools.JSObjectToDouble(args[0]));
        }

        public static JSObject trunc(JSObject[] args)
        {
            if (args.Length < 1)
                return double.NaN;
            return System.Math.Truncate(Tools.JSObjectToDouble(args[0]));
        }

        #endregion
    }
}
