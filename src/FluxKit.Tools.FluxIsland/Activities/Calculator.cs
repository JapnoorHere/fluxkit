using System.Globalization;

namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>
/// A small, safe calculator for the command bar: numbers, + − × ÷, ^ (power), % (remainder) and brackets. It reads
/// the text itself, so nothing typed is ever run as code. "x" and "×" mean times, "÷" means divide.
/// </summary>
public static class Calculator
{
    /// <summary>The value of <paramref name="expression"/>, or null if it isn't a sum the calculator understands.</summary>
    public static double? Evaluate(string expression)
    {
        var parser = new Parser(expression);
        try
        {
            double value = parser.ParseExpression();
            return parser.AtEnd && double.IsFinite(value) ? value : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>True if the text looks like a sum (has a digit and an operator), so plain words aren't treated as maths.</summary>
    public static bool LooksLikeMaths(string text) =>
        text.Any(char.IsAsciiDigit) && text.IndexOfAny(['+', '-', '*', '/', '^', '%', '×', '÷', '(', 'x']) >= 0 && Evaluate(text) is not null;

    /// <summary>A result as people write it: up to 10 significant digits, no trailing zeros.</summary>
    public static string Format(double value) =>
        Math.Abs(value) >= 1e12 || (value != 0 && Math.Abs(value) < 1e-6)
            ? value.ToString("0.######E+0", CultureInfo.CurrentCulture)
            : Math.Round(value, 10).ToString("#,0.##########", CultureInfo.CurrentCulture);

    /// <summary>Recursive descent over the grammar: expression = term {(+|−) term}; term = power {(×|÷|%) power}; power = unary [^ power].</summary>
    private sealed class Parser(string text)
    {
        private const int MaxDepth = 64;
        private int _at;
        private int _depth;

        public bool AtEnd
        {
            get
            {
                SkipSpaces();
                return _at >= text.Length;
            }
        }

        public double ParseExpression()
        {
            double value = ParseTerm();
            while (true)
            {
                if (Accept('+'))
                {
                    value += ParseTerm();
                }
                else if (Accept('-') || Accept('−'))
                {
                    value -= ParseTerm();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseTerm()
        {
            double value = ParsePower();
            while (true)
            {
                if (Accept('*') || Accept('×') || Accept('x') || Accept('X'))
                {
                    value *= ParsePower();
                }
                else if (Accept('/') || Accept('÷'))
                {
                    double divisor = ParsePower();
                    value = divisor == 0 ? throw new FormatException() : value / divisor;
                }
                else if (Accept('%'))
                {
                    double divisor = ParsePower();
                    value = divisor == 0 ? throw new FormatException() : value % divisor;
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParsePower()
        {
            double value = ParseUnary();
            return Accept('^') ? Math.Pow(value, ParsePower()) : value;
        }

        private double ParseUnary()
        {
            if (Accept('-') || Accept('−'))
            {
                return -ParseUnary();
            }

            if (Accept('+'))
            {
                return ParseUnary();
            }

            if (Accept('('))
            {
                if (++_depth > MaxDepth)
                {
                    throw new FormatException();
                }

                double inner = ParseExpression();
                _depth--;
                return Accept(')') ? inner : throw new FormatException();
            }

            return ParseNumber();
        }

        private double ParseNumber()
        {
            SkipSpaces();
            int start = _at;
            while (_at < text.Length && (char.IsAsciiDigit(text[_at]) || text[_at] is '.' or ','))
            {
                _at++;
            }

            // Commas group thousands ("1,200"); a dot is the decimal point.
            string number = text[start.._at].Replace(",", string.Empty, StringComparison.Ordinal);
            return number.Length > 0 && double.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value)
                ? value
                : throw new FormatException();
        }

        private bool Accept(char symbol)
        {
            SkipSpaces();
            if (_at < text.Length && text[_at] == symbol)
            {
                _at++;
                return true;
            }

            return false;
        }

        private void SkipSpaces()
        {
            while (_at < text.Length && char.IsWhiteSpace(text[_at]))
            {
                _at++;
            }
        }
    }
}
