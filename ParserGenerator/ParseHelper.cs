// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, John Gough QUT 2006-2014
// (see accompanying GPPGcopyright.rtf)

using System;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using QUT.GPGen.Lexers;
using QUT.GplexBuffers;


namespace QUT.GPGen.Parser
{
    internal partial class Parser
    {
        internal ErrorHandler handler;

        private Grammar grammar;
        internal Grammar Grammar { get { return grammar; } }

        private string baseName;
        private string sourceFileInfo;

        internal string ListfileName { get { return baseName + ".lst"; } }
        internal string SourceFileInfo { get { return sourceFileInfo; } }

        private NonTerminal currentLHS;

        enum TokenProperty { token, left, right, nonassoc }

        internal Parser(string filename, string fileinfo, Scanner scanner, ErrorHandler handler)
            : base(scanner)
        {
            this.handler = handler;
            this.sourceFileInfo = fileinfo;
            this.baseName = System.IO.Path.GetFileNameWithoutExtension(filename);
            this.grammar = new Grammar( handler );
            this.grammar.InputFileIdent = fileinfo;
            this.grammar.InputFilename = filename;
        }

        // ===============================================================
        //
        //  Various helpers for the semantic actions of the parser
        //  Definition Part Helpers
        //
        // ===============================================================

        internal void SetSemanticType(LexSpan span)
        {
            if (grammar.ValueTypeNameSpan != null)
            {
                handler.ListError(grammar.ValueTypeNameSpan, 72);
                handler.ListError(span, 72);
            }
            else
            {
                grammar.ValueTypeNameSpan = span;
                grammar.ValueTypeName = span.ToString();
            }
        }



        private void DeclareTokens(LexSpan span1, PrecType prop, string kind, List<TokenInfo> list)
        {
            grammar.BumpPrec();
            if (GPCG.ImportedTokens)
                handler.ListError( span1, 81 );
            foreach (TokenInfo info in list) {
                Token token = (IsLitChar(info.name) ? Token.litchar : Token.ident);
                Terminal t = grammar.LookupOrDefineTerminal(token, info.name, info.alias, span1);
                if (prop != PrecType.token)
                    t.prec = new Precedence(prop, grammar.Prec);
                if (!String.IsNullOrEmpty(kind))
                    t.kind = kind;
            }
        }

        internal string GetLitString(LexSpan span)
        {
            string text = span.ToString();
            if (text[0] != '\"' || text[text.Length - 1] != '\"')
                throw new GppgInternalException("Internal error: invalid litstring");
            text = text.Substring(1, text.Length - 2);
            try
            {
                text = CharacterUtilities.InterpretCharacterEscapes(text);
            }
            catch (StringInterpretException e)
            {
                handler.ListError(span, 70, e.Message, '\'');
            }
            return text;
        }

        internal static string GetVerbatimString(LexSpan span)
        {
            string text = span.ToString();
            if (text[0] != '@' || text[1] != '\"' || text[text.Length - 1] != '\"')
                throw new GppgInternalException("Internal error: invalid litstring");
            text = text.Substring(2, text.Length - 3);
            return CharacterUtilities.InterpretEscapesInVerbatimString(text);
        }

        private void DeclareNtKind(string kind, List<string> list)
        {
            foreach (string name in list)
            {
                NonTerminal nt = grammar.LookupNonTerminal(name);
                nt.kind = kind;
            }
        }

        /// <summary>
        /// This method is called when the divider "%%" signals the
        /// end of the defnitions section and the start of productions.
        /// </summary>
        /// <param name="def">The divider token text span</param>
        private void TidyUpDefinitions(LexSpan def)
        {
            handler.DefaultSpan = def;
            if (GPCG.Defines) {
                grammar.TokFileName = baseName + ".tokens";
            }
            if (GPCG.CsTokenFile) {
                grammar.CsTokFileName = baseName + "Tokens.cs";
            }
            if (GPCG.ShareTokens) {
                grammar.DatFileName = baseName + "Tokens.dat";
            }
            if (GPCG.Conflicts) grammar.DiagFileName = baseName + ".conflicts";
            // If both %union AND %YYSTYPE have been set, YYSTYPE must be
            // a simple name, and not a type-constructor. Check this now!
            if (grammar.unionType != null && 
                grammar.ValueTypeName != null &&
                grammar.ValueTypeName.LastIndexOfAny(new char[] { '.', '[', '<' }) > 0) {
                handler.ListError(grammar.ValueTypeNameSpan, 71);
            }
            // If %importtokens has been declared then there must be no
            // other %token, %left, %right and so on declarations.
            if (GPCG.ImportedTokens) {
                // Terminal should only contain the two token
                // values added by default: error, and EOF.
                if (grammar.terminals.Count > 2)
                    handler.ListError( def, 79 );
                if (GPCG.ShareTokens)
                    handler.ListError( def, 80 );
                if (GPCG.CsTokenFile)
                    handler.ListError( def, 82 );

                FileStream fStrm = null;
                try {
                    fStrm = new FileStream( grammar.DatFileName, FileMode.Open );
                    BinaryFormatter formatter = new BinaryFormatter();
                    grammar.terminals = (Dictionary<string, Terminal>)formatter.Deserialize( fStrm );
                    Terminal.RemoveMaxDummyTerminalFromDictionary( grammar.terminals );
                }
                catch (Exception x) {
                    Console.Error.WriteLine( "GPPG: Error. Failed to deserialize file {0}", grammar.DatFileName );
                    Console.Error.WriteLine( x.Message );
                }
                finally {
                    if (fStrm != null)
                        fStrm.Close();
                }
            }
        }

        // ===============================================================
        //
        //  Various helpers for the semantic actions of the parser
        //  Rules Part Helpers
        //
        // ===============================================================

        private void SetCurrentLHS(LexSpan lhs) 
        {
            string lhsName = lhs.ToString();
            NonTerminal nt = grammar.LookupNonTerminal(lhsName);
            if (grammar.terminals.ContainsKey(lhsName))
                handler.ListError(lhs, 76);
            currentLHS = nt;
            if (grammar.startSymbol == null)
                grammar.startSymbol = nt;

            if (grammar.productions.Count == 0)
                grammar.CreateSpecialProduction(grammar.startSymbol);
        }

        private void ClearCurrentLHS() { currentLHS = null; }

        private Production NewProduction()
        {
            return new Production(currentLHS);
        }

        private Production NewProduction(List<string> symbols, ActionProxy proxy)
        {
            Production rslt = new Production(currentLHS);
            if (symbols != null)
                AddSymbolsToProduction(rslt, symbols);
            if (proxy != null)
                AddActionToProduction(rslt, proxy);
            return rslt;
        }

        private void AddSymbolsToProduction( Production prod, List<string> list ) {
            // Version 1.3.1 sends even empty lists to this method.
            // Furthermore, version 1.3.1 no longer explicitly calls
            // FixInternalReduction().  It is easier to adopt a consistent
            // approach and let AddXxxToProd check for a trailing action
            // prior to adding symbols or a new action.
            //
            if (list != null) {
                if (prod.semanticAction != null || prod.precSpan != null)
                    FixInternalReduction( prod );
                foreach (string str in list) {
                    Symbol symbol = null;

                    switch (TokenOf( str )) {
                        case Token.litchar: // This is a character literal symbol
                            if (GPCG.ImportedTokens && Terminal.BumpsMax( str ))
                                handler.ListError( this.CurrentLocationSpan, 82, str, '\0' );
                            symbol = grammar.LookupTerminal( Token.litchar, str );
                            break;
                        case Token.litstring: // This is a uned occurrence of a terminal alias.
                            String s = CharacterUtilities.CanonicalizeAlias( str );
                            if (!grammar.aliasTerms.ContainsKey( s ))
                                handler.ListError( this.CurrentLocationSpan, 83, str, '\0' );
                            else {
                                symbol = grammar.aliasTerms[s];
                                if (symbol == Terminal.Ambiguous) // Use of an ambiguous alias.
                                    handler.ListError( this.CurrentLocationSpan, 84, str, '\0' );
                            }                                                
                            break;
                        case Token.ident: // This is a used occurrence of a terminal name.
                            if (grammar.terminals.ContainsKey( str ))
                                symbol = grammar.terminals[str];
                            else
                                symbol = grammar.LookupNonTerminal( str );
                            break;
                    }
                    prod.rhs.Add( symbol );
                }
            }
        }

        private void AddActionToProduction(Production prod, ActionProxy proxy)
        {
            // Version 1.3.1 no longer explicitly calls FixInternalReduction().  
            // It is easier to adopt a consistent approach and
            // let AddXxxToProd check for a trailing action
            // prior to adding symbols or a new action.
            //
            if (proxy != null)
            {
                if (prod.semanticAction != null || prod.precSpan != null)
                    FixInternalReduction(prod);
                LexSpan cSpan = proxy.codeBlock;            // LexSpan of action code
                LexSpan pSpan = proxy.precedenceToken;      // LexSpan of ident in %prec ident
                if (pSpan != null)
                {
                    prod.prec = grammar.LookupTerminal(Token.ident, pSpan.ToString()).prec;
                    prod.precSpan = proxy.precedenceSpan;
                }
                if (cSpan != null)
                {
                    prod.semanticAction = new SemanticAction(prod, prod.rhs.Count, cSpan);
                }
            }
        }

        private void FixInternalReduction(Production prod)
        {
            // This production has an action or precedence. 
            // Before more symbols can be added to the rhs
            // the existing action must be turned into an
            // internal reduction, and the action (and 
            // precedence, if any) moved to the new reduction.
            //
            if (prod.semanticAction != null)
            {
                string anonName = "Anon@" + (++grammar.NumActions).ToString(CultureInfo.InvariantCulture);
                NonTerminal anonNonT = grammar.LookupNonTerminal(anonName);
                Production EmptyProd = new Production(anonNonT);
                EmptyProd.semanticAction = prod.semanticAction;
                EmptyProd.prec = prod.prec;

                grammar.AddProduction(EmptyProd);
                prod.rhs.Add(anonNonT);
            }
            if (prod.precSpan != null)
                handler.ListError(prod.precSpan, 102);
            prod.semanticAction = null;
            prod.precSpan = null;
            prod.prec = null;
        }

        private void FinalizeProduction(Production prod)
        {
            if (prod.semanticAction != null)
                ActionScanner.CheckSpan(prod.rhs.Count, prod.semanticAction.Code, handler);
            grammar.AddProduction(prod);
            Precedence.Calculate(prod);
            prod.precSpan = null;
        }

        // ===============================================================
        // ===============================================================

        private static Token TokenOf(string str)
        {
            if (str[0] == '\'') return Token.litchar;
            else if (str[0] == '\"') return Token.litstring;
            else return Token.ident;
        }

        private static bool IsLitChar(string text)
        {
            return text[0] == '\'' && text[text.Length - 1] == '\'';
        }
    }

    // ===================================================================
    // ===================================================================

    [Serializable]
    public class GppgInternalException : Exception {
        public GppgInternalException() { }
        public GppgInternalException(string message) : base(message) { }
        public GppgInternalException(string message, Exception innerException)
            : base(message, innerException) { }
        protected GppgInternalException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }

    [Serializable]
    public class TooManyErrorsException : Exception {
        public TooManyErrorsException() { }
        public TooManyErrorsException(string message) : base(message) { }
        public TooManyErrorsException(string message, Exception innerException)
            : base(message, innerException) { }
        protected TooManyErrorsException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }

    // ===================================================================
    // ===================================================================

    internal class ActionProxy
    {
        internal LexSpan codeBlock;
        internal LexSpan precedenceToken;
        internal LexSpan precedenceSpan;

        internal ActionProxy(LexSpan precedence, LexSpan identifier, LexSpan code)
        {
            codeBlock = code;
            precedenceToken = identifier;
            precedenceSpan = precedence;
        }
    }

    // ===================================================================
    // ===================================================================

    internal class TokenInfo
    {
        internal string name;
        internal string alias;

        // This constructor ignores explicit numeric value declarations
        // This might change later ...
        internal TokenInfo(LexSpan name, LexSpan alias)
        {
            this.name = name.ToString();
            if (alias != null)
                this.alias = alias.ToString();
        }
    }

    // ===================================================================
    // ===================================================================

    /// <summary>
    /// Objects of this class represent locations in the input text.
    /// The fields record both line:column information and also 
    /// file position data and buffer object identity.
    /// </summary>
    internal class LexSpan : QUT.Gppg.IMerge<LexSpan>
    {
        internal int startLine;     // start line of span
        internal int startColumn;   // start column of span
        internal int endLine;       // end line of span
        internal int endColumn;     // end column of span
        internal int startIndex;    // start position in the buffer
        internal int endIndex;      // end position in the buffer
        internal ScanBuff buffer;   // reference to the buffer

        public LexSpan() { }
        public LexSpan(int sl, int sc, int el, int ec, int sp, int ep, ScanBuff bf)
        { startLine = sl; startColumn = sc; endLine = el; endColumn = ec; startIndex = sp; endIndex = ep; buffer = bf; }

        /// <summary>
        /// This method implements the IMerge interface
        /// </summary>
        /// <param name="end">The last span to be merged</param>
        /// <returns>A span from the start of 'this' to the end of 'end'</returns>
        public LexSpan Merge(LexSpan end)
        {
            return new LexSpan(startLine, startColumn, end.endLine, end.endColumn, startIndex, end.endIndex, buffer);
        }

        public override string ToString()
        {
            return buffer.GetString(startIndex, endIndex);
        }
    }

}
