
REM generate a fresh copy of parser.cs
gppg /gplex /nolines gppg.y
move parser.cs ..

REM generate a fresh copy of Scanner.cs
gplex gppg.lex
move Scanner.cs ..

REM generate a fresh copy of ScanAction.cs
gplex ScanAction.lex
move ScanAction.cs ..

if not exist GplexBuffers.cs goto finish
move GplexBuffers.cs ..

:finish
REM Ended

