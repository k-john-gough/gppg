// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2014
// (see accompanying GPPGcopyright.rtf)


using System;
using System.Text;
using System.Collections.Generic;


namespace QUT.GPGen
{
	internal class Production
	{
		internal int num;
		internal NonTerminal lhs;
		internal List<Symbol> rhs = new List<Symbol>();
		internal SemanticAction semanticAction;
		internal Precedence prec;
        internal Parser.LexSpan precSpan;


		internal Production(NonTerminal lhs)
		{
			this.lhs = lhs;
			lhs.productions.Add(this);
		}

        /// <summary>
        /// For this Production, return the rightmost Terminal, if any.
        /// </summary>
        /// <returns>Rightmost Terminal, or null</returns>
        internal Terminal RightmostTerminal() {
            Terminal result = null;
            for (int i = this.rhs.Count - 1; i >= 0; i--)
                if ((result = this.rhs[i] as Terminal) != null)
                    break;
            return result;
        }


		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();

			builder.AppendFormat("{0} -> ", lhs);
            if (rhs.Count == 0)
                builder.Append("/* empty */");
            else
                builder.Append(ListUtilities.GetStringFromList(rhs, ", ", builder.Length));
            return builder.ToString();
		}
	}
}
