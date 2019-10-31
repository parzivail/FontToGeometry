using System;
using System.Collections.Generic;
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
                        p.AddString(c.ToString(), font, (int)style, size, Point.Empty, StringFormat.GenericDefault);

                        p.Flatten(new Matrix(), 0.01f);

                        var points = new List<PointF>();
                        points.AddRange(p.PathPoints);
                        points.Add(p.PathPoints[0]);

                        PointF? loopStart = null;

                        for (var i = 0; i < points.Count; i++)
                        {
                            var iA = i;
                            var iB = i - 1;

                            var tStart = iB / (float)points.Count;
                            var tEnd = iA / (float)points.Count;

                            if ((p.PathTypes[i % p.PointCount] & maskType) == 0)
                            {
                                if (loopStart.HasValue)
                                    AddSegment(segments, points[iB], loopStart.Value, ascent, tStart, tEnd);

                                loopStart = points[iA];
                                continue;
                            }

                            AddSegment(segments, points[iA], points[iB], ascent, tStart, tEnd);
                        }
                    }

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

        private static void AddSegment(ICollection<Segment> segments, PointF a, PointF b, float ascent, float tStart, float tEnd)
        {
            a.Y = ascent - a.Y;
            b.Y = ascent - b.Y;

            segments.Add(new Segment(tStart, tEnd, a, b));
        }
    }

    internal struct Segment
    {
        public float ValueStart;
        public float ValueEnd;
        public PointF Start;
        public PointF End;

        public Segment(float valueStart, float valueEnd, PointF start, PointF end)
        {
            ValueStart = valueStart;
            ValueEnd = valueEnd;
            Start = start;
            End = end;
        }
        
        public string ToStringX()
        {
            return $"{Str(ValueStart)}<t_1<{Str(ValueEnd)}:\\phi\\left({Str(Start.X)},{Str(End.X)},\\frac{{\\left(t_1-{Str(ValueStart)}\\right)}}{{{Str(ValueEnd - ValueStart)}}}\\right)";
        }

        public string ToStringY()
        {
            return $"{Str(ValueStart)}<t_1<{Str(ValueEnd)}:\\phi\\left({Str(Start.Y)},{Str(End.Y)},\\frac{{\\left(t_1-{Str(ValueStart)}\\right)}}{{{Str(ValueEnd - ValueStart)}}}\\right)";
        }

        /// <summary>
        /// Gets rid of scientific notation
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string Str(double input)
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
