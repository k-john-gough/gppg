// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2007
// (see accompanying GPPGcopyright.rtf)


using System;
using System.Collections.Generic;
using System.Globalization;


namespace QUT.GPGen
{
	internal class LR0Generator
	{
		protected List<AutomatonState> states = new List<AutomatonState>();
		protected Grammar grammar;
		private Dictionary<Symbol, List<AutomatonState>> accessedBy = new Dictionary<Symbol,List<AutomatonState>>();


		internal LR0Generator(Grammar grammar)
		{
			this.grammar = grammar;
		}


        internal List<AutomatonState> BuildStates()
		{
			// create state for root production and expand recursively
			ExpandState(grammar.rootProduction.lhs, new AutomatonState(grammar.rootProduction));
            
            return states;
		}


		private void ExpandState(Symbol sym, AutomatonState newState)
		{
			//newState.accessedBy = sym;
			states.Add(newState);

			if (!accessedBy.ContainsKey(sym))
				accessedBy[sym] = new List<AutomatonState>();
			accessedBy[sym].Add(newState);

			newState.AddClosure();
			ComputeGoto(newState);
		}


		private void ComputeGoto(AutomatonState state)
		{
			foreach (ProductionItem item in state.allItems)
				if (!item.expanded && !item.isReduction())
				{
					item.expanded = true;
					Symbol s1 = item.production.rhs[item.pos];

					// Create itemset for new state ...
					List<ProductionItem> itemSet = new List<ProductionItem>();
					itemSet.Add(new ProductionItem(item.production, item.pos+1));

					foreach (ProductionItem item2 in state.allItems)
						if (!item2.expanded && !item2.isReduction())
						{
							Symbol s2 = item2.production.rhs[item2.pos];

							if (s1 == s2)
							{
								item2.expanded = true;
								itemSet.Add(new ProductionItem(item2.production, item2.pos+1));
							}
						}

					AutomatonState existingState = FindExistingState(s1, itemSet);

					if (existingState == null)
					{
						AutomatonState newState = new AutomatonState(itemSet);
						state.AddGoto(s1, newState);
						ExpandState(s1, newState);
					}
					else
						state.AddGoto(s1, existingState);
				}
		}


		private AutomatonState FindExistingState(Symbol sym, List<ProductionItem> itemSet)
		{
			if (accessedBy.ContainsKey(sym))
				foreach (AutomatonState state in accessedBy[sym])
					if (ProductionItem.SameProductions(state.kernelItems, itemSet))
						return state;

			return null;
		}




		internal void BuildParseTable()
		{
			foreach (AutomatonState state in states)
			{
				// Add shift actions ...
				foreach (Terminal t in state.terminalTransitions)
					state.parseTable[t] = new Shift(state.Goto[t]);

				// Add reduce actions ...
				foreach (ProductionItem item in state.allItems)
					if (item.isReduction())
					{
						// Accept on everything
						if (item.production == grammar.rootProduction)
							foreach (Terminal t in grammar.terminals.Values)
								state.parseTable[t] = new Reduce(item);

						foreach (Terminal t in item.LA)
						{
							// possible conflict with existing action
							if (state.parseTable.ContainsKey(t))
							{
                                Reduce reduceAction;
								ParserAction other = state.parseTable[t];
                                Production iProd = item.production;
								if ((reduceAction = other as Reduce)!= null)
                                {
                                    Production oProd = reduceAction.item.production;

                                    // Choose in favour of production listed first in the grammar
                                    if (oProd.num > iProd.num)
                                        state.parseTable[t] = new Reduce(item);

                                    string p1 = String.Format(CultureInfo.InvariantCulture, " Reduce {0}:\t{1}", oProd.num, oProd.ToString());
                                    string p2 = String.Format(CultureInfo.InvariantCulture, " Reduce {0}:\t{1}", iProd.num, iProd.ToString());
                                    int chsn = (oProd.num > iProd.num ? iProd.num : oProd.num);
                                    grammar.conflicts.Add(new ReduceReduceConflict(t, p1, p2, chsn, state));
                                    if (GPCG.Verbose)
                                    {
                                        Console.Error.WriteLine(
                                            "Reduce/Reduce conflict in state {0} on symbol {1}",
                                            state.num,
                                            t.ToString());
                                        Console.Error.WriteLine(p1);
                                        Console.Error.WriteLine(p2);
                                    }
                                    else
                                        Console.Error.WriteLine("Reduce/Reduce conflict, state {0}: {1} vs {2} on {3}",
                                                                                    state.num, iProd.num, oProd.num, t);
                                }
								else
								{
                                    if (iProd.prec != null && t.prec != null)
                                    {
                                        if (iProd.prec.prec > t.prec.prec ||
                                            (iProd.prec.prec == t.prec.prec &&
                                             iProd.prec.type == PrecType.left))
                                        {
                                            // resolve in favour of reduce (without error)
                                            state.parseTable[t] = new Reduce(item);
                                        }
                                        else
                                        {
                                            // resolve in favour of shift (without error)
                                        }
                                    }
                                    else
                                    {
                                        AutomatonState next = ((Shift)other).next;
                                        string p1 = String.Format(CultureInfo.InvariantCulture, " Shift \"{0}\":\tState-{1} -> State-{2}", t, state.num, next.num);
                                        string p2 = String.Format(CultureInfo.InvariantCulture, " Reduce {0}:\t{1}", iProd.num, iProd.ToString());
                                        grammar.conflicts.Add(new ShiftReduceConflict(t, p1, p2, state, next));
                                        if (GPCG.Verbose)
                                        {
                                            Console.Error.WriteLine("Shift/Reduce conflict");
                                            Console.Error.WriteLine(p1);
                                            Console.Error.WriteLine(p2);
                                        }
                                        else
                                            Console.Error.WriteLine("Shift/Reduce conflict, state {0} on {1}", state.num, t);
                                    }
									// choose in favour of the shift
								}
							}
							else
								state.parseTable[t] = new Reduce(item);
						}
					}
			}
		}
	}

    // ===========================================================
    #region Diagnostics
    /// <summary>
    /// Class for determining input token sequences that
    /// lead to each state by the shortest token sequence.
    /// The corresponding sequence for each NonTerminal is
    /// already computed in Grammar.MarkTerminating() as a
    /// side-effect of detecting non-terminating NonTerms.
    /// </summary>
    internal static class DiagnosticHelp
    {
        private static List<T> ListClone<T>(List<T> list)
        {
            List<T> rslt = new List<T>(list.Count + 1);
            for (int i = 0; i < list.Count; i++)
                rslt.Add(list[i]);
            return rslt;
        }

        internal static void PopulatePrefixes(List<AutomatonState> states)
        {
            AutomatonState start = states[0];
            start.shortestPrefix = new List<Symbol>(); // The empty list.
            start.statePath = new List<AutomatonState>();
            start.statePath.Add(start);

            bool changed = false;
            do
            {
                changed = false;
                foreach (AutomatonState state in states)
                {
                    List<Symbol> newfix;
                    List<Symbol> prefix = state.shortestPrefix;
                    List<AutomatonState> newPath;
                    List<AutomatonState> oldPath = state.statePath;

                    if (prefix != null)
                    {
                        foreach (KeyValuePair<Symbol, AutomatonState> a in state.Goto)
                        {
                            Symbol smbl = a.Key;
                            AutomatonState nextState = a.Value;
                            newfix = ListClone<Symbol>(prefix);
                            newPath = ListClone<AutomatonState>(oldPath);

                            newPath.Add(nextState);
                            if (!smbl.IsNullable())
                                newfix.Add(smbl);
                            if (nextState.shortestPrefix == null ||
                                nextState.shortestPrefix.Count > newfix.Count)
                            {
                                nextState.shortestPrefix = newfix;
                                nextState.statePath = newPath;
                                changed = true;
                            }
                        }
                    }
                }
            } while (changed);
        }
    }
    #endregion
    // ===========================================================
}