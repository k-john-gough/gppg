/*
 *  Using GPLEX to check validity of action strings
 *  Author: John Gough, 2008
 */

%namespace QUT.GPGen.Lexers
%using QUT.GPGen.Parser;
%visibility internal
%scannertype ActionScanner
%scanbasetype ActionBase
%tokentype ActionToken

%option noparser, nofiles, verbose, summary, noembedbuffers, out:ScanAction.cs

%{
   int length;
   int colNo;
   int lineNo;
   LexSpan src;
   ErrorHandler handler;
%}

eol      \r\n?|\n
number   [0-9]+
dontcare [^@$\n\r]
kindChrs [^>\n\r]
kind     <{kindChrs}+>

%%


{eol}                 lineNo++; colNo = 0;
{dontcare}            colNo++; 

@\$                    |
\$\$                    colNo += 2;

${kind}{number}       {
                        CheckLengthWithKind(yytext); 
                        colNo += yytext.Length; 
                      }
                      
@{number}             |
${number}             {
                        CheckLength(yytext); 
                        colNo += yytext.Length; 
                      }

@{dontcare}           |
\${dontcare}          { handler.ListError(ErrSpan(2), 74); colNo += 2; }

%%

    internal static void CheckSpan(int len, LexSpan span, ErrorHandler hdlr)
    {
        ActionScanner scnr = new ActionScanner();
        scnr.SetSource(span.ToString(), 1);
        scnr.length = len;
        scnr.src = span;
        scnr.handler = hdlr;
        scnr.lineNo = span.startLine;
        scnr.colNo = span.startColumn;
        scnr.yylex();
    }
    
    private void CheckLength(string text)
    {
        int val = int.Parse(text.Substring(1), 0, CultureInfo.InvariantCulture);
        if (val < 1 || val > length)
            handler.ListError(ErrSpan(text.Length), 73);
    }
    
    private void CheckLengthWithKind(string text)
    {
        int endKind = text.LastIndexOf('>');
        int val = int.Parse(text.Substring(endKind + 1), 0, CultureInfo.InvariantCulture);
        if (val < 1 || val > length)
            handler.ListError(ErrSpan(text.Length), 73);
    }
    
    private LexSpan ErrSpan(int len)
    {
        return new LexSpan(lineNo, colNo, lineNo, colNo+len, src.startIndex, src.endIndex, src.buffer);
    }


