// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, K John Gough, QUT 2006-2014
// (see accompanying GPPGcopyright.rtf)
// This file author: John Gough, borrowed from GPLEX

using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using QUT.GplexBuffers;

namespace QUT.GPGen.Parser
{
    internal class Error : IComparable<Error>
    {
        internal const int minErr = 50;
        internal const int minWrn = 100;

        internal int code;
        internal bool isWarn;
        internal string message;
        internal LexSpan span;


        internal Error(int code, string msg, LexSpan spn, bool wrn)
        {
            this.code = code;
            isWarn = wrn;
            message = msg;
            span = spn;
        }

        public int CompareTo(Error r)
        {
            if (span.startLine < r.span.startLine) return -1;
            else if (span.startLine > r.span.startLine) return 1;
            else if (span.startColumn < r.span.startColumn) return -1;
            else if (span.startColumn > r.span.startColumn) return 1;
            else return 0;
        }
    }
    
    
    internal class ErrorHandler
    {
        const int maxErrors = 50; // Will this be enough for all users?

        List<Error> errors;
        int errNum;
        int wrnNum;

        LexSpan defaultSpan;
        internal LexSpan DefaultSpan { 
            set { defaultSpan = value; }
            get { return (defaultSpan != null ? defaultSpan : new LexSpan( 1, 1, 0, 0, 0, 0, null )); }
        }

        internal bool Errors { get { return errNum > 0; } }
        internal bool Warnings { get { return wrnNum > 0; } }

        internal ErrorHandler()
        {
            errors = new List<Error>(8);
        }

        private void AddError(Error e) {
            errors.Add(e);
            if (errors.Count > maxErrors) {
                errors.Add(new Error(1, "Too many errors, abandoning", e.span, false));
                throw new TooManyErrorsException("Too many errors");
            }
        }
 
        // -----------------------------------------------------
        //   Public utility methods
        // -----------------------------------------------------

        internal List<Error> SortedErrorList()
        {
            if (errors.Count > 1) errors.Sort();
            return errors;
        }

        internal void AddError(int code, string msg, LexSpan spn)
        {
            if (spn == null)
                spn = DefaultSpan;
            this.AddError(new Error(code, msg, spn, false)); errNum++;
        }

        internal void AddWarning(int code, string msg, LexSpan spn)
        {
            if (spn == null)
                spn = DefaultSpan;
            this.AddError(new Error(code, msg, spn, true)); wrnNum++;
        }

        /// <summary>
        /// Add this error to the error buffer.
        /// </summary>
        /// <param name="spn">The span to which the error is attached</param>
        /// <param name="num">The error number</param>
        /// <param name="key">The featured string</param>
        internal void ListError( LexSpan spn, int num, string key, char quote ) {
            string s = (quote == '\0' ? "" : quote.ToString());
            ListError( spn, num, key, s, s );
        }

        void ListError(LexSpan spn, int num, string key, string lh, string rh)
        {
            string prefix, suffix, message;
            if (spn == null)
                spn = DefaultSpan;
            switch (num)
            {
                // Syntactic Errors Detected by the Parser ...
                case 70: prefix = "Invalid string escape"; suffix = ""; break;
                case 82: prefix = "Character literal"; suffix = "exceeds maximum in imported token type"; break;
                case 83: prefix = "Key"; suffix = "was not found in token alias list"; break;
                case 84: prefix = "Ambiguous alias"; suffix = "has multiple definitions "; break;

                case 103: prefix = "Highest char literal token"; suffix = "is very large"; break;
                default: prefix = "Error " + Convert.ToString(num, CultureInfo.InvariantCulture); suffix = "";
                    break;
            }
            message = String.Format(CultureInfo.InvariantCulture, "{0} {1}{2}{3} {4}", prefix, lh, key, rh, suffix);
            this.AddError(new Error(num, message, spn, num >= Error.minWrn));
            if (num < Error.minWrn) errNum++; else wrnNum++;
        }


        internal void ListError(LexSpan spn, int num)
        {
            string message;
            switch (num)
            {
                // Lexical Errors Detected by the Scanner ...
                case 50: message = "Unknown %keyword in this context"; break;
                case 51: message = "Bad format for decimal number"; break;
                case 52: message = "Bad format for hexadecimal number"; break;
                case 53: message = "Unterminated comment starts here"; break;
                case 54: message = "Only whitespace is permitted here"; break;
                case 55: message = "Code block has unbalanced braces '{','}'"; break;
                case 56: message = "Keyword \"%}\" is out of place here"; break;
                case 57: message = "This character is invalid in this context"; break;
                case 58: message = "Literal string terminated by EOL"; break;
                case 59: message = "Keyword must start in column-0"; break;
                case 60: message = "Premature termination of Code Block"; break;

                // Syntactic Errors Detected by the Parser ...
                case 71: message = "With %union, %YYSTYPE can only be a simple name"; break;
                case 72: message = "Duplicate definition of Semantic Value Type name"; break;
                case 73: message = "Semantic action index is out of bounds"; break;
                case 74: message = "Unknown special marker in semantic action"; break;
                case 75: message = "Bad separator character in list"; break;
                case 76: message = "This name already defined as a terminal symbol"; break;
                case 77: message = "Position of unmatched brace"; break;
                case 78: message = "Literal string terminated by end of line"; break;
                case 79: message = "Cannot define tokens AND declare %importtokens"; break;
                case 80: message = "Cannot declare %sharetokens AND %importtokens"; break;
                case 81: message = "Cannot declare %importtokens AND extra tokens"; break;
                case 82: message = "Cannot declare %importtokens AND csTokenFile"; break;

                // Warnings Issued by Either Scanner or Parser ...
                case 100: message = "Optional numeric code ignored in this version"; break;
                case 101: message = "%locations is the default in GPPG"; break;
                case 102: message = "Mid-rule %prec has no effect"; break;

                default: message = "Error " + Convert.ToString(num, CultureInfo.InvariantCulture); break;
            }
            this.AddError(new Error(num, message, spn, num >= Error.minWrn));
            if (num < Error.minWrn) errNum++; else wrnNum++;
        }
 
        
        // -----------------------------------------------------
        //   Error Listfile Reporting Method
        // -----------------------------------------------------

        internal void MakeListing(ScanBuff buff, StreamWriter sWrtr, string name, string version)
        {
            int line = 1;
            int eNum = 0;
            int eLin = 0;

            int nxtC = (int)'\n';
            int groupFirst;
            int currentCol;
            int currentLine;

            //
            //  Errors are sorted by line number
            //
            errors = SortedErrorList();
            //
            //  Reset the source file buffer to the start
            //
            buff.Pos = 0;
            sWrtr.WriteLine(); 
            ListDivider(sWrtr);
            sWrtr.WriteLine("//  GPPG error listing for yacc source file <"
                                                           + name + ">");
            ListDivider(sWrtr);
            sWrtr.WriteLine("//  Version:  " + version);
            sWrtr.WriteLine("//  Machine:  " + Environment.MachineName);
            sWrtr.WriteLine("//  DateTime: " + DateTime.Now.ToString());
            sWrtr.WriteLine("//  UserName: " + Environment.UserName);
            ListDivider(sWrtr); sWrtr.WriteLine(); sWrtr.WriteLine();
            //
            //  Initialize the error group
            //
            groupFirst = 0;
            currentCol = 0;
            currentLine = 0;
            //
            //  Now, for each error do
            //
            for (eNum = 0; eNum < errors.Count; eNum++)
            {
                Error errN = errors[eNum];
                eLin = errN.span.startLine;
                if (eLin > currentLine)
                {
                    //
                    // Spill all the waiting messages
                    //
                    int maxGroupWidth = 0;
                    if (currentCol > 0)
                    {
                        sWrtr.WriteLine();
                        currentCol = 0;
                    }
                    for (int i = groupFirst; i < eNum; i++)
                    {
                        Error err = errors[i];
                        string prefix = (err.isWarn ? "// Warning: " : "// Error: ");
                        string msg = StringUtilities.MakeComment(3, prefix + err.message);
                        if (StringUtilities.MaxWidth(msg) > maxGroupWidth)
                            maxGroupWidth = StringUtilities.MaxWidth(msg);
                        sWrtr.Write(msg);
                        sWrtr.WriteLine();
                    }
                    if (groupFirst < eNum)
                    {
                        sWrtr.Write("// ");
                        Spaces(sWrtr, maxGroupWidth - 3);
                        sWrtr.WriteLine();
                    }
                    currentLine = eLin;
                    groupFirst = eNum;
                }
                //
                //  Emit lines up to *and including* the error line
                //
                while (line <= eLin)
                {
                    nxtC = buff.Read();
                    if (nxtC == (int)'\n')
                        line++;
                    else if (nxtC == ScanBuff.EndOfFile)
                        break;
                    sWrtr.Write((char)nxtC);
                }
                //
                //  Now emit the error message(s)
                //
                if (errN.span.endColumn > 3 && errN.span.startColumn < 80)
                {
                    if (currentCol == 0)
                    {
                        sWrtr.Write("//");
                        currentCol = 2;
                    }
                    if (errN.span.startColumn > currentCol)
                    {
                        Spaces(sWrtr, errN.span.startColumn - currentCol - 1);
                        currentCol = errN.span.startColumn - 1;
                    }
                    for (; currentCol < errN.span.endColumn && currentCol < 80; currentCol++ )
                        sWrtr.Write('^'); 
                }
            }
            //
            //  Clean up after last message listing
            //  Spill all the waiting messages
            //
            int maxEpilogWidth = 0;
            if (currentCol > 0)
            {
                sWrtr.WriteLine();
            }
            for (int i = groupFirst; i < errors.Count; i++)
            {
                Error err = errors[i];
                string prefix = (err.isWarn ? "// Warning: " : "// Error: ");
                string msg = StringUtilities.MakeComment(3, prefix + err.message);
                if (StringUtilities.MaxWidth(msg) > maxEpilogWidth)
                    maxEpilogWidth = StringUtilities.MaxWidth(msg);
                sWrtr.Write(msg);
                sWrtr.WriteLine();
            }
            if (groupFirst < errors.Count)
            {
                sWrtr.Write("// ");
                Spaces(sWrtr, maxEpilogWidth - 3);
                sWrtr.WriteLine();
            }
            //
            //  And dump the tail of the file
            //
            nxtC = buff.Read();
            while (nxtC != ScanBuff.EndOfFile)
            {
                sWrtr.Write((char)nxtC);
                nxtC = buff.Read();
            }
            ListDivider(sWrtr); sWrtr.WriteLine();
            sWrtr.Flush();
            // sWrtr.Close();
        }

        internal static void ListDivider(StreamWriter wtr)
        {
            wtr.WriteLine(
            "// =========================================================================="
            );
        }

        internal static void Spaces(StreamWriter wtr, int len)
        {
            for (int i = 0; i < len; i++) wtr.Write('-');
        }


        // -----------------------------------------------------
        //   Console Error Reporting Method
        // -----------------------------------------------------

        internal void DumpErrorsInMsbuildFormat( ScanBuff buff, TextWriter wrtr ) {
            StringBuilder builder = new StringBuilder();
            //
            // Message prefix
            //
            string location = (buff != null ? buff.FileName : "GPPG");
            foreach (Error err in errors) {
                builder.Length = 0; // Works for V2.0 even.
                //
                // Origin
                //
                builder.Append( location );
                if (buff != null) {
                    builder.Append( '(' );
                    builder.Append( err.span.startLine );
                    builder.Append( ',' );
                    builder.Append( err.span.startColumn );
                    builder.Append( ')' );
                }
                builder.Append( ':' );
                //
                // Category                builder.Append( ':' );
                //
                builder.Append( err.isWarn ? "warning " : "error " );
                builder.Append( err.code );
                builder.Append( ':' );
                //
                // Message
                //
                builder.Append( err.message );
                Console.Error.WriteLine( builder.ToString() );
            }
        }

        internal void DumpAll(ScanBuff buff, TextWriter wrtr) {
            if (!GPCG.ErrorsToConsole) {
                DumpErrorsInMsbuildFormat( buff, wrtr );
                return;
            }
            if (buff == null) {
                PanicDump(wrtr); return;
            }
            //
            //  Errors are sorted by line number
            //
            errors = SortedErrorList();
            //
            int  line = 1;
            int  eNum = 0;
            int  eLin = 0;
            int nxtC = (int)'\n'; 
            //
            //  Initialize the error group
            //
            int groupFirst = 0;
            int currentCol = 0;
            int currentLine = 0;
            //
            //  Reset the source file buffer to the start
            //
            buff.Pos = 0;
            wrtr.WriteLine("Error Summary --- ");
            //
            //  Initialize the error group
            //
            groupFirst = 0;
            currentCol = 0;
            currentLine = 0;
            //
            //  Now, for each error do
            //
            for (eNum = 0; eNum < errors.Count; eNum++) {
                eLin = errors[eNum].span.startLine;
                if (eLin > currentLine) {
                    //
                    // Spill all the waiting messages
                    //
                    if (currentCol > 0) {
                        wrtr.WriteLine();
                        currentCol = 0;
                    }
                    for (int i = groupFirst; i < eNum; i++) {
                        Error err = errors[i];
                        wrtr.Write((err.isWarn ? "Warning: " : "Error: "));
                        wrtr.Write(err.message);    
                        wrtr.WriteLine();    
                    }
                    currentLine = eLin;
                    groupFirst  = eNum;
                } 
                //
                //  Skip lines up to *but not including* the error line
                //
                while (line < eLin) {
                    nxtC = buff.Read();
                    if (nxtC == (int)'\n') line++;
                    else if (nxtC == ScanBuff.EndOfFile) break;
                } 
                //
                //  Emit the error line
                //
                if (line <= eLin) {
                    wrtr.Write((char)((eLin/1000)%10+(int)'0'));
                    wrtr.Write((char)((eLin/100)%10+(int)'0'));
                    wrtr.Write((char)((eLin/10)%10+(int)'0'));
                    wrtr.Write((char)((eLin)%10+(int)'0'));
                    wrtr.Write(' ');
                    while (line <= eLin) {
                        nxtC = buff.Read();
                        if (nxtC == (int)'\n') line++;
                        else if (nxtC == ScanBuff.EndOfFile) break;
                        wrtr.Write((char)nxtC);
                    } 
                } 
                //
                //  Now emit the error message(s)
                //
                if (errors[eNum].span.startColumn >= 0 && errors[eNum].span.startColumn < 75) {
                    if (currentCol == 0) {
                        wrtr.Write("-----");
                    }
                    for (int i = currentCol; i < errors[eNum].span.startColumn - 1; i++, currentCol++) {
                        wrtr.Write('-');
                    } 
                    for ( ; currentCol < errors[eNum].span.endColumn && currentCol < 75; currentCol++)
                        wrtr.Write('^');
                }
            }
            //
            //  Clean up after last message listing
            //  Spill all the waiting messages
            //
            if (currentCol > 0) {
                wrtr.WriteLine();
            }
            for (int i = groupFirst; i < errors.Count; i++) {
                Error err = errors[i];
                wrtr.Write((err.isWarn ? "Warning: " : "Error: "));
                wrtr.Write(err.message);    
                wrtr.WriteLine();    
            }
        }

        private void PanicDump(TextWriter wrtr) {
            foreach (Error err in errors) {
                wrtr.Write((err.isWarn ? "Warning: " : "Error: "));
                wrtr.Write(err.message);
                wrtr.WriteLine();
            }

        }
    }
}