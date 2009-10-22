
/* gppg.lex: Author: John Gough, August 2008 */

%using System.Collections;
%using QUT.GPGen.Parser;
%namespace QUT.GPGen.Lexers
%visibility internal
%tokentype Token

%option stack, minimize, parser, verbose, persistbuffer, noembedbuffers, out:Scanner.cs

Eol             (\r\n?|\n)
NotWh           [^ \t\r\n]
Space           [ \t]
Ident           [a-zA-Z_][a-zA-Z0-9_]*
Number          [0-9]+
OctDig          [0-7]
HexDig          [0-9a-fA-F]
HexNumber       0x{HexDig}+

CmntStrt     \/\*
CmntEnd      \*\/
ABStar       [^\*\n\r]* 

CodeSkip     [^@"/{}%] 
OpChars      [<>\.\[\]{};:|,]
 
DotChr       [^\r\n]
EscChr       \\{DotChr}
OctEsc       \\{OctDig}{3}
HexEsc       \\x{HexDig}{2}
UniEsc       \\u{HexDig}{4}
UNIESC       \\U{HexDig}{8}

ChrChs       [^\\'\a\b\f\n\r\t\v\0]
StrChs       [^\\\"\a\b\f\n\r\t\v\0]
VrbChs       [^\"] 

LitChr       \'({ChrChs}|{EscChr}|{OctEsc}|{HexEsc}|{UniEsc}|{UNIESC})\'
LitStr       \"({StrChs}|{EscChr}|{OctEsc}|{HexEsc}|{UniEsc}|{UNIESC})*\"
VrbStr       @\"({VrbChs}|{EscChr}|{OctEsc}|{HexEsc}|{UniEsc}|{UNIESC})*\"
BadStr       \"({StrChs}|{EscChr}|{OctEsc}|{HexEsc}|{UniEsc}|{UNIESC})*

OneLineCmnt  \/\/{DotChr}*

/* --------------------------- */

%x TheRules
%x TheEpilog
%x GetPath
%x Prolog
%x Action
%x Comment
%x CodeBlock
%x ShouldBeBlank


%%

%{
    LexSpan comStart = null;
    LexSpan errStart = null;
    LexSpan errEnd   = null;
    int braceNestingLevel = 0;
%}

/* Comment handling in almost all contexts */
<0,TheRules,CodeBlock,GetPath,ShouldBeBlank>{OneLineCmnt} { /* skip */ }
<0,TheRules,CodeBlock,GetPath,ShouldBeBlank>{CmntStrt} { 
                              yy_push_state(Comment); 
                              comStart = TokenSpan(); 
                              /* And no token returned */
                            }

/* Special case of comment as first character of CodeBlock */ 
<TheEpilog,Prolog,Action>{OneLineCmnt} {
                              yy_push_state(CodeBlock);  
                              return (int)Token.codeStart; 
                            }
<TheEpilog,Prolog,Action>{CmntStrt} {
                              yy_push_state(CodeBlock); 
                              yy_push_state(Comment); 
                              comStart = TokenSpan(); 
                              return (int)Token.codeStart; 
                            }
                            
                            
<Comment>{
    {ABStar}           |
    \*                 /* skip */
    <<EOF>>            { Error(53, comStart); /* This comment is unterminated */ }
    {CmntEnd}          { 
                         yy_pop_state();
                         // If comment has obscured one or more EOL then ...  
                         if (YY_START == ShouldBeBlank) {
                             if (errStart != null) {
                                 Error(54, errStart.Merge(errEnd));
                                 errStart = null;
                             } else if (lNum > comStart.startLine) 
                                 yy_pop_state();
                         }
                         /* And no token returned */ 
                       }
}

<ShouldBeBlank>{Eol}        {
                              if (errStart != null)
                                  Error(54, errStart.Merge(errEnd)); 
                              yy_pop_state(); 
                            }
<ShouldBeBlank>{NotWh}      { 
                              {
                                LexSpan cSpan = TokenSpan(); /* Only white space goes here */
                                errEnd = cSpan;
                                if (errStart == null) errStart = cSpan;
                              } 
                            }

/* Rules in the initial state */
^%%                         { 
                              yy_clear_stack();
                              BEGIN(TheRules); 
                              yy_push_state(ShouldBeBlank); errStart = null;
                              return (int)Token.kwPCPC; 
                            }
^%\{                        { 
                              yy_push_state(Prolog); 
                              yy_push_state(ShouldBeBlank); errStart = null;
                              return (int)Token.kwLbrace; 
                            }

^%{Ident}                   { 
                              {
                                  Token kWord = GetKeyword(yytext); 
                                  if (kWord == Token.kwOutput)
                                      BEGIN(GetPath);
                                  return (int)kWord;
                              }
                            }
\%{Ident}                   { 
							  {   // An error, but attempt recovery
                                  Token kWord = GetKeyword(yytext); 
                                  if (kWord != Token.errTok)
                                      Error(59, TokenSpan());
                                  if (kWord == Token.kwOutput)
                                      BEGIN(GetPath);
                                  return (int)kWord;
                              }
                            }
                                                        
{Number}                    { yylval.iVal = ParseDecimal(yytext); return (int)Token.number; }
{HexNumber}                 { yylval.iVal = ParseHexaDec(yytext); return (int)Token.number; }
{Ident}                     { return (int)Token.ident; }
{LitChr}                    { return (int)Token.litchar; }
{LitStr}                    { return (int)Token.litstring; }                           
{OpChars}                   { return (int)(yytext[0]); } 

/* Rules for scanning filenames */
<GetPath>{
    {LitStr}           { return (int)Token.litstring; }
    {VrbStr}           { return (int)Token.verbatim; }
    ={NotWh}*          { yyless(1); return (int)'='; }
    {NotWh}+           { return (int)Token.filePath; }
    {Eol}              { BEGIN(0); }
}

/* This is how the Prolog state is finished */
<Prolog>^%\}                { 
                              if (braceNestingLevel != 0) {
                                  Error(55, TokenSpan()); braceNestingLevel = 0;
                              }
                              else {
                                  yy_pop_state(); return (int)Token.kwRbrace; 
                              }
                            }

/* This is how the TheEpilog state is finished */
<TheEpilog><<EOF>>          { yy_pop_state(); yyless(0); }
                            
/* This is how the Action state is finished */
<Action>\}                  { yy_pop_state(); return (int)'}'; }

<CodeBlock>{         // Start of CodeBlock production group
    \{               { braceNestingLevel++; }
    {CodeSkip}+      |
    \%{CodeSkip}+    |
    \/               |
    \%               |
    @                |
    {LitStr}         |
    {LitChr}         |
    {VrbStr}         /* skip */
    {BadStr}         { Error(58, TokenSpan()); }
    \}               { 
                              if (braceNestingLevel == 0 && 
                                       yy_top_state() == Action) {
                                  yy_pop_state();
                                  yyless(0);
                                  return (int)Token.codeEnd;
                              }
                              else
                                  braceNestingLevel--; }
    ^%%              {
                              for ( ; ; ) {
                                  yy_pop_state();
                                  switch (YY_START)
                                  {
                                    case INITIAL: 
                                        BEGIN(TheRules);
                                        Error(60, TokenSpan()); 
                                        return (int)Token.kwPCPC;
                                    case TheRules: 
                                        BEGIN(TheEpilog);
                                        Error(60, TokenSpan()); 
                                        return (int)Token.kwPCPC;
                                    default: break; 
                                  }
                              }
                            }
    <<EOF>>          {
                              if (braceNestingLevel != 0)
                                  Error(55, TokenSpan()); 
                              yy_pop_state(); 
                              yyless(0); 
                              return (int)Token.codeEnd; 
                            }
                            
    ^%\}             { 
                              if (yy_top_state() != Prolog) {
                                  Error(56, TokenSpan()); yy_clear_stack(); BEGIN(Prolog);
                              } else if (braceNestingLevel != 0) {
                                  Error(55, TokenSpan());
                              } else {
                                  yy_pop_state();
                              } 
                              yyless(0); return (int)Token.codeEnd;
                            }
} // End of CodeBlock production group                            

/* This rule must come last! */
<Prolog,TheEpilog,Action>.  { yy_push_state(CodeBlock); yyless(0); return (int)Token.codeStart; }

<TheRules>{
    \{                { // This pattern must come first 
                        yy_push_state(Action); 
                        braceNestingLevel = 1; 
                        return (int)'{'; 
                      }
    {OpChars}         { // This pattern must come second
                        return (int)(yytext[0]);  
                      } 
    ^%%               { 
                        yy_clear_stack();
                        BEGIN(TheEpilog); 
                        yy_push_state(ShouldBeBlank); errStart = null;
                        return (int)Token.kwPCPC; 
                      }
    ^{Ident}          { return (int)Token.anchoredSymbol; }
    {Ident}           { return (int)Token.ident; }
    \%{Ident}          { return (int)GetKeyword(yytext); }
    {LitChr}          { return (int)Token.litchar; }
    {LitStr}          { return (int)Token.litstring; }
    \'                { Error(58, TokenSpan()); }
}

/* "Catch all" rules for lexical error recovery */
<*>{NotWh}                  { Error(57, TokenSpan()); } /* Unknown character */

<0,TheRules>^%\}            { return (int)Token.kwRbrace; /* But a syntax error! */ }
<*>%{NotWh}                 { Error(50, TokenSpan()); } /* Unknown %keyword in this context */
<*>{BadStr}/[\r\n]          { Error(58, TokenSpan()); } /* Literal string terminated by EOL */

/* ------------------------------------------ */
%{
	yylloc = new LexSpan(tokLin, tokCol, tokELin, tokECol, tokPos, tokEPos, buffer);
%}
/* ------------------------------------------ */

%%  
/* User Code is all in LexHelper.cs */
