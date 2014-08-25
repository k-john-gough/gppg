
// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2014
// (see accompanying GPPGcopyright.rtf)


using System;
using System.Text;
using System.Collections.Generic;


namespace QUT.GPGen
{
    [Serializable]
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

		public override string ToString()
		{
			return name;
		}


		internal abstract bool IsNullable();
	}

    [Serializable]
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

        internal void AddAlias( string alias ) {
            if (this.alias == null)
                this.alias = alias;
        }

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
        /// <param name="symbolic">Means "is an ident, not a literal character"</param>
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

        internal static readonly Terminal Ambiguous = new Terminal( true, "$Ambiguous$" );

        internal Terminal(bool symbolic, string name, string alias) 
            : this(symbolic, name)
        {
            if (alias != null)
                this.alias = alias;
        }


		internal override bool IsNullable() { return false;	}

        internal string EnumName() { return base.ToString(); }

        public override string ToString()
        {
            if (this.alias != null)
                return CharacterUtilities.QuoteAndCanonicalize( this.alias );
            else 
                return base.ToString();
        }

        public string BaseString() {
            return base.ToString();
        }

        internal static void InsertMaxDummyTerminalInDictionary( Dictionary<string, Terminal> table ) {
            Terminal maxTerm = null;
            if (Terminal.Max != 0) {
                string maxChr = CharacterUtilities.QuoteMap( Terminal.Max ); // FIXME
                maxTerm = table[maxChr];
            }
            table["@Max@"] = maxTerm;
        }

        internal static void RemoveMaxDummyTerminalFromDictionary( Dictionary<string, Terminal> table ) {
            Terminal maxTerm = table["@Max@"];
            max = (maxTerm != null ? maxTerm.n : 0);
            table.Remove( "@Max@" );
        }

        internal static bool BumpsMax( string str ) {
            string num = CharacterUtilities.CanonicalizeCharacterLiteral( str, 1 );
            int ord = CharacterUtilities.OrdinalOfCharacterLiteral( str, 1 );
            return ord > Terminal.max;
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