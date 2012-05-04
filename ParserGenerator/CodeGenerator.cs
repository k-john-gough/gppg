// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2010
// (see accompanying GPPGcopyright.rtf)

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Text;
using System.Globalization;
using QUT.GPGen.Parser;


namespace QUT.GPGen
{
    internal class CodeGenerator
    {
		internal Grammar grammar;

        internal void Generate(List<AutomatonState> states, Grammar grammar)
        {
            StreamWriter tWrtr = null;
            StreamWriter sWrtr = null;
            StreamWriter cWrtr = null;
            TextWriter   save = Console.Out;

			this.grammar = grammar;

            // did we specify out filename in command line or the file? (command line takes precedence)
            string outFile = GPCG.OutFileName;
            if (outFile == null)
              outFile = grammar.OutFileName;

            if (outFile != null)
            {
                try
                {
                    FileStream fStrm = new FileStream(outFile, FileMode.Create);
                    sWrtr = new StreamWriter(fStrm);
                    Console.WriteLine("GPPG: sending output to {0}", outFile);
                    Console.SetOut(sWrtr);
                }
                catch (IOException x)
                {
                    Console.Error.WriteLine("GPPG: Error. File redirect failed");
                    Console.Error.WriteLine(x.Message);
                    Console.Error.WriteLine("GPPG: Terminating ...");
                    Environment.Exit(1);
                }
            }

            if (grammar.TokFileName != null) // generate token list file
            {
                try {
                    FileStream fStrm = new FileStream( grammar.TokFileName, FileMode.Create );
                    tWrtr = new StreamWriter( fStrm );
                    tWrtr.WriteLine( "// Symbolic tokens for parser for grammar file \"{0}\"", grammar.InputFileIdent );
                }
                catch (IOException x) {
                    Console.Error.WriteLine( "GPPG: Error. Failed to create token namelist file" );
                    Console.Error.WriteLine( x.Message );
                    tWrtr = null;
                }
            }

            if (GPCG.ShareTokens && grammar.DatFileName != null) // serialize Terminals dictionary.
            {
                FileStream fStrm = null;
                try {
                    // Insert marker to carry Terminal.max into the serialized structure.
                    Terminal.InsertMaxDummyTerminalInDictionary( grammar.terminals );
                   
                    fStrm = new FileStream( grammar.DatFileName, FileMode.Create );
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize( fStrm, grammar.terminals );
                }
                catch (IOException x) {
                    Console.Error.WriteLine( "GPPG: Error. Failed to create token serialization file" );
                    Console.Error.WriteLine( x.Message );
                }
                finally {
                    if (fStrm != null)
                        fStrm.Close();
                    Terminal.RemoveMaxDummyTerminalFromDictionary( grammar.terminals );
                }
            }

            if (grammar.DiagFileName != null) // generate conflict list file
            {
                try
                {
                    FileStream cStrm = new FileStream(grammar.DiagFileName, FileMode.Create);
                    cWrtr = new StreamWriter(cStrm);
                    cWrtr.WriteLine("// Parser Conflict Information for grammar file \"{0}\"", grammar.InputFileIdent);
                    cWrtr.WriteLine();
                }
                catch (IOException x)
                {
                    Console.Error.WriteLine("GPPG: Error. Failed to create conflict information file");
                    Console.Error.WriteLine(x.Message);
                    cWrtr = null;
                }
            }

            GenerateCopyright();

            GenerateUsingHeader();

            if (grammar.Namespace != null)
            {
                Console.WriteLine("namespace {0}", grammar.Namespace);
                Console.WriteLine('{');
            }

            if (!GPCG.ImportedTokens)
			    GenerateTokens(grammar.terminals, tWrtr);
            grammar.ReportConflicts(cWrtr);

            GenerateClassHeader(grammar.ParserName);
            if (grammar.prologCode.Count > 0) {
              Console.WriteLine("  // Verbatim content from {0}", grammar.InputFileIdent);
              foreach (LexSpan span in grammar.prologCode)
                InsertCodeSpan(span);
              Console.WriteLine("  // End verbatim content from {0}", grammar.InputFileIdent);
              Console.WriteLine();
            }
            GenerateInitializeMethod(states, grammar.productions, grammar.nonTerminals);
            GenerateActionMethod(grammar.productions);
            GenerateToStringMethod();
            InsertCodeSpan(grammar.epilogCode);
            GenerateClassFooter();

            if (grammar.Namespace != null)
                Console.WriteLine('}');

            if (tWrtr != null)
            {
                tWrtr.WriteLine("// End symbolic tokens for parser");
                tWrtr.Close(); // Close the optional token name stream
            }

            if (cWrtr != null)
            {
                cWrtr.WriteLine("// End conflict information for parser");
                cWrtr.Close(); // Close the optional token name stream
            }

            if (sWrtr != null)
            {
                Console.SetOut(save);
                sWrtr.Close();
            }
        }


		private void GenerateCopyright()
		{
            Console.WriteLine("// This code was generated by the Gardens Point Parser Generator");
            Console.WriteLine("// Copyright (c) Wayne Kelly, QUT 2005-2010");
            Console.WriteLine("// (see accompanying GPPGcopyright.rtf)");
            Console.WriteLine();
            if (GPCG.IncludeInfo) {
              Console.WriteLine("// GPPG version " + GPCG.versionInfo);
              Console.WriteLine("// Machine:  " + Environment.MachineName);
              Console.WriteLine("// DateTime: " + DateTime.Now.ToString());
              Console.WriteLine("// UserName: " + Environment.UserName);
              Console.WriteLine("// Input file <{0}>", grammar.InputFileIdent);
            }

            if (!GPCG.NoFilename && !GPCG.IncludeInfo)
              Console.WriteLine("// Input file <{0}>", grammar.InputFileIdent);

            Console.WriteLine();

            Console.Write("// options:");
            if (GPCG.Babel) Console.Write(" babel");
            if (GPCG.Conflicts) Console.Write(" conflicts");
            if (GPCG.Lines) Console.Write(" lines"); else Console.Write(" no-lines");
            if (GPCG.Diagnose) 
                Console.Write(" diagnose & report");
            else if (GPCG.Report) 
                Console.Write(" report");
            if (GPCG.Defines) Console.Write(" defines");
            if (GPCG.ForGplex) Console.Write(" gplex");
            if (GPCG.Conflicts) Console.Write(" conflicts");
            if (GPCG.Listing) Console.Write(" listing");

            
            Console.WriteLine();
            Console.WriteLine();
		}

		private void GenerateUsingHeader()
        {
            Console.WriteLine("using System;");
            Console.WriteLine("using System.Collections.Generic;");
            Console.WriteLine("using System.Globalization;");
            Console.WriteLine("using System.Text;");
            Console.WriteLine("using QUT.Gppg;");
            foreach (string s in grammar.usingList)
                Console.WriteLine("using " + s + ";");
            Console.WriteLine();
        }

        private void GenerateTokens(Dictionary<string, Terminal> terminals, StreamWriter writer)
        {
            Console.Write("{0} enum {1} {{", grammar.Visibility, grammar.TokenName);
            bool first = true;
            foreach (Terminal terminal in terminals.Values)
                if (terminal.symbolic)
                {
                    if (!first)
                        Console.Write(",");
                    if (terminal.num % 6 == 1)
                    {
                        Console.WriteLine();
                        Console.Write("    ");
                    }
                    Console.Write("{0}={1}", terminal.EnumName(), terminal.num);
                    first = false;
                    if (writer != null) 
                        writer.WriteLine("\t{0}.{1} /* {2} */", 
                            grammar.TokenName, terminal.EnumName(), terminal.num);
                }

            Console.WriteLine("};");
            Console.WriteLine();
        }

		private void GenerateValueType()
		{
			if (grammar.unionType != null)
			{
                if (grammar.ValueTypeName == null)
                    // we have a "union" type declared, but no type name declared.
                    grammar.ValueTypeName = Grammar.DefaultValueTypeName;
				Console.WriteLine("{0}{1} struct {2}", 
                    grammar.Visibility, grammar.PartialMark, grammar.ValueTypeName);
				InsertCodeSpan(grammar.unionType);
			}
			else if (grammar.ValueTypeName == null)
				grammar.ValueTypeName = "int";
            // else we have a value type name declared, but no "union"
		}

        private void GenerateScannerBaseClass() {
            Console.WriteLine( "// Abstract base class for GPLEX scanners" );
            Console.WriteLine( "{0} abstract class {1} : AbstractScanner<{2},{3}> {{",
                grammar.Visibility, grammar.ScanBaseName, grammar.ValueTypeName, grammar.LocationTypeName );
            Console.WriteLine( "  private {0} __yylloc = new {0}();", grammar.LocationTypeName );
            Console.Write( "  public override {0} yylloc", grammar.LocationTypeName );
            Console.WriteLine( " { get { return __yylloc; } set { __yylloc = value; } }" );
            Console.WriteLine( "  protected virtual bool yywrap() { return true; }" );
            if (GPCG.Babel) {
                Console.WriteLine();
                Console.WriteLine( "  protected abstract int CurrentSc { get; set; }" );
                Console.WriteLine( "  //" );
                Console.WriteLine( "  // Override the virtual EolState property if the scanner state is more" );
                Console.WriteLine( "  // complicated then a simple copy of the current start state ordinal" );
                Console.WriteLine( "  //" );
                Console.WriteLine( "  public virtual int EolState { get { return CurrentSc; } set { CurrentSc = value; } }" );
                Console.WriteLine( '}' );
                Console.WriteLine();
                Console.WriteLine( "// Interface class for 'colorizing' scanners" );
                Console.WriteLine( "public interface IColorScan {" );
                Console.WriteLine( "  void SetSource(string source, int offset);" );
                Console.WriteLine( "  int GetNext(ref int state, out int start, out int end);" );
            }
            Console.WriteLine( '}' );
            Console.WriteLine();
        }

        private void GenerateScanObjClass() {
            Console.WriteLine( "// Utility class for encapsulating token information" );
            Console.WriteLine( "{0} class ScanObj {{", grammar.Visibility );
            Console.WriteLine( "  public int token;" );
            Console.WriteLine( "  public {0} yylval;", grammar.ValueTypeName );
            Console.WriteLine( "  public {0} yylloc;", grammar.LocationTypeName );
            Console.WriteLine( "  public ScanObj( int t, {0} val, {1} loc ) {{", grammar.ValueTypeName, grammar.LocationTypeName );
            Console.WriteLine( "    this.token = t; this.yylval = val; this.yylloc = loc;" );
            Console.WriteLine( "  }" );
            Console.WriteLine( '}' );
            Console.WriteLine();
        }

        private void GenerateClassHeader( string name )
        {
            GenerateValueType();
            if (GPCG.ForGplex) {
                GenerateScannerBaseClass();
                GenerateScanObjClass();
            }
            Console.WriteLine("{2}{3} class {0}: ShiftReduceParser<{1}, {4}>", 
                name, grammar.ValueTypeName, grammar.Visibility, grammar.PartialMark, grammar.LocationTypeName);
            Console.WriteLine('{');
        }


        private static void GenerateClassFooter()
        {
            Console.WriteLine('}');
        }


        private void GenerateInitializeMethod(
			List<AutomatonState> states, 
			List<Production> productions, 
			Dictionary<string, NonTerminal> nonTerminals)
        {
            // warning 649 : this field never assigned to.
            Console.WriteLine("#pragma warning disable 649");
            Console.WriteLine("  private static Dictionary<int, string> aliasses;");
            Console.WriteLine("#pragma warning restore 649");
            Console.WriteLine("  private static Rule[] rules = new Rule[{0}];", productions.Count + 1);
            Console.WriteLine("  private static State[] states = new State[{0}];", states.Count);
            Console.WriteLine("  private static string[] nonTerms = new string[] {");

            int length = 0;
            Console.Write("      ");
            foreach (NonTerminal nonTerminal in nonTerminals.Values) {
              string ss = String.Format(CultureInfo.InvariantCulture, "\"{0}\", ", nonTerminal.ToString());
              length += ss.Length;
              Console.Write(ss);
              if (length > 70) {
                Console.WriteLine();
                Console.Write("      ");
                length = 0;
              }
            }
            Console.WriteLine("};");
            Console.WriteLine();

            Console.WriteLine("  static {0}() {{", grammar.ParserName);
            int state_nr = 0;
            foreach (AutomatonState state in states)
              GenerateStateElement(state_nr++, state);
            Console.WriteLine();

            Console.WriteLine("    for (int sNo = 0; sNo < states.Length; sNo++) states[sNo].number = sNo;");

            Console.WriteLine();
            foreach (Production production in productions)
              GenerateRule(production);

            List<Terminal> aliasList = new List<Terminal>();
            foreach (KeyValuePair<string, Terminal> pair in grammar.terminals) {
              Terminal term = pair.Value;
              if (term.Alias != null)
                aliasList.Add(term);
            }
            if (aliasList.Count > 0) {
              Console.WriteLine();
              Console.WriteLine("    aliasses = new Dictionary<int, string>();");
              foreach (Terminal termWithAlias in aliasList) {
                Console.WriteLine("    aliasses.Add({0}, {1});",
                    termWithAlias.num,
                    CharacterUtilities.QuoteMap(termWithAlias.Alias));
              }
            }
            Console.WriteLine("  }");
            Console.WriteLine();

            Console.WriteLine("  protected override void Initialize() {");
			Console.WriteLine("    this.InitSpecialTokens((int){0}.error, (int){0}.EOF);", grammar.TokenName);
            Console.WriteLine("    this.InitStates(states);");
            Console.WriteLine("    this.InitRules(rules);");
            Console.WriteLine("    this.InitNonTerminals(nonTerms);");
            Console.WriteLine("  }");
			Console.WriteLine();
        }


        private static void GenerateStateElement(int stateNumber, AutomatonState state) {
          Console.Write("    states[{0}] = new State(", stateNumber);

          int defaultAction = GetDefaultAction(state);
          if (defaultAction != 0)
            Console.Write(defaultAction);
          else {
            Console.Write("new int[]{");
            bool first = true;
            foreach (KeyValuePair<Terminal, ParserAction> transition in state.parseTable) {
              if (!first)
                Console.Write(",");
              Console.Write("{0},{1}", transition.Key.num, transition.Value.ToNum());
              first = false;
            }
            Console.Write('}');
          }

          if (state.nonTerminalTransitions.Count > 0) {
            Console.Write(",new int[]{");
            bool first = true;
            foreach (Transition transition in state.nonTerminalTransitions.Values) {
              if (!first)
                Console.Write(",");
              Console.Write("{0},{1}", transition.A.num, transition.next.num);
              first = false;
            }
            Console.Write('}');
          }
          Console.WriteLine(");");
        }

		private static int GetDefaultAction(AutomatonState state)
        {
			IEnumerator<ParserAction> enumerator = state.parseTable.Values.GetEnumerator();
			enumerator.MoveNext();
			int defaultAction = enumerator.Current.ToNum();

			if (defaultAction > 0) 
				return 0; // can't have default shift action

			foreach (KeyValuePair<Terminal, ParserAction> transition in state.parseTable)
				if (transition.Value.ToNum() != defaultAction)
					return 0;

			return defaultAction;
        }


        private static void GenerateRule(Production production)
        {
            Console.Write("    rules[{0}] = new Rule({1}, new int[]{{", production.num, production.lhs.num);
			bool first = true;
			foreach (Symbol sym in production.rhs)
			{
				if (!first)
					Console.Write(",");
				else
					first = false;
				Console.Write("{0}", sym.num);
			}
			Console.WriteLine("});");
        }
        

        private void GenerateActionMethod(List<Production> productions)
        {
            Console.WriteLine("  protected override void DoAction(int action)");
            Console.WriteLine("  {");
            // warning 162 : unreachable code; 1522 empty switch block
            Console.WriteLine( "#pragma warning disable 162, 1522" );
            Console.WriteLine("    switch (action)");
            Console.WriteLine("    {");
            foreach(Production production in productions)
            {
				if (production.semanticAction != null)
                {
                    string prefix = String.Format(CultureInfo.InvariantCulture, "      case {0}: ", production.num); 
                    Console.WriteLine("{0}// {1}", prefix, 
                        StringUtilities.MakeComment(prefix.Length, production.ToString()));
					production.semanticAction.GenerateCode(this);
                    Console.WriteLine("        break;");
                }
            }
            Console.WriteLine("    }");
            Console.WriteLine( "#pragma warning restore 162, 1522" );
            Console.WriteLine("  }");
            Console.WriteLine();
        }

        private void GenerateToStringMethod()
        {
            Console.WriteLine("  protected override string TerminalToString(int terminal)");
            Console.WriteLine("  {");
            Console.WriteLine("    if (aliasses != null && aliasses.ContainsKey(terminal))");
            Console.WriteLine("        return aliasses[terminal];");
            Console.WriteLine(
                "    else if ((({0})terminal).ToString() != terminal.ToString(CultureInfo.InvariantCulture))", 
                grammar.TokenName);
            Console.WriteLine("        return (({0})terminal).ToString();", grammar.TokenName);
            Console.WriteLine("    else");
            Console.WriteLine("        return CharToString((char)terminal);");
            Console.WriteLine("  }");
            Console.WriteLine();
        }

        private void InsertCodeSpan(LexSpan span)
        {
            if (span != null)
            {
                int lNum = span.startLine;
                string code = span.ToString();
                string format = null;
                if (GPCG.Lines)
                  format = String.Format(CultureInfo.InvariantCulture, "#line {{0}} \"{0}\"", GPCG.LinesFilename ?? grammar.InputFilename);

                StringReader reader = new StringReader(code);
                while (true)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                        break;
                    if (format != null)
                        Console.WriteLine(format, lNum++);
                    Console.WriteLine(line);
                }
            }
        }
    }
}







