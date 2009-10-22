// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2007
// (see accompanying GPPGcopyright.rtf)



using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using QUT.GPGen.Lexers;
using QUT.GPGen.Parser;


namespace QUT.GPGen
{
	internal class Grammar
	{
        internal const string DefaultValueTypeName = "ValueType";
        internal const int LineLength = 80;

        private int currentPrec;
        internal void BumpPrec() { currentPrec += 10; }
        internal int Prec { get { return currentPrec; } }

		internal List<Production> productions = new List<Production>();
        internal LexSpan unionType;
		internal int NumActions;
		internal List<LexSpan> prologCode = new List<LexSpan>();	// before first %%
        internal LexSpan epilogCode;	// after last %%
		internal NonTerminal startSymbol;
		internal Production rootProduction;
		internal Dictionary<string, NonTerminal> nonTerminals = new Dictionary<string, NonTerminal>();
		internal Dictionary<string, Terminal> terminals = new Dictionary<string, Terminal>();
        internal Dictionary<string, Terminal> aliasTerms = new Dictionary<string, Terminal>();
        internal List<string> usingList = new List<string>();
        internal List<Conflict> conflicts = new List<Conflict>();

        internal bool IsPartial;
        internal string OutFileName;
        internal string TokFileName;
        internal string DiagFileName;
        internal string InputFileName;
        internal string Namespace;
        internal string Visibility = "public";
        internal string ParserName = "Parser";
        internal string TokenName = "Tokens";
        internal string ScanBaseName = "ScanBase";
        internal string ValueTypeName;
        internal string LocationTypeName = "LexLocation";
        internal string PartialMark { get { return (IsPartial ? " partial" : ""); } }
        internal LexSpan ValueTypeNameSpan;

        // Experimental features
        // readonly List<Terminal> emptyTerminalList;
        ErrorHandler handler;
        bool hasNonTerminatingNonTerms;

        internal bool HasNonTerminatingNonTerms { 
            get { return hasNonTerminatingNonTerms; } 
        }
        // end

        internal Grammar()
        {
			LookupTerminal(Token.ident, "error");
			LookupTerminal(Token.ident, "EOF");
            // emptyTerminalList = new List<Terminal>();
        }


        internal Terminal LookupTerminal(Token token, string name)
        {
            bool isIdent = (token == Token.ident);
            // Canonicalize escaped char-literals
            if (!isIdent)
                name = CharacterUtilities.Canonicalize(name, 1);
            // Check if already present in dictionary
            if (!terminals.ContainsKey(name)) // else insert ...
                terminals[name] = new Terminal(isIdent, name);

            return terminals[name];
        }

        internal Terminal LookupOrDefineTerminal(Token token, string name, string alias)
        {
            bool isIdent = (token == Token.ident);
            // Canonicalize escaped char-literals
            if (!isIdent)
                name = CharacterUtilities.Canonicalize(name, 1);
            // Check if already present in dictionary
            if (!terminals.ContainsKey(name)) // else insert ...
            {
                Terminal newTerm = new Terminal(isIdent, name, alias);
                terminals[name] = newTerm;
                if (alias != null)
                    aliasTerms[alias] = newTerm;
            }

            return terminals[name];
        }


		internal NonTerminal LookupNonTerminal(string name)
		{
			if (!nonTerminals.ContainsKey(name))
				nonTerminals[name] = new NonTerminal(name);

			return nonTerminals[name];
		}


		internal void AddProduction(Production production)
		{
			productions.Add(production);
			production.num = productions.Count;
		}


		internal void CreateSpecialProduction(NonTerminal root)
		{
			rootProduction = new Production(LookupNonTerminal("$accept"));
			AddProduction(rootProduction);
			rootProduction.rhs.Add(root);
            rootProduction.rhs.Add(LookupTerminal(Token.ident, "EOF"));
		}

        void MarkReachable()
        {
            Stack<NonTerminal> work = new Stack<NonTerminal>();
            rootProduction.lhs.reached = true; // by definition.
            work.Push(startSymbol);
            startSymbol.reached = true;
            while (work.Count > 0)
            {
                NonTerminal nonT = work.Pop();
                foreach (Production prod in nonT.productions)
                {
                    foreach (Symbol smbl in prod.rhs)
                    {
                        NonTerminal rhNt = smbl as NonTerminal;
                        if (rhNt != null && !rhNt.reached)
                        {
                            rhNt.reached = true;
                            work.Push(rhNt);
                        }
                    }
                }
            }
        }


        // =============================================================================
        #region Terminating Computation
        // =============================================================================

        const int finishMark = int.MaxValue;

        /// <summary>
        /// This is the method that computes the shortest terminal
        /// string sequence for each NonTerminal symbol.  The immediate
        /// guide is to find those NT that are non-terminating.
        /// </summary>
        void MarkTerminating()
        {
            bool changed = false;
            int nonTerminatingCount = 0;
            // This uses a naive algorithm that iterates until
            // an iteration completes without changing anything.
            do
            {
                changed = false;
                nonTerminatingCount = 0;
                foreach (KeyValuePair<string, NonTerminal> kvp in this.nonTerminals)
                {
                    NonTerminal nonTerm = kvp.Value;
                    if (!nonTerm.terminating)
                    {
                        foreach (Production prod in nonTerm.productions)
                            if (ProductionTerminates(prod))
                            {
                                nonTerm.terminating = true;
                                changed = true;
                            }
                        if (!nonTerm.terminating)
                            nonTerminatingCount++;
                    }
                }
            } while (changed);
            //
            // Now produce some helpful diagnostics.
            // We wish to find single NonTerminals that, if made
            // terminating will fix up many, even all of the
            // non-terminating NonTerminals that have been found.
            //
            if (nonTerminatingCount > 0)
            {
                List<NonTerminal> ntDependencies = BuildDependencyGraph();
                hasNonTerminatingNonTerms = true;
                handler.AddError(
                    String.Format(CultureInfo.InvariantCulture, "There are {0} non-terminating NonTerminal Symbols{1} {{{2}}}",
                        nonTerminatingCount,
                        System.Environment.NewLine,
                        ListUtilities.GetStringFromList(ntDependencies)), null);

                FindNonTerminatingSCC(ntDependencies); // Do some diagnosis
            }
        }

        private static bool ProductionTerminates(Production thisProd)
        {
            foreach (Symbol smbl in thisProd.rhs)
            {
                NonTerminal nonTerm = smbl as NonTerminal;
                if (nonTerm != null && !nonTerm.terminating)
                    return false;
            }
            return true;
        }

        //
        // NonTerminals that are non-terminating are usually so because
        // they depend on other NonTerms that are themselves non-terminating.
        // We first construct a graph modelling these dependencies, and then
        // find strongly connected regions in the dependency graph.
        //
        private void FindNonTerminatingSCC(List<NonTerminal> ntDependencies)
        {
            int count = 0;
            // ntStack is the working stack used to find Strongly Connected 
            // Components, hereafter referred to as SCC.
            Stack<NonTerminal> ntStack = new Stack<NonTerminal>();

            // candidates is the list of states that *might* be to blame.
            // These are two groups: leaves of the dependency graph, and
            // NonTerminals that fix up a complete SCC
            List<NonTerminal> candidates = new List<NonTerminal>();
            foreach (NonTerminal nt in ntDependencies)
            {
                if (nt.dependsOnList.Count == 0)
                    candidates.Add(nt);
                else if (nt.depth != finishMark)
                    Walk(nt, ntStack, candidates, ref count);
            }
            foreach (NonTerminal candidate in candidates)
                LeafExperiment(candidate, ntDependencies);
        }

        private void Walk(NonTerminal node, Stack<NonTerminal> stack, List<NonTerminal> fixes, ref int count)
        {
            count++;
            stack.Push(node);
            node.depth = count;
            foreach (NonTerminal next in node.dependsOnList)
            {
                if (next.depth == 0)
                    Walk(next, stack, fixes, ref count);
                if (next.depth < count)
                    node.depth = next.depth;
            }
            if (node.depth == count) // traversal leaving strongly connected component
            {
                // This algorithm is folklore. I have been using it since
                // at least early 1980s in the Gardens Point compilers.
                // I don't even remember where I learned it ... (kjg).
                //
                NonTerminal popped = stack.Pop();
                popped.depth = finishMark;
                if (popped != node)
                {
                    List<NonTerminal> SCC = new List<NonTerminal>();
                    SCC.Add(popped);
                    do
                    {
                        popped = stack.Pop();
                        popped.depth = finishMark;
                        SCC.Add(popped);
                    } 
                    while (popped != node);
                    handler.AddWarning(String.Format(CultureInfo.InvariantCulture,
                        "The following {2} symbols form a non-terminating cycle {0}{{{1}}}",
                        System.Environment.NewLine,
                        ListUtilities.GetStringFromList(SCC),
                        SCC.Count), null);
                    //
                    // Check if termination of any single NonTerminal
                    // would eliminate the whole cycle of dependency.
                    //
                    SccExperiment(SCC, fixes);
                }
            }
            count--;
        }

        // Return a new list with only the terminating (fixed) elements of the input.
        private static List<NonTerminal> FilterTerminatingElements(List<NonTerminal> input)
        {
            List<NonTerminal> rslt = new List<NonTerminal>();
            foreach (NonTerminal nt in input)
                if (nt.terminating)
                    rslt.Add(nt);
            return rslt;
        }

        private List<NonTerminal> BuildDependencyGraph()
        {
            List<NonTerminal> rslt = new List<NonTerminal>();
            foreach (KeyValuePair<string, NonTerminal> kvp in this.nonTerminals)
            {
                NonTerminal nonTerm = kvp.Value;
                NonTerminal dependency = null;
                if (!nonTerm.terminating)
                {
                    rslt.Add(nonTerm);
                    nonTerm.dependsOnList = new List<NonTerminal>();
                    foreach (Production prod in nonTerm.productions)
                        foreach (Symbol symbol in prod.rhs)
                        {
                            dependency = symbol as NonTerminal;
                            if (dependency != null &&
                                dependency != nonTerm &&
                                !dependency.terminating &&
                                !nonTerm.dependsOnList.Contains(dependency))
                            {
                                nonTerm.depth = 0;
                                nonTerm.dependsOnList.Add(dependency);
                            }
                        }

                }
            }
            return rslt;
        }

        private static void SccExperiment(List<NonTerminal> component, List<NonTerminal> fixes)
        {
            foreach (NonTerminal probe in component)
            {
                // Test what happens with probe nullable ...
                probe.terminating = true;
                SccPropagate(probe, component, fixes);
                // Then reset the values of all components
                foreach (NonTerminal element in component)
                    element.terminating = false;
            }
        }

        private static void SccPropagate(NonTerminal root, List<NonTerminal> thisTestConfig, List<NonTerminal> fixes)
        {
            int count = 0;
            bool changed = false;
            do
            {
                count = 0;
                changed = false;
                foreach (NonTerminal nt in thisTestConfig)
                {
                    if (!nt.terminating)
                    {
                        foreach (Production prod in nt.productions)
                            if (ProductionTerminates(prod))
                            {
                                nt.terminating = true;
                                changed = true;
                            }
                        if (!nt.terminating)
                            count++;
                    }
                }
            }
            while (changed);
            if (count == 0)
                fixes.Add(root);
        }

        private void LeafExperiment(NonTerminal probe, List<NonTerminal> component)
        {
                // Test what happens with probe terminating ...
                probe.terminating = true;
                LeafPropagate(probe, component);
                // Then reset the values of all components
                foreach (NonTerminal element in component)
                    element.terminating = false;
        }



        private void LeafPropagate(NonTerminal root, List<NonTerminal> thisTestConfig)
        {
            int count = 0;
            bool changed = false;
            do
            {
                count = 0;
                changed = false;
                foreach (NonTerminal nt in thisTestConfig)
                {
                    if (!nt.terminating)
                    {
                        foreach (Production prod in nt.productions)
                            if (ProductionTerminates(prod))
                            {
                                nt.terminating = true;
                                changed = true;
                            }
                        if (!nt.terminating)
                            count++;
                    }
                }
            }
            while (changed);

            List<NonTerminal> filtered = FilterTerminatingElements(thisTestConfig);
            handler.AddWarning(String.Format(CultureInfo.InvariantCulture,
                        "Terminating {0} fixes the following size-{1} NonTerminal set{2}{{{3}}}",
                        root.ToString(),
                        filtered.Count,
                        System.Environment.NewLine,
                        ListUtilities.GetStringFromList(filtered)), null);
        }

        // =============================================================================
        #endregion Terminating Computation
        // =============================================================================

        internal bool CheckGrammar(ErrorHandler handler)
        {
            bool ok = true;
            NonTerminal nt;
            this.handler = handler;
            MarkReachable();
            MarkTerminating();
            foreach (KeyValuePair<string, NonTerminal> pair in nonTerminals)
            {
                nt = pair.Value;
                if (!nt.reached)
                    handler.AddWarning(String.Format(CultureInfo.InvariantCulture,
                        "NonTerminal symbol \"{0}\" is unreachable", pair.Key), null);

                if (nt.productions.Count == 0)
                {
                    ok = false;
                    handler.AddError(String.Format(CultureInfo.InvariantCulture,
                        "NonTerminal symbol \"{0}\" has no productions", pair.Key), null);
                }
            }
            if (this.HasNonTerminatingNonTerms) ok = false;
            return ok;    
        }

        internal void ReportConflicts(StreamWriter wrtr)
        {
            if (wrtr == null)
                return;
            foreach (Conflict theConflict in conflicts)
                theConflict.Report(wrtr);
        }

        // =====================================================================================
        #region ReportWriter
        // =====================================================================================


        internal void GenerateReport(StreamWriter writer, string inputFilename, List<AutomatonState> statelist)
        {
            writer.WriteLine("<b>Grammar {0}</b>", inputFilename);
            WriteProductions(writer);

            foreach (AutomatonState state in statelist)
            {
                writer.WriteLine(StateAnchor(state.num));
                writer.WriteLine(StateToString(state));
            }
        }

        internal void GenerateCompoundReport(StreamWriter writer, string inputFilename, List<AutomatonState> statelist)
        {
            writer.WriteLine("<b>Grammar and Diagnosis {0}</b>", inputFilename);

            WriteProductions(writer);
            DiagnosticHelp.PopulatePrefixes(statelist);
            Mapper<string, AutomatonState> map = delegate(AutomatonState elemState) { return StateRef(elemState.num); };

            foreach (AutomatonState state in statelist)
            {
                writer.WriteLine(StateAnchor(state.num));
                DiagnoseState(writer, state, map, false);
                writer.WriteLine(StateToString(state));
            }
        }

        static void DiagnoseState<T>(StreamWriter writer, AutomatonState state, Mapper<T, AutomatonState> map, bool doKernel)
        {
            // List<T> statePath = ListUtilities.Map<T, AutomatonState>(state.statePath, map);
            IEnumerable<T> statePath = ListUtilities.MapC<T, AutomatonState>(state.statePath, map);

            writer.WriteLine("    Shortest prefix: {0}", ListUtilities.GetStringFromList(state.shortestPrefix, " ", 8));
            writer.WriteLine("    State path: {0}", ListUtilities.GetStringFromList(statePath, "->", 8, false));
            if (state.conflicts != null)
            {
                writer.WriteLine();
                writer.WriteLine("    <b>Conflicts in this state</b>");
                foreach (Conflict conflict in state.conflicts)
                {
                    conflict.HtmlReport(writer);
                }
            }
            if (doKernel)
            {
                writer.WriteLine("    Kernel items --");
                foreach (ProductionItem item in state.kernelItems)
                    writer.WriteLine("      {0}", ItemToString(item, false));
            }
            writer.WriteLine();
        }

        void WriteProductions(StreamWriter writer)
        {
            NonTerminal lhs = null;

            foreach (Production production in productions)
            {
                int lhsLength = production.lhs.ToString().Length;

                if (production.lhs != lhs)
                {
                    lhs = production.lhs;
                    writer.WriteLine();
                    writer.Write("{0} {1}: ", ProductionAnchor(production.num), lhs);
                }
                else
                    writer.Write("{0} {1}| ", ProductionAnchor(production.num), new string(' ', lhsLength));

                if (production.rhs.Count == 0)
                    writer.WriteLine("/* empty */");
                else
                    writer.WriteLine(ListUtilities.GetStringFromList(production.rhs, " ", lhsLength + 12));
            }

            writer.WriteLine();
        }

        static string StateToString(AutomatonState thisState)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            builder.AppendLine(Header2("Kernel Items"));
            foreach (ProductionItem item in thisState.kernelItems)
            {
                builder.AppendFormat("    {0}", ItemToString(item, true));
                builder.AppendLine();
            }

            builder.AppendLine();

            if (thisState.parseTable.Count > 0)
                builder.AppendLine(Header2("Parser Actions"));
            foreach (KeyValuePair<Terminal, ParserAction> a in thisState.parseTable)
            {
                builder.AppendFormat("    {0,-14} {1}", a.Key, ActionToString(a.Value));
                builder.AppendLine();
            }

            builder.AppendLine();

            if (thisState.nonTerminalTransitions.Count > 0)
                builder.AppendLine(Header2("Transitions"));
            foreach (KeyValuePair<NonTerminal, Transition> n in thisState.nonTerminalTransitions)
            {
                builder.AppendFormat("    {0,-14} go to state {1}", n.Key, StateRef(thisState.Goto[n.Key].num));
                builder.AppendLine();
            }

            builder.AppendLine();

            return builder.ToString();
        }

        static string ActionToString(ParserAction action)
        {
            string result = null;
            Shift shift = action as Shift;
            if (shift != null)
                return "shift, and go to state " + StateRef(shift.next.num);
            Reduce reduce = action as Reduce;
            if (reduce != null)
                return String.Format(CultureInfo.InvariantCulture, "reduce using {0} ({1}{2})",
                    ProductionRef(reduce.item.production.num),
                    (reduce.item.production.rhs.Count == 0 ? "Erasing " : ""),
                    reduce.item.production.lhs);
            return result;
        }

        static string ItemToString(ProductionItem item, bool doLA)
        {
            int lhsLength;
            List<string> list = new List<string>();
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            builder.AppendFormat("{0} {1}: ", item.production.num, item.production.lhs);
            lhsLength = builder.Length;

            for (int i = 0; i < item.production.rhs.Count; i++)
            {
                if (i == item.pos)
                    list.Add(".");
                list.Add(item.production.rhs[i].ToString());
            }

            if (item.pos == item.production.rhs.Count)
                list.Add(".");

            builder.Append(ListUtilities.GetStringFromList(list, " ", lhsLength + 6));

            if (item.LA != null && doLA)
            {
                builder.AppendLine();
                builder.AppendFormat("\t-lookahead: {{ {0} }}", ListUtilities.GetStringFromList(item.LA, ", ", 16));
            }

            return builder.ToString();
        }


        static internal void HtmlHeader(StreamWriter wrtr, string name)
        {
            wrtr.WriteLine("<html><head><title>{0}</title></head>", name);
            wrtr.WriteLine("<body bgcolor=\"white\">");
            wrtr.WriteLine("<hr><pre>");
        }

        static internal void HtmlTrailer(StreamWriter wrtr)
        {
            wrtr.WriteLine("</font></pre></hr></body></html>");
        }

        static string ProductionAnchor(int prodNum)
        {
            return String.Format(CultureInfo.InvariantCulture, "<a name=\"prod{0}\">{0,5}</a>", prodNum);
        }

        static string ProductionRef(int prodNum)
        {
            return String.Format(CultureInfo.InvariantCulture, "<a href=\"#prod{0}\">rule {0}</font></a>", prodNum);
        }

        static string StateAnchor(int stateNum)
        {
            // return String.Format(CultureInfo.InvariantCulture, "<b><a name=\"state{0}\">State {0}</a></b>", stateNum);
            return String.Format(CultureInfo.InvariantCulture, "<b><a name=\"state{0}\">State</a> {1}</b>", stateNum, StateRef(stateNum));
        }

        static string StateRef(int stateNum)
        {
            return String.Format(CultureInfo.InvariantCulture, "<a href=\"#state{0}\">{0}</font></a>", stateNum);
        }

        static string Header2(string display)
        {
            return String.Format(CultureInfo.InvariantCulture, "  <b>{0}</b>", display);
        }

        // =====================================================================================
        #endregion // ReportWriter
        // =====================================================================================
    }

    #region Conflict Diagnostics

    internal abstract class Conflict
    {
        protected Terminal symbol;
        protected string str1 = null;
        protected string str2 = null;
        internal Conflict(Terminal sy, string s1, string s2) { symbol = sy; str1 = s1; str2 = s2; }

        internal abstract void Report(StreamWriter w);
        internal abstract void HtmlReport(StreamWriter w);
    }

    internal class ReduceReduceConflict : Conflict
    {
        int chosen;
        AutomatonState inState;

        internal ReduceReduceConflict(Terminal sy, string s1, string s2, int prod, AutomatonState state) 
            : base(sy, s1, s2)
        { 
            chosen = prod;
            inState = state;
            state.Link(this);
        }

        internal override void Report(StreamWriter wrtr)
        {
            wrtr.WriteLine(
                "Reduce/Reduce conflict in state {0} on symbol \"{1}\", parser will reduce production {2}",
                inState.num,
                symbol.ToString(),
                chosen);
            wrtr.WriteLine(str1);
            wrtr.WriteLine(str2);
            wrtr.WriteLine();
        }

        internal override void HtmlReport(StreamWriter wrtr)
        {
            wrtr.WriteLine(
                "      Reduce/Reduce conflict on symbol \"{0}\", parser will reduce production {1}",
                symbol.ToString(),
                chosen);
            wrtr.WriteLine("      " + str1);
            wrtr.WriteLine("      " + str2);
            wrtr.WriteLine("      ---------");
        }
    }

    internal class ShiftReduceConflict : Conflict
    {
        AutomatonState fromState;
        AutomatonState toState;
        internal ShiftReduceConflict(Terminal sy, string s1, string s2, AutomatonState from, AutomatonState to)
            : base(sy, s1, s2)
        { 
            fromState = from; toState = to;
            fromState.Link(this);
        }

        internal override void Report(StreamWriter wrtr)
        {
            wrtr.WriteLine("Shift/Reduce conflict on symbol \"{0}\", parser will shift", symbol.ToString());
            wrtr.WriteLine(str2);
            wrtr.WriteLine(str1);
            wrtr.Write("  Items for From-state ");
            wrtr.WriteLine(fromState.ItemDisplay());
            wrtr.Write("  Items for Next-state ");
            wrtr.WriteLine(toState.ItemDisplay());
            wrtr.WriteLine();
        }

        internal override void HtmlReport(StreamWriter wrtr)
        {
            wrtr.WriteLine("      Shift/Reduce conflict on symbol \"{0}\", parser will shift", symbol.ToString());
            wrtr.WriteLine("      " + str1);
            wrtr.WriteLine("      " + str2);
            wrtr.WriteLine("      ---------");
        }

    }

    #endregion

}







