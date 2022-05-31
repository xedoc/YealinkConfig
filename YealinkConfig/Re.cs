using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace sharedlib
{
    public interface IPumaRegexp
    {
        object Matches(string input, string re);
        bool IsMatch(string input, string re);
        object GroupMatches(string input, string re);
    }
    public class PumaRegexp : IPumaRegexp
    {
        public bool IsMatch(string input, string re)
        {
            return Re.IsMatch(input, re);
        }
        public object Matches(string input, string re)
        {
            var reObj = new Regex(re, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var matches = reObj.Matches(input);
            var matchList = new List<string>();
            foreach (Match item in matches)
            {
                matchList.Add(item.Value);
            }
            return matchList.Cast<object>().ToArray();
        }
        public object GroupMatches(string input, string re)
        {
            var reObj = new Regex(re, RegexOptions.IgnoreCase);
            var matches = reObj.Matches(input);
            var matchList = new List<object>();

            if (matches.Count <= 0)
                return matchList.Cast<object>().ToArray();

            foreach (Match item in matches)
            {
                var line = new List<string>();
                for (int i = 0; i < item.Groups.Count; i++)
                {
                    line.Add(item.Groups[i].Value);
                }
                matchList.Add(line.Cast<object>().ToArray());
            }
            return matchList.Cast<object>().ToArray();
        }
    }
    public static class Re
    {
        public static bool IsValidRegex(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;

            try
            {
                Regex.Match("", pattern);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }
        public static string GetSubString(string input, string re)
        {
            if (input == null)
                return null;

            var match = Regex.Match(input, re, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            if (match.Groups.Count <= 1)
                return null;

            var result = match.Groups[1].Value;

            return String.IsNullOrEmpty(result) ? null : result;
        }
        public static string GetMatchByIndex(string input, string re, int index)
        {
            if (input == null)
                return null;

            var matches = Regex.Matches(input, re, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (matches.Count <= 0 || index > matches.Count - 1)
                return null;

            var result = matches[index].Value;

            return String.IsNullOrEmpty(result) ? null : result;
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
        public static string Unescape(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
                return text;

            return Regex.Unescape(text);
        }

        public static string Escape(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
                return text;

            return Regex.Escape(text);
        }
        public static int CountMatches(string re, string text)
        {
            try
            {
                return new Regex(re, RegexOptions.IgnoreCase | RegexOptions.Singleline).Matches(text).Count;
            }
            catch
            {
                return 0;
            }

        }
        public static HashSet<Match> UniqueMatches(string re, string text)
        {
            return UniqueMatches(new Regex(re, RegexOptions.IgnoreCase | RegexOptions.Singleline), text);
        }
        public static HashSet<Match> UniqueMatches(Regex re, string text)
        {
            var matches = re.Matches(text);
            var matchList = new HashSet<Match>();
            foreach (Match match in matches)
            {
                if (!matchList.Any(m => m.Value == match.Value))
                    matchList.Add(match);
            }
            return matchList;
        }
    }
}
