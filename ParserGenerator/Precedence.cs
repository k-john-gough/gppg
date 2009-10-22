// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2007
// (see accompanying GPPGcopyright.rtf)


using System;


namespace QUT.GPGen
{
	internal enum PrecType { left, right, nonassoc, token };
 
	internal class Precedence
	{
		internal PrecType type;
		internal int prec;

		internal Precedence(PrecType type, int prec)
		{
			this.type = type;
			this.prec = prec;
		}

		internal static void Calculate(Production p)
		{
			// Precedence of a production is that of its rightmost terminal
			// unless explicitly labelled with %prec

			if (p.prec == null)
				for (int i = p.rhs.Count - 1; i >= 0; i--)
					if (p.rhs[i] is Terminal)
					{
						p.prec = ((Terminal)p.rhs[i]).prec;
						break;
					}
		}
	}
}