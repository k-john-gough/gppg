// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2007
// (see accompanying GPPGcopyright.rtf)


using System;
using System.Collections.Generic;


namespace QUT.GPGen
{
	internal class Transition
	{
        // The start state of this transition is known
        // implicitly from the dictionary from which 
        // this Transition was extracted.
		internal int N;
		internal NonTerminal A;
		internal AutomatonState next;

		internal SetCollection<Terminal> DR;
		internal List<Transition> includes = new List<Transition>();
		internal SetCollection<Terminal> Read;
		internal SetCollection<Terminal> Follow;


		internal Transition(NonTerminal A, AutomatonState next)
		{
			this.A = A;
			this.next = next;
		}
	}
}