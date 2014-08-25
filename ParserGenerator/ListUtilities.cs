// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, K John Gough, QUT 2006-2014
// (see accompanying GPPGcopyright.rtf)
// This file author: John Gough

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace QUT.GPGen
{
    public delegate TOut Mapper<TOut,TIn>(TIn input);

    public static class ListUtilities
    {
        /// <summary>
        /// The (line) break rule for list formation has possible values
        /// never, meaning never break the list;
        /// length, meaning break list when line-length would exceed 80;
        /// positive lengths (BreakRule)N, means break list after each N
        /// elements.
        /// </summary>
        public enum BreakRule { never = -1, length = 0, count };
        public const int LineLength = 80;

        public static string GetStringFromList<T>(IEnumerable<T> list)
        {
            return GetStringFromList<T>(list, ", ", 4, BreakRule.length);
        }

        public static string GetStringFromList<T>(IEnumerable<T> list, string separator, int indent) {
            return GetStringFromList<T>(list, separator, indent, BreakRule.length);
        }

        public static string GetStringFromList<T>(IEnumerable<T> list, string separator, int indent, BreakRule lineBreak)
        {
            int lastBreak = -indent;
            int itemCount = 0;
            bool more = false;
            string indentStr = new String(' ', indent);
            string listBreak = System.Environment.NewLine + indentStr;
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            IEnumerator<T> e = list.GetEnumerator();
            if (e.MoveNext())
                do {
                    T nt = e.Current;
                    string addend = nt.ToString();
                    switch (lineBreak) {
                        case BreakRule.never: break;
                        case BreakRule.length:
                            if (builder.Length + addend.Length >= lastBreak + LineLength) {
                                lastBreak = builder.Length;
                                builder.Append(listBreak);
                            }
                            break;
                        default:
                            if (itemCount >= (int)lineBreak) {
                                builder.Append( listBreak );
                                itemCount = 0;
                            }
                            itemCount++;
                            break;
                    }
                    more = e.MoveNext();
                    builder.AppendFormat("{0}{1}", nt.ToString(), (more ? separator : ""));
                } while (more);

            return builder.ToString();
        }

        public static Collection<TOut> MapC<TOut, TIn>(IEnumerable<TIn> input, Mapper<TOut, TIn> map)
        {
            Collection<TOut> rslt = new Collection<TOut>();
            foreach (TIn elem in input)
                rslt.Add(map(elem));
            return rslt;
        }


    }
}
