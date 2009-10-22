// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2007
// (see accompanying GPPGcopyright.rtf)



using System;
using System.Collections.Generic;
using System.Text;


namespace QUT.GPGen
{
	internal class ProductionItem
	{
		internal Production production;
		internal int pos;
		internal bool expanded;
		internal SetCollection<Terminal> LA;


		internal ProductionItem(Production production, int pos)
		{
			this.production = production;
			this.pos = pos;
		}


		public override bool Equals(object obj)
		{
			ProductionItem item = (ProductionItem)obj;
			return item.pos == pos && item.production == production;
		}

		public override int GetHashCode()
		{
			return production.GetHashCode() + pos;
		}


		internal static bool SameProductions(List<ProductionItem> list1, List<ProductionItem> list2)
		{
			if (list1.Count != list2.Count)
				return false;

			foreach (ProductionItem item1 in list1)
			{
				bool found = false;
				foreach (ProductionItem item2 in list2)
				{
					if (item1.Equals(item2))
					{
						found = true;
						break;
					}
				}
				if (!found)
					return false;
			}
			return true;
		}


		internal bool isReduction()
		{
			return pos == production.rhs.Count;
		}


		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();

			builder.AppendFormat("{0} {1}: ", production.num, production.lhs);


			for (int i = 0; i < production.rhs.Count; i++)
			{
				if (i == pos)
					builder.Append(". ");
				builder.AppendFormat("{0} ", production.rhs[i]);
			}

			if (pos == production.rhs.Count)
				builder.Append(".");

            if (LA != null)
            {
                builder.AppendLine();
                builder.AppendFormat("\t-lookahead: {0}", ListUtilities.GetStringFromList(LA, ", ", 16));
            }

			return builder.ToString();
		}
	}
}