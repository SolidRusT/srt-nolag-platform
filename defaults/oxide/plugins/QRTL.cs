using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System;

namespace Oxide.Plugins
{
    [Info("QRTL", "Quapi", "0.2.0")]
    [Description("A fix for RTL Languages")]
    class QRTL : CovalencePlugin
    {
        private object OnBetterChat(Dictionary<string, object> data)
        {
            var message = data["Message"] as string;
            if (IsRightToLeft(message))
            {
                data["Message"] = RtlText(message);
                return data;
            }
            else
                return null;
        }

        #region -Methods-

        protected string RtlText(string text)
        {
            if (!IsRightToLeft(text)) return text;

            //var reversed = Reverse(text);
            //return IsRTLLang(text) ? reversed.Replace(" ", "") : reversed;

            StringBuilder fixedMessage = new StringBuilder();

            // Keep track of insertion proceedings to retain sentence logic
            // If true, insert at beginning of sentence
            bool resetInsertionPos = false;


            string[] words = text.Split(' ');

            for (int i = 0; i < words.Length; i++)
            {
                if (IsRightToLeft(words[i]))
                {
                    StringBuilder fixedRTLPart = new StringBuilder();

                    for (; i < words.Length; i++)
                    {
                        if (IsRightToLeft(words[i]) || IsNumber(words[i]))
                        {
                            string wordToFix = words[i];
                            fixedRTLPart.Insert(0, FixWord(wordToFix) + " ");
                        }
                        else
                        {
                            i--;
                            break;
                        }
                    }

                    if (!resetInsertionPos)
                    {
                        fixedMessage.Append(fixedRTLPart);
                    }
                    else
                    {
                        fixedMessage.Insert(0, fixedRTLPart);
                        resetInsertionPos = false;
                    }
                }
                else
                {
                    StringBuilder fixedLTRPart = new StringBuilder();

                    for (; i < words.Length; i++)
                    {
                        if (!IsRightToLeft(words[i]))
                        {
                            fixedLTRPart.Append(words[i]).Append(' ');
                        }
                        else
                        {
                            i--;
                            break;
                        }
                    }
                    resetInsertionPos = true;
                    fixedMessage.Insert(0, fixedLTRPart);
                }
            }

            return fixedMessage.ToString();
        }

        protected bool IsBothRTLOrSpecial(char a, char b)
        {
            return IsRTLLang(a) && IsRTLLang(b)
                    || IsRTLLang(a) && IsSpecialChar(b)
                    || IsSpecialChar(a) && IsRTLLang(b)
                    || IsSpecialChar(a) && IsSpecialChar(b);
        }

        protected bool IsSpecialChar(char character)
        {
            return character == '!' || character == ' ' || character == '-' || character == '_' || character == '@'
                    || character == '#' || character == '$' || character == '%' || character == '^' || character == '&' || character == '*'
                    || character == '?' || character == '(' || character == ')' || character == ';';
        }

        protected bool IsNumber(string v)
        {
            foreach (char c in v.ToCharArray())
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }

        protected readonly Regex regex = new Regex(string.Empty, RegexOptions.RightToLeft);

        protected bool IsRTL(string text)
        {
            return regex.Match(text).Success;
        }

        protected bool IsRTLLang(char c)
        {
            return IsRTL(c + "");
        }

        #region -RTL-

        protected string FixWord(string word)
        {
            char[] chars = word.ToCharArray();

            chars = SwapRTLCharacters(chars);
            chars = SwapWordIndexes(chars);

            return new string(chars);
        }

        /**
         * Swaps all RTL characters and switches their order
         */
        protected char[] SwapRTLCharacters(char[] characters)
        {
            Dictionary<int, char> chars = new Dictionary<int, char>();

            for (int i = 0; i < characters.Length; i++)
            {
                chars.Add(i, characters[i]);
            }

            for (int i = 0; i < chars.Count; i++)
            {
                for (int j = i; j < chars.Count; j++)
                {
                    if (IsBothRTLOrSpecial(chars[i], chars[j]))
                    {
                        char tmp = chars[j];
                        chars[j] = chars[i];
                        chars[i] = tmp;
                    }
                    else 
                    {
                        break;
                    }
                }
            }

            char[] returnable = new char[chars.Count];
            for (int i = 0; i < chars.Count; i++)
                returnable[i] = chars[i];


            return returnable;
        }

        protected char[] SwapWordIndexes(char[] characters)
        {
            if (characters.Length == 0) return new char[0];

            char[] chars = characters;

            Stack<string> innerWords = new Stack<string>();

            StringBuilder currentWord = new StringBuilder();
            foreach (char character in chars) {
                if (currentWord.Length == 0 || IsBothRTLOrSpecial(currentWord[0], character)
                        || !IsRightToLeft(currentWord[0].ToString()) && !IsSpecialChar(currentWord[0])
                        && !IsRightToLeft(character.ToString()) && !IsSpecialChar(character))
                {
                    currentWord.Append(character);
                }
                else
                {
                    innerWords.Push(currentWord.ToString());
                    currentWord = new StringBuilder("" + character);
                }
            }

            if (currentWord.Length > 0)
            {
                innerWords.Push(currentWord.ToString());
            }

            if (innerWords.Count == 0)
            {
                return new char[0];
            }

            int currentIndex = 0;
            while (innerWords.Count != 0)
            {
                string s = innerWords.Pop();
                foreach (char c in s.ToCharArray())
                {
                    chars[currentIndex] = c;
                    currentIndex++;
                }
            }

            return chars;
        }

        protected bool IsRightToLeft(string text)
        {
            return regex.Match(text).Success;
        }
        #endregion
    #endregion
    }
}
