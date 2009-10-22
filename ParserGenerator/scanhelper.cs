// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, John Gough QUT 2008
// (see accompanying GPPGcopyright.rtf)

using System;
using System.Globalization;
using QUT.GPGen.Parser;

namespace QUT.GPGen.Lexers
{
    internal sealed partial class Scanner : ScanBase
    {
        private ErrorHandler yyhdlr;
        internal void SetHandler(ErrorHandler hdlr) { yyhdlr = hdlr; }

        private Token GetKeyword(string text)
        {
             text = text.Substring(1);
             switch (text)
             {
             case "token":  
                 return Token.kwToken;
             case "type":  
                 return Token.kwType;
             case "left":  
                 return Token.kwLeft;
             case "right":  
                 return Token.kwRight;
             case "nonassoc":  
                 return Token.kwNonassoc;
             case "prec":  
                 return Token.kwPrec;
             case "start":  
                 return Token.kwStart;
             case "union":  
                 return Token.kwUnion;
             case "defines":  
                 return Token.kwDefines;
             case "locations":  
                 return Token.kwLocations;
             case "namespace":  
                 return Token.kwNamespace;
             case "partial":  
                 return Token.kwPartial;
             case "output":  
                 return Token.kwOutput;
             case "parsertype":  
                 return Token.kwParsertype;
             case "tokentype":
                 return Token.kwTokentype;
             case "scanbasetype":
                 return Token.kwScanbasetype;
             case "using":  
                 return Token.kwUsing;
             case "visibility":  
                 return Token.kwVisibility;
             case "YYSTYPE":  case "valuetype":  
                 return Token.kwYYSTYPE;
             case "YYLTYPE":  
                 return Token.kwYYLTYPE;
             default:
                 Error(50, TokenSpan());
                return Token.errTok;
             }
        }


        public override void yyerror(string format, params object[] args)
        {
            if (yyhdlr != null)
            {
                LexSpan span = TokenSpan();
                if (args == null || args.Length == 0)
                    yyhdlr.AddError(format, span);
                else
                    yyhdlr.AddError(String.Format(CultureInfo.InvariantCulture,format, args), span);
            }
        }

        private void Error(int n, LexSpan s)
        {
            if (yyhdlr != null) yyhdlr.ListError(s, n);
        }
        
        private LexSpan TokenSpan() 
        { return new LexSpan(tokLin, tokCol, tokELin, tokECol, tokPos, tokEPos, buffer); }

        internal int ParseDecimal(string txt)
        {
            int rslt = 0;
            bool isOk = int.TryParse(txt, out rslt);
            if (!isOk)
                Error(51, TokenSpan());
            return rslt;
        }

        internal int ParseHexaDec(string txt)
        {
            int rslt = 0;
            try
            {
                // This particular Convert method does not throw
                // an overflow for 0x7fffffff < num <= 0xffffffff
                rslt = System.Convert.ToInt32(txt, 16);
                if (rslt < 0) throw new OverflowException();
            }
            catch (OverflowException) { Error(52, TokenSpan()); }
            return rslt;
        }
    }
}

