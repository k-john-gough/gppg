
README file for gppg\ParserGenerator

Question:  Where are GplexBuffers.cs, Parser.cs, Scanner.cs and ScanAction.cs?

Answer:  
These four files are not part of the Source-Controlled distribution. 
These files are produced from specifications prior to the build, from gppg.lex, gppg.y, 
ScanAction.lex and the embedded resources of gplex.

Running the GenerateAll.bat batch file in .\SpecFiles will restore these files.

Of course, you must have current versions of gppg.exe and gplex.exe on your executable
path to do this.

 
