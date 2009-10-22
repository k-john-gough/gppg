// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2007
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