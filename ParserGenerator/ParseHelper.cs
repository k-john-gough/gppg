// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, John Gough QUT 2008
// (see accompanying GPPGcopyright.rtf)

using System;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.Serialization;
using QUT.GPGen.Lexers;
using QUT.GplexBuffers;


namespace QUT.GPGen.Parser
{
    internal partial class Parser
    {
        internal ErrorHandler handler;

        private Grammar grammar = new Grammar();
        internal Grammar Grammar { get { return grammar; } }

        private string baseName;
        private string sourceName;

        internal string ListfileName { get { return baseName + ".lst"; } }
        internal string SourceFileName { get { return sourceName; } }

        private NonTerminal currentLHS;

        enum TokenProperty { token, left, right, nonassoc }

        internal Parser(string filename, Scanner scanner, ErrorHandler handler)
            : base(scanner)
        {
            this.handler = handler;
            this.sourceName = filename;
            this.baseName = System.IO.Path.GetFileNameWithoutExtension(filename);
            grammar.InputFileName = filename;
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



        private void DeclareTokens(PrecType prop, string kind, List<TokenInfo> list)
        {
            grammar.BumpPrec();
            foreach (TokenInfo info in list) {
                Token token = (IsLitChar(info.name) ? Token.litchar : Token.ident);
                Terminal t = grammar.LookupOrDefineTerminal(token, info.name, info.alias);
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

        private void TidyUpDefinitions(LexSpan def)
        {
            handler.DefaultSpan = def;
            if (GPCG.Defines) grammar.TokFileName = baseName + ".tokens";
            if (GPCG.Conflicts) grammar.DiagFileName = baseName + ".conflicts";
            // If both %union AND %YYSTYPE have been set, YYSTYPE must be
            // a simple name, and not a type-constructor. Check this now!
            if (grammar.unionType != null && 
                grammar.ValueTypeName != null &&
                grammar.ValueTypeName.LastIndexOfAny(new char[] { '.', '[', '<' }) > 0)
            {
                handler.ListError(grammar.ValueTypeNameSpan, 71);
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

        private void AddSymbolsToProduction(Production prod, List<string> list)
        {
            // Version 1.3.1 sends even empty lists to this method.
            // Furthermore, version 1.3.1 no longer explicitly calls
            // FixInternalReduction().  It is easier to adopt a consistent
            // approach and let AddXxxToProd check for a trailing action
            // prior to adding symbols or a new action.
            //
            if (list != null) 
            {
                if (prod.semanticAction != null || prod.precSpan != null)
                    FixInternalReduction(prod);
                foreach (string str in list)
                {
                    Symbol symbol = null;
                    switch (TokenOf(str))
                    {
                        case Token.litchar:
                            symbol = grammar.LookupTerminal(Token.litchar, str);
                            break;
                        case Token.litstring:
                            symbol = grammar.aliasTerms[str];
                            break;
                        case Token.ident:
                            if (grammar.terminals.ContainsKey(str))
                                symbol = grammar.terminals[str];
                            else
                                symbol = grammar.LookupNonTerminal(str);
                            break;
                    }
                    prod.rhs.Add(symbol);;
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
                LexSpan cSpan = proxy.codeBlock;      // LexSpan of action code
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
    public class GppgInternalException : Exception
    {
        public GppgInternalException() { }
        public GppgInternalException(string message) : base(message) { }
        public GppgInternalException(string message, Exception innerException)
            : base(message, innerException) { }
        protected GppgInternalException(SerializationInfo info, StreamingContext context)
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

        //internal bool IsInitialized { get { return buffer != null; } }

        /// <summary>
        /// Write the text of this text span to the stream
        /// </summary>
        /// <param name="sWtr"></param>
        //internal void StreamDump(TextWriter sWtr)
        //{
        //    int savePos = buffer.Pos;
        //    string str = buffer.GetString(startIndex, endIndex);

        //    sWtr.WriteLine(str);
        //    buffer.Pos = savePos;
        //    sWtr.Flush();
        //}

        /// <summary>
        /// Write the text of this text span to the console
        /// </summary>
        //internal void ConsoleDump()
        //{
        //    int savePos = buffer.Pos;
        //    string str = buffer.GetString(startIndex, endIndex);
        //    Console.WriteLine(str);
        //    buffer.Pos = savePos;
        //}

        public override string ToString()
        {
            return buffer.GetString(startIndex, endIndex);
        }
    }

}
