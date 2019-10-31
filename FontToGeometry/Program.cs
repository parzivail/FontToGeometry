using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FontToGeometry
{
    class Program
    {
        static void Main(string[] args)
        {
            const int maskType = 0x7;

            var font = new FontFamily("IBM Plex Sans");
            var style = FontStyle.Regular;
            float size = 1;

            var ascent = font.GetCellAscent(style) / (float)font.GetEmHeight(style) * size;

            var chars = new Dictionary<char, string>
            {
                {'~', "Grave"},
                {'`', "Tick"},
                {'1', "1"},
                {'2', "2"},
                {'3', "3"},
                {'4', "4"},
                {'5', "5"},
                {'6', "6"},
                {'7', "7"},
                {'8', "8"},
                {'9', "9"},
                {'0', "0"},
                {'!', "Excl"},
                {'@', "At"},
                {'#', "Pound"},
                {'$', "Dollar"},
                {'%', "Percent"},
                {'^', "Carrot"},
                {'&', "Amp"},
                {'*', "Ast"},
                {'(', "OPar"},
                {')', "CPar"},
                {'_', "Under"},
                {'-', "Minus"},
                {'+', "Plus"},
                {'=', "Equals"},
                {'{', "OCur"},
                {'}', "CCur"},
                {'[', "OSqr"},
                {']', "CSqr"},
                {'|', "Pipe"},
                {'\\', "BSlash"},
                {':', "Colon"},
                {';', "Semi"},
                {'"', "DQuo"},
                {'\'', "SQuo"},
                {'<', "Lt"},
                {'>', "Gt"},
                {'?', "Ques"},
                {',', "Comma"},
                {'.', "Period"},
                {'/', "Slash"},
            };

            for (var i = 'a'; i <= 'z'; i++)
                chars.Add(i, i.ToString());

            for (var i = 'A'; i <= 'Z'; i++)
                chars.Add(i, i.ToString());

            using (var t = new StreamWriter("out.txt"))
            {
                foreach (var pair in chars)
                {
                    var segments = new List<Segment>();

                    var c = pair.Key;
                    var name = pair.Value;

                    using (var p = new GraphicsPath())
                    {
                        p.AddString(c.ToString(), font, (int)style, size, Point.Empty, StringFormat.GenericTypographic);

                        var points = new List<PointF>();
                        points.AddRange(p.PathPoints);
                        points.Add(p.PathPoints[0]);

                        PointF? loopStart = null;

                        var seg = 0;
                        for (var i = 0; i < points.Count; i++)
                        {
                            var iA = i;
                            var iB = i - 1;

                            var tStart = (seg - 1) / (float)points.Count;
                            var tEnd = seg / (float)points.Count;

                            var type = (p.PathTypes[i % p.PointCount] & maskType);

                            switch (type)
                            {
                                case 0:
                                    {
                                        if (loopStart.HasValue)
                                            AddLineSegment(segments, points[iB], loopStart.Value, ascent, tStart, tEnd);

                                        loopStart = points[iA];
                                        break;
                                    }
                                case 1:
                                    {
                                        AddLineSegment(segments, points[iA], points[iB], ascent, tStart, tEnd);
                                        break;
                                    }
                                case 3:
                                    {
                                        AddCubicSegment(segments, points[(i - 1) % points.Count], points[(i + 0) % points.Count], points[(i + 1) % points.Count], points[(i + 2) % points.Count], ascent, tStart, tEnd);
                                        i += 2;
                                        break;
                                    }
                                default:
                                    throw new IndexOutOfRangeException("Invalid type!");
                            }

                            seg++;
                        }
                    }

                    // The output functions depend on these two utility functions:

                    // Linear interpolation
                    // \phi\left(a_{1},b_{1},t_{1}\right)=t_{1}a_{1}+\left(1-t_{1}\right)b_{1}

                    // Cubic bezier interpolation
                    // B_{c}\left(p_{0},p_{1},p_{2},p_{3},t_{1}\right)=\left(1-t_{1}\right)^{3}p_{0}+3\left(1-t_{1}\right)^{2}t_{1}p_{1}+3\left(1-t_{1}\right)t_{1}^{2}p_{2}+t_{1}^{3}p_{3}

                    t.Write($"S_{{{name}cx}}\\left(t_1\\right)=\\left\\{{");
                    t.Write(string.Join(",", segments.Select(segment => segment.ToStringX())));
                    t.WriteLine("\\right\\}");

                    t.Write($"S_{{{name}cy}}\\left(t_1\\right)=\\left\\{{");
                    t.Write(string.Join(",", segments.Select(segment => segment.ToStringY())));
                    t.WriteLine("\\right\\}");

                    t.WriteLine($"L_{{{name}}}\\left(x_1,y_1,s_1,t_1\\right)=\\left(s_1S_{{{name}cx}}\\left(t_1\\right)+x_1,s_1S_{{{name}cy}}\\left(t_1\\right)+y_1\\right)");
                }
            }
        }

        private static void AddLineSegment(ICollection<Segment> segments, PointF a, PointF b, float ascent, float tStart, float tEnd)
        {
            a.Y = ascent - a.Y;
            b.Y = ascent - b.Y;

            segments.Add(new LineSegment(tStart, tEnd, a, b));
        }

        private static void AddCubicSegment(ICollection<Segment> segments, PointF a, PointF b, PointF c, PointF d, float ascent, float tStart, float tEnd)
        {
            a.Y = ascent - a.Y;
            b.Y = ascent - b.Y;
            c.Y = ascent - c.Y;
            d.Y = ascent - d.Y;

            segments.Add(new CubicSegment(tStart, tEnd, a, b, c, d));
        }
    }

    internal class LineSegment : Segment
    {
        public float ValueStart;
        public float ValueEnd;
        public PointF Start;
        public PointF End;

        public LineSegment(float valueStart, float valueEnd, PointF start, PointF end)
        {
            ValueStart = valueStart;
            ValueEnd = valueEnd;
            Start = start;
            End = end;
        }

        public override string ToStringX()
        {
            return $"{Str(ValueStart)}<t_1<{Str(ValueEnd)}:\\phi\\left({Str(Start.X)},{Str(End.X)},\\frac{{\\left(t_1-{Str(ValueStart)}\\right)}}{{{Str(ValueEnd - ValueStart)}}}\\right)";
        }

        public override string ToStringY()
        {
            return $"{Str(ValueStart)}<t_1<{Str(ValueEnd)}:\\phi\\left({Str(Start.Y)},{Str(End.Y)},\\frac{{\\left(t_1-{Str(ValueStart)}\\right)}}{{{Str(ValueEnd - ValueStart)}}}\\right)";
        }
    }

    internal class CubicSegment : Segment
    {
        public float ValueStart;
        public float ValueEnd;
        public PointF A;
        public PointF B;
        public PointF C;
        public PointF D;

        public CubicSegment(float valueStart, float valueEnd, PointF a, PointF b, PointF c, PointF d)
        {
            ValueStart = valueStart;
            ValueEnd = valueEnd;
            A = a;
            B = b;
            C = c;
            D = d;
        }

        public override string ToStringX()
        {
            return $"{Str(ValueStart)}<t_1<{Str(ValueEnd)}:B_{{c}}\\left({Str(A.X)},{Str(B.X)},{Str(C.X)},{Str(D.X)},\\frac{{t_1-{Str(ValueStart)}}}{{{Str(ValueEnd - ValueStart)}}}\\right)";
        }

        public override string ToStringY()
        {
            return $"{Str(ValueStart)}<t_1<{Str(ValueEnd)}:B_{{c}}\\left({Str(A.Y)},{Str(B.Y)},{Str(C.Y)},{Str(D.Y)},\\frac{{t_1-{Str(ValueStart)}}}{{{Str(ValueEnd - ValueStart)}}}\\right)";
        }
    }

    internal abstract class Segment
    {
        public abstract string ToStringX();

        public abstract string ToStringY();

        /// <summary>
        /// Gets rid of scientific notation
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        protected static string Str(double input)
        {
            var strOrig = input.ToString(CultureInfo.InvariantCulture);
            var str = strOrig.ToUpper();

            // if string representation was collapsed from scientific notation, just return it:
            if (!str.Contains("E")) return strOrig;

            var negativeNumber = false;

            if (str[0] == '-')
            {
                str = str.Remove(0, 1);
                negativeNumber = true;
            }

            var sep = CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator;
            var decSeparator = sep.ToCharArray()[0];

            var exponentParts = str.Split('E');
            var decimalParts = exponentParts[0].Split(decSeparator);

            // fix missing decimal point:
            if (decimalParts.Length == 1) decimalParts = new string[] { exponentParts[0], "0" };

            var exponentValue = int.Parse(exponentParts[1]);

            var newNumber = decimalParts[0] + decimalParts[1];

            string result;

            if (exponentValue > 0)
            {
                result =
                    newNumber +
                    GetZeros(exponentValue - decimalParts[1].Length);
            }
            else // negative exponent
            {
                result =
                    "0" +
                    decSeparator +
                    GetZeros(exponentValue + decimalParts[0].Length) +
                    newNumber;

                result = result.TrimEnd('0');
            }

            if (negativeNumber)
                result = "-" + result;

            return result;
        }

        private static string GetZeros(int zeroCount)
        {
            if (zeroCount < 0)
                zeroCount = Math.Abs(zeroCount);

            var sb = new StringBuilder();

            for (var i = 0; i < zeroCount; i++) sb.Append("0");

            return sb.ToString();
        }
    }
}
