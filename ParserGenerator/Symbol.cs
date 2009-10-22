
// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2007
// (see accompanying GPPGcopyright.rtf)


using System;
using System.Text;
using System.Collections.Generic;


namespace QUT.GPGen
{
	internal abstract class Symbol
	{
		private string name;
		internal string kind;

		internal abstract int num
		{
			get;
		}

		internal Symbol(string name)
		{
			this.name = name;
		}

        //protected void Rename(string newname)
        //{
        //    this.name = newname;
        //}


		public override string ToString()
		{
			return name;
		}


		internal abstract bool IsNullable();
	}


	internal class Terminal : Symbol
	{
        static int count;
		static int max;
        internal static int Max { get { return max; } }

		internal Precedence prec;
		private int n;
		internal bool symbolic;
        private string alias;

        internal string Alias { get { return alias; } }

		internal override int num
		{
			get
			{
				if (symbolic)
					return max + n;
				else
					return n;
			}
		}

        /// <summary>
        /// If name is an escaped char-lit, it must already be
        /// canonicalized according to some convention. In this 
        /// application CharUtils.Canonicalize().
        /// </summary>
        /// <param name="symbolic">Means "is an ident"</param>
        /// <param name="name">string representation of symbol</param>
		internal Terminal(bool symbolic, string name)
        	: base(name)
        {
			this.symbolic = symbolic;
			if (symbolic)
				this.n = ++count;
			else
			{
				this.n = CharacterUtilities.OrdinalOfCharacterLiteral(name, 1);
				if (n > max) max = n;
			}
		}

        internal Terminal(bool symbolic, string name, string alias) 
            : this(symbolic, name)
        {
            if (alias != null)
                this.alias = alias;
        }


		internal override bool IsNullable()
		{
			return false;
		}

        public override string ToString()
        {
            if (this.alias != null)
                return this.alias;
            else 
                return base.ToString();
        }

        internal string EnumName()
        {
            return base.ToString();
        }
	}



	internal class NonTerminal : Symbol
	{
        internal bool reached;

        // Start experimental features
        internal List<NonTerminal> dependsOnList;
        internal int depth;
        internal bool terminating;
        // end

        static int count;
		private int n;
		internal List<Production> productions = new List<Production>();

		internal NonTerminal(string name)
			: base(name)
		{
            n = ++count;
		}

		internal override int num
		{
			get
			{
				return -n;
			}
		}

		private object isNullable;
		internal override bool IsNullable()
		{
			if (isNullable == null)
			{
				isNullable = false;
				foreach (Production p in productions)
				{
					bool nullable = true;
					foreach (Symbol rhs in p.rhs)
						if (!rhs.IsNullable())
						{
							nullable = false;
							break;
						}
					if (nullable)
					{
						isNullable = true;
						break;
					}
				}
			}

			return (bool)isNullable;
		}
	}
}