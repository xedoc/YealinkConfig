using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace sharedlib
{
    public static class Util
    {
        public static string Try(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                return e.Message;
            }

            return String.Empty;
        }
        public static void Try(Action action, Action<Exception> exceptionAction)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                exceptionAction(e);
            }
        }
        public static string LimitToDateTime( this string text )
        {
            if (String.IsNullOrEmpty(text))
                return String.Empty;

            string date;
            try
            {
                var offset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
                date = Convert.ToDateTime(text).ToString("s") + String.Format("{2}{0:00}:{1:00}", offset.Hours, offset.Minutes, offset.Hours < 0 ? "-" : "+");
                
            }
            catch(FormatException e)
            {
                throw new FormatException(String.Format("Invalid date provided {0}", text));
            }
            return date;
        }
        public static string LimitedDecimal( this string text, int maxLength = 16, int digitsAfterPoint = 4)
        {
            if (String.IsNullOrEmpty(text))
                return String.Empty;

            if ( !double.TryParse(text.Replace(",",".").Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double doubleValue))
                throw new ArgumentException(String.Format("Invalid decimal value provided: {0}", text));
            

            var result = Math.Round(doubleValue, digitsAfterPoint).ToString().Replace(",",".");

            if ( result.ToString().Length > maxLength )
                throw new ArgumentOutOfRangeException(String.Format("Value {0} is out of range. Max length = {1}, max digits after point = {2}", text, maxLength, digitsAfterPoint));

            return result;
        }
        public static string RightNumber(this string text, int length, bool check = true)
        {
            if (String.IsNullOrEmpty(text))
                return text;
            
            if( !check)
            {
                return text.Right(length);
            }
            else
            {
                var digits = StringBuilderChars(text.Where(Char.IsDigit));           
                return digits.Right(length);
            }

        }
        public static bool IsMatch(string input, string pattern)
        {
            if (input != null)
                return Regex.IsMatch(input, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            else if (pattern == null)
                return true;
            else
                return false;
        }
        public static string LeftNumber(this string text, int length)
        {
            if (String.IsNullOrEmpty(text))
                return text;

            var digits = StringBuilderChars(text.Where(Char.IsDigit));
            return digits.Left(length);
        }
        public static string StringBuilderChars(IEnumerable<char> charSequence)
        {
            var sb = new StringBuilder();
            foreach (var c in charSequence)
            {
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Left(this string text, int length)
        {
            if (length == 0)
                return string.Empty;

            if (String.IsNullOrEmpty(text))
                return text;

            length = Math.Abs(length);

            return text.Substring(0, Math.Min(length, text.Length));
        }
        public static string Right(this string text, int length)
        {
            if (String.IsNullOrEmpty(text))
                return text;

            if (length >= text.Length)
                return text;

            if (length == 0)
                return string.Empty;

            length = Math.Min(Math.Abs(length), text.Length);
            return text.Substring(text.Length - length, length);
        }
        public static string LimitValues(this string text, string[] strings)
        {
            if (strings.Any(x => x.Equals(text, StringComparison.InvariantCultureIgnoreCase)))
            {
                return text;
            }
            else
                throw new ArgumentException(@"Invalid value provided: """ + text + @""". Only " + String.Join(", ", strings) + " are allowed");
        }
    }
}
