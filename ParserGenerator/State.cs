// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2014
// (see accompanying GPPGcopyright.rtf)


using System;
using System.Collections.Generic;
using System.Text;


namespace QUT.GPGen
{
	internal class AutomatonState
	{
		private static int TotalStates;

		internal int num;

		internal List<ProductionItem> kernelItems = new List<ProductionItem>();
		internal List<ProductionItem> allItems    = new List<ProductionItem>();
		internal Dictionary<Symbol, AutomatonState> Goto = new Dictionary<Symbol, AutomatonState>();
		internal SetCollection<Terminal> terminalTransitions = new SetCollection<Terminal>();
		internal Dictionary<NonTerminal, Transition> nonTerminalTransitions = new Dictionary<NonTerminal, Transition>();
		internal Dictionary<Terminal, ParserAction> parseTable = new Dictionary<Terminal, ParserAction>();

        internal List<Symbol> shortestPrefix;
        internal List<AutomatonState> statePath;
        internal List<AutomatonState> predecessors;
        internal List<Conflict> conflicts;

        bool forceLookahead; 
        /// <summary>
        /// If this boolean is set, the emitted parser file will not use a
        /// default action.  This is necessary to ensure that violations of
        /// the use of a %nonassoc token are detected as errors even if the 
        /// state would othewise have a default action.
        /// </summary>
        public bool ForceLookahead {
            get { return forceLookahead; }
            set { forceLookahead = value; }
        }

		internal AutomatonState(Production production)
		{
			num = TotalStates++;
			AddKernel(production, 0);
		}


		internal AutomatonState(List<ProductionItem> itemSet)
		{
			num = TotalStates++;
			kernelItems.AddRange(itemSet);
			allItems.AddRange(itemSet);
		}


		internal void AddClosure()
		{
			foreach (ProductionItem item in kernelItems)
				AddClosure(item);
		}


		private void AddClosure(ProductionItem item)
		{
			if (item.pos < item.production.rhs.Count)
			{
                NonTerminal nonTerm = null;
				Symbol rhs = item.production.rhs[item.pos];
				if ((nonTerm = rhs as NonTerminal) != null)
					foreach (Production p in nonTerm.productions)
						AddNonKernel(p);
			}
		}


		private void AddKernel(Production production, int pos)
		{
			ProductionItem item = new ProductionItem(production, pos);
			kernelItems.Add(item);
			allItems.Add(item);
		}


		private void AddNonKernel(Production production)
		{
			ProductionItem item = new ProductionItem(production, 0);

			if (!allItems.Contains(item))
			{
				allItems.Add(item);
				AddClosure(item);
			}
		}


		internal void AddGoto(Symbol s, AutomatonState next)
		{
			this.Goto[s] = next;
            Terminal term;

            if ((term = s as Terminal) != null)
                terminalTransitions.Add(term);
            else
            {
                NonTerminal nonTerm = (NonTerminal)s;
                nonTerminalTransitions.Add(nonTerm, new Transition(nonTerm, next));
            }
		}

        internal string ItemDisplay()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("State {0}", num);
            foreach (ProductionItem item in kernelItems)
            {
                builder.AppendLine();
                builder.AppendFormat("    {0}", item);
            }
            return builder.ToString();
        }

        internal void AddPredecessor( AutomatonState pred ) {
            if (predecessors == null)
                predecessors = new List<AutomatonState>();
            if (!predecessors.Contains( pred ))
                predecessors.Add( pred );
        }


		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();

			builder.AppendFormat("State {0}", num);
			builder.AppendLine();
			builder.AppendLine();

			foreach (ProductionItem item in kernelItems)
			{
				builder.AppendFormat("    {0}", item);
				builder.AppendLine();
			}

			builder.AppendLine();

			foreach (KeyValuePair<Terminal, ParserAction> a in parseTable)
			{
				builder.AppendFormat("    {0,-14} {1}", a.Key, a.Value);
				builder.AppendLine();
			}

			builder.AppendLine();

			foreach (KeyValuePair<NonTerminal, Transition> n in nonTerminalTransitions)
			{
				builder.AppendFormat("    {0,-14} go to state {1}", n.Key, Goto[n.Key].num);
				builder.AppendLine();
			}

			builder.AppendLine();

			return builder.ToString();
		}

        internal void Link(Conflict conflict)
        {
            if (this.conflicts == null)
                conflicts = new List<Conflict>();
            conflicts.Add(conflict);
        }
	}
}