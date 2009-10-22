// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2007
// (see accompanying GPPGcopyright.rtf)


using System;
using System.Collections.Generic;


namespace QUT.GPGen
{
	internal class LALRGenerator: LR0Generator
	{
		internal LALRGenerator(Grammar grammar): base(grammar)
		{
		}


		internal void ComputeLookAhead()
		{
			ComputeDRs();
			ComputeReads();
			ComputeIncludes();
			ComputeFollows();
			ComputeLA();
		}


		private void ComputeDRs()
		{
			// DR(p,A) = { t | p -> A -> r -> t -> ? }

			foreach (AutomatonState p in states)
				foreach (Transition pA in p.nonTerminalTransitions.Values)
					pA.DR = pA.next.terminalTransitions;
		}


		private Stack<Transition> S;

		// DeRemer and Pennello algorithm to compute Reads
		private void ComputeReads()
		{
			S = new Stack<Transition>();

			foreach (AutomatonState ps in states)
				foreach (Transition x in ps.nonTerminalTransitions.Values)
					x.N = 0;

			foreach (AutomatonState ps in states)
				foreach (Transition x in ps.nonTerminalTransitions.Values)
					if (x.N == 0)
						TraverseReads(x, 1);
		}


		private void TraverseReads(Transition x, int k)
		{
			S.Push(x);
			x.N = k;
			x.Read = new SetCollection<Terminal>(x.DR);

			// foreach y such that x reads y
			foreach (Transition y in x.next.nonTerminalTransitions.Values)
				if (y.A.IsNullable())
				{
					if (y.N == 0)
						TraverseReads(y, k + 1);

					if (y.N < x.N)
						x.N = y.N;

					x.Read.AddRange(y.Read);
				}

			if (x.N == k)
				do
				{
					S.Peek().N = int.MaxValue;
					S.Peek().Read = new SetCollection<Terminal>(x.Read);
				} while (S.Pop() != x);
		}


		private void ComputeIncludes()
		{
			// (p,A) include (q,B) iff B -> Beta A Gamma and Gamma => empty and q -> Beta -> p

			foreach (AutomatonState q in states)
				foreach (Transition qB in q.nonTerminalTransitions.Values)
					foreach (Production prod in qB.A.productions)
					{
						for (int i = prod.rhs.Count - 1; i >= 0; i--)
						{
							Symbol A = prod.rhs[i];
                            NonTerminal NT = A as NonTerminal;
							if (NT != null)
							{
								AutomatonState p = PathTo(q, prod, i);
								p.nonTerminalTransitions[NT].includes.Add(qB);
							}

							if (!A.IsNullable())
								break;
						}
					}
		}


		private static AutomatonState PathTo(AutomatonState q, Production prod, int prefix)
		{
			// q -> prod.rhs[0] ... prod.rhs[prefix] -> ???

			for (int i = 0; i < prefix; i++)
			{
				Symbol s = prod.rhs[i];
				if (q.Goto.ContainsKey(s))
					q = q.Goto[s];
				else
					return null;
			}

			return q;
		}


		// DeRemer and Pennello algorithm to compute Follows
		private void ComputeFollows()
		{
			S = new Stack<Transition>();

			foreach (AutomatonState ps in states)
				foreach (Transition x in ps.nonTerminalTransitions.Values)
					x.N = 0;

			foreach (AutomatonState ps in states)
				foreach (Transition x in ps.nonTerminalTransitions.Values)
					if (x.N == 0)
						TraverseFollows(x, 1);
		}


		private void TraverseFollows(Transition x, int k)
		{
			S.Push(x);
			x.N = k;
			x.Follow = new SetCollection<Terminal>(x.Read);

			foreach (Transition y in x.includes)
				if (x != y)
				{
					if (y.N == 0)
						TraverseFollows(y, k + 1);

					if (y.N < x.N)
						x.N = y.N;

					x.Follow.AddRange(y.Follow);
				}

			if (x.N == k)
				do
				{
					S.Peek().N = int.MaxValue;
					S.Peek().Follow = new SetCollection<Terminal>(x.Follow);
				} while (S.Pop() != x);
		}


		private void ComputeLA()
		{
			// LA(q, A->w) = Union { Follow(p,A) | p -> w -> q }

			foreach (AutomatonState q in states)
			{
				foreach (ProductionItem item in q.allItems)
				{
					if (item.isReduction())
					{
						item.LA = new SetCollection<Terminal>();
						foreach (AutomatonState p in states)
							if (PathTo(p, item.production, item.pos) == q)
							{
								NonTerminal A = item.production.lhs;
								if (p.nonTerminalTransitions.ContainsKey(A))
								{
									Transition pA = p.nonTerminalTransitions[A];
									item.LA.AddRange(pA.Follow);
								}
							}
					}
				}
			}
		}
	}
}