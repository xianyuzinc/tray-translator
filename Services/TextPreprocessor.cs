using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace TrayTranslator.Services
{
    public static class TextPreprocessor
    {
        public static string CleanForTranslation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            text = text.Replace("\r\n", "\n")
                       .Replace('\r', '\n')
                       .Replace("\a", "")
                       .Replace('\u00A0', ' ')
                       .Replace('\u200B', ' ');

            string[] lines = text.Split('\n');
            var paragraphs = new List<string>();
            var current = new StringBuilder();

            foreach (string rawLine in lines)
            {
                string line = Regex.Replace(rawLine.Trim(), @"[ \t]+", " ");
                if (line.Length == 0)
                {
                    FlushParagraph(current, paragraphs);
                    continue;
                }

                if (current.Length == 0)
                {
                    current.Append(line);
                    continue;
                }

                if (TryRemoveTrailingHyphen(current))
                {
                    current.Append(line);
                }
                else if (ShouldJoinWithoutSpace(current, line))
                {
                    current.Append(line);
                }
                else
                {
                    current.Append(' ');
                    current.Append(line);
                }
            }

            FlushParagraph(current, paragraphs);

            string result = string.Join("\n\n", paragraphs.ToArray());
            result = Regex.Replace(result, @"[ \t]{2,}", " ");
            result = Regex.Replace(result, @"([\u4E00-\u9FFF])\s+([\u4E00-\u9FFF])", "$1$2");
            result = Regex.Replace(result, @"\s+([,.;:!?，。；：！？])", "$1");
            return result.Trim();
        }

        private static void FlushParagraph(StringBuilder current, List<string> paragraphs)
        {
            if (current.Length == 0)
            {
                return;
            }

            string paragraph = current.ToString().Trim();
            if (paragraph.Length > 0)
            {
                paragraphs.Add(paragraph);
            }

            current.Length = 0;
        }

        private static bool TryRemoveTrailingHyphen(StringBuilder builder)
        {
            int index = builder.Length - 1;
            while (index >= 0 && char.IsWhiteSpace(builder[index]))
            {
                index--;
            }

            if (index <= 0)
            {
                return false;
            }

            char ch = builder[index];
            if ((ch == '-' || ch == '\u00AD' || ch == '\u2010' || ch == '\u2011') && char.IsLetter(builder[index - 1]))
            {
                builder.Length = index;
                return true;
            }

            return false;
        }

        private static bool ShouldJoinWithoutSpace(StringBuilder current, string nextLine)
        {
            char last = LastVisibleChar(current);
            char first = FirstVisibleChar(nextLine);
            if (last == '\0' || first == '\0')
            {
                return false;
            }

            if (IsCjk(last) || IsCjk(first))
            {
                return true;
            }

            return last == '/' || first == ')' || first == ']' || first == '}';
        }

        private static char LastVisibleChar(StringBuilder builder)
        {
            for (int i = builder.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(builder[i]))
                {
                    return builder[i];
                }
            }

            return '\0';
        }

        private static char FirstVisibleChar(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return text[i];
                }
            }

            return '\0';
        }

        private static bool IsCjk(char ch)
        {
            return (ch >= '\u3400' && ch <= '\u4DBF') ||
                   (ch >= '\u4E00' && ch <= '\u9FFF') ||
                   (ch >= '\uF900' && ch <= '\uFAFF');
        }
    }
}
