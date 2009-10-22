%{  
/*
 *  Parser spec for GPPG
 *  gppg.y: Author: John Gough, August 2008
 *  Process with > GPPG /gplex /no-lines gppg.y
 */
%}

%output=Parser.cs 
%using System.Collections;
%namespace QUT.GPGen.Parser
%tokentype Token

%visibility internal

%YYLTYPE LexSpan
%partial
%union { public int iVal; 
         public List<string> stringList;
         public List<TokenInfo> tokenList;
         public TokenInfo tokenInfo; 
         public Production prod;
         public ActionProxy proxy;
       }

%token codeStart codeEnd ident anchoredSymbol 
%token <iVal> number 
%token filePath litstring verbatim litchar
%token kwPCPC "%%", kwLbrace "%{", kwRbrace "%}" 
%token kwToken "%token", kwType "%type", kwLeft "%left" 
%token kwRight "%right", kwNonassoc "%nonassoc", kwPrec "%prec" 
%token kwStart "%start", kwUnion "%union", kwDefines "%defines"
%token kwLocations "%locations", kwNamespace "%namespace" 
%token kwPartial "%partial", kwOutput "%output" 
%token kwParsertype "%parsertype", kwTokentype "%tokentype", kwScanbasetype "%scanbasetype"  
%token kwUsing "%using", kwVisibility "%visibility" 
%token kwYYSTYPE "%YYSTYPE", kwYYLTYPE "%YYLTYPE" 

%token maxParseToken errTok

%type <tokenList> TokenList
%type <tokenInfo> TokenDecl
%type <stringList> NtSymbolList Symbols SymbolsOpt
%type <prod> RightHandSide RHStermList
%type <proxy> Action PrecOptAndAction

%%

Program
    : DefinitionSectionOpt Divider RulesSection EpilogOpt
    | error
    ;
    
Divider
    : kwPCPC			{ TidyUpDefinitions(@1); }
    ;
    
EpilogOpt
    : kwPCPC CodeBlock	{ grammar.epilogCode = @2; }
    | /* empty */
    ;
    
CodeBlock
    : codeStart codeEnd { /* default location action @$ = @1.Merge(@2); */ }
    | /* empty */
    ;
    
    /* =============== Definition Section Productions =============== */
    
DefinitionSectionOpt
    : Definitions
    | /* empty */
    | error
    ;

Definitions
    : Definitions Definition
    | Definition
    | error Definition
    ;
     
Definition
    : kwLbrace CodeBlock kwRbrace
						{ grammar.prologCode.Add(@2); }
    | Declaration
    ;
    
Declaration
    : kwToken KindOpt TokenList
						{ DeclareTokens(PrecType.token, @2.ToString(), $3); }
						
    | kwType Kind NtSymbolList
						{
						  string kind = @2.ToString();
						  DeclareNtKind(kind, $3);
						}

    | kwLeft KindOpt TokenList
						{ DeclareTokens(PrecType.left, @2.ToString(), $3); }

    | kwRight KindOpt TokenList
						{ DeclareTokens(PrecType.right, @2.ToString(), $3); }

    | kwNonassoc KindOpt TokenList
						{ DeclareTokens(PrecType.nonassoc, @2.ToString(), $3); }

    | kwStart NtSymbol
						{ grammar.startSymbol = grammar.LookupNonTerminal(@2.ToString()); }

    | kwUnion TypeNameOpt UnionTypeConstructor
						{ grammar.unionType = @3; }

    | kwLocations		{  handler.ListError(@1, 101); }

    | kwDefines			{ GPCG.Defines = true; }
    
    | kwPartial			{ grammar.IsPartial = true; }

    | kwNamespace DottedName
						{ grammar.Namespace = @2.ToString(); }

    | kwUsing DottedName SemiOpt
						{ grammar.usingList.Add(@2.ToString()); }

    | kwOutput '=' filePath
						{ grammar.OutFileName = @3.ToString(); }

    | kwOutput '=' litstring
						{ grammar.OutFileName = GetLitString(@3); }
						
    | kwOutput '=' verbatim
						{ grammar.OutFileName = GetVerbatimString(@3); }

    | kwScanbasetype ident
						{ grammar.ScanBaseName = @2.ToString(); }
						
    | kwParsertype ident
						{ grammar.ParserName = @2.ToString(); }

    | kwVisibility ident
						{ grammar.Visibility = @2.ToString(); }
						
    | kwTokentype ident
						{ grammar.TokenName = @2.ToString(); }

    | kwYYSTYPE TypeConstructor
						{ SetSemanticType(@2); }

    | kwYYLTYPE TypeConstructor
						{ grammar.LocationTypeName = @2.ToString(); }
    ;
    
TypeNameOpt
    : ident				{ SetSemanticType(@1); }
    | /* skip */
    ;
    
DottedName
    : DottedName '.' ident
    | ident
    ;
    
KindOpt
    : Kind 
    | /* empty */
    ;
    
Kind
    : '<' ident '>'     { @$ = @2; }
    | '<' error '>'
    ;
    
TokenList
    : TokenDecl         {
                          $$ = new List<TokenInfo>();
                          $$.Add($1);
                        }
    | TokenList CommaOpt TokenDecl
                        { $1.Add($3); $$ = $1;}
    | TokenList BadSeparator
                        { handler.ListError(@2, 75); $$ = $1; }
    ;
    
TokenDecl
    : ident number      { 
                          handler.ListError(@2, 100); 
                          $$ = new TokenInfo(@1, null);
                        }
    | ident litstring number
                        { 
                          handler.ListError(@2, 100); 
                          $$ = new TokenInfo(@1, @2);
                        } 
    | ident             { $$ = new TokenInfo(@1, null); }
    | ident litstring   { $$ = new TokenInfo(@1, @2); }
    | litchar           { $$ = new TokenInfo(@1, null); }
    ;
    
NtSymbolList
    : NtSymbol          { 
                          $$ = new List<string>();
						  $$.Add(@1.ToString()); 
						}
    | NtSymbolList CommaOpt NtSymbol          
                        { $1.Add(@3.ToString()); $$ = $1; }
    | NtSymbolList BadSeparator
                        { handler.ListError(@2, 75); $$ = $1; }                        
    ;
    
NtSymbol
    : ident					  
    | anchoredSymbol
    ;
    
TypeConstructor
    : DottedName '[' ']'
    | DottedName '<' TypeConstructor '>'
    | DottedName
    ;
    
UnionTypeConstructor
    : '{' DeclList '}'
    ;
    
DeclList
    : OneDecl
    | DeclList OneDecl
    | error
    ;
    
OneDecl
    : TypeConstructorSeq ident ';'
    ;
    
TypeConstructorSeq
    : TypeConstructor
    | TypeConstructorSeq TypeConstructor
    ;
    
CommaOpt
    : ','
    | /* empty */
    ;
    
SemiOpt
    : ';'
    | /* empty */
    ;
    
BadSeparator
    : ';' | ':' | '<' | '>' | '(' | ')' | '[' | ']' | '{' | '}' 
    ;

  /* ================== Rules Section Productions ================== */

RulesSection
    : RulesSection ARule
    | ARule
    | error
    ;
    
ARule
    : RuleProlog RightHandSide AlternativesOpt ';'
						{ ClearCurrentLHS(); }
    ;
    
RuleProlog
    : anchoredSymbol ':'
						{ SetCurrentLHS(@1); } 
	;
    
AlternativesOpt
    : /* skip */
    | AlternativesOpt '|' RightHandSide
    ;

RightHandSide
    : /* skip */		{ $$ = NewProduction(); FinalizeProduction($$); }
    | RHStermList       { $$ = $1; FinalizeProduction($$); }
    ;
    
RHStermList
    : Symbols           { $$ = NewProduction($1, null); }
    | PrecOptAndAction SymbolsOpt {
                          $$ = NewProduction(null, $1);
                          AddSymbolsToProduction($$, $2);
                        }
    | RHStermList PrecOptAndAction SymbolsOpt {
                          AddActionToProduction($1, $2);
                          AddSymbolsToProduction($1, $3);
                          $$ = $1;
                        }
    ;  
 
SymbolsOpt
    : /* skip */        { $$ = null; }
    | Symbols
    ;  
    
    // ----------------------------------------------

    // ----------------------------------------------
    
Symbols
    : SymOrLit			{ $$ = new List<string>(); $$.Add(@1.ToString()); }
    | Symbols SymOrLit  { $1.Add(@2.ToString());  $$ = $1; }
    ;

SymOrLit
    : ident
    | litchar
    | litstring
    ;
    
    
PrecOptAndAction
    : Action			{ $$ = $1; }
    | kwPrec ident Action
                        { $3.precedenceToken = @2; $3.precedenceSpan = @1; $$ = $3; }
    | kwPrec ident
                        { $$ = new ActionProxy(@1, @2, null); }
    ;
        
Action
    : '{' CodeBlock '}' { $$ = new ActionProxy(null, null, @$); }
    | '{' CodeBlock error 
    ;
%%
