// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2007
// (see accompanying GPPGcopyright.rtf)


using System;


namespace QUT.GPGen
{
	internal abstract class ParserAction
	{
        internal abstract int ToNum();
	}


	internal class Shift : ParserAction
	{
		internal AutomatonState next;

		internal Shift(AutomatonState next)
		{
			this.next = next;
		}

		public override string ToString()
		{
			return "shift, and go to state " + next.num;
		}

        internal override int ToNum()
        {
            return next.num;
        }
	}


	internal class Reduce : ParserAction
	{
		internal ProductionItem item;

		internal Reduce(ProductionItem item)
		{
			this.item = item;
		}

		public override string ToString()
		{
			return "reduce using rule " + item.production.num + " (" + item.production.lhs + ")";
		}

        internal override int  ToNum()
        {
            return -item.production.num;
        }
	}
}