// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2014
// (see accompanying GPPGcopyright.rtf)


using System;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using QUT.GPGen.Parser;
using QUT.GPGen.Lexers;

[assembly: CLSCompliant( true )]
namespace QUT.GPGen {
    class GPCG {
        // Main return codes
        const int MC_OK = 0;
        const int MC_FILEERROR = 1;
        const int MC_TOOMANYERRORS = 2;
        const int MC_EXCEPTION = 3;

        public static bool Babel;
        public static bool Lines = true;
        public static bool Report;
        public static bool Defines;
        public static bool CsTokenFile;
        public static bool ShareTokens;
        public static bool ImportedTokens;
        public static bool ForGplex;
        public static bool Diagnose;
        public static bool Verbose;
        public static bool Conflicts;
        public static bool Listing;
        public static bool NoFilename;
        public static bool ErrorsToConsole;
        public static string versionInfo;
        public static bool IncludeInfo = true;
        public static bool NoThrowOnError;

        public static string OutFileName;
        public static string LinesFilename;

        private static int Main( string[] args ) {
            Stream inputFile = null;

            Grammar grammar = null;
            ErrorHandler handler = new ErrorHandler();
            string inputFileInfo = null;  // Filename plus revision time.
            Lexers.Scanner scanner = null;
            Parser.Parser parser = null;
            Assembly assm = Assembly.GetExecutingAssembly();
            object info = Attribute.GetCustomAttribute( assm, typeof( AssemblyFileVersionAttribute ) );
            versionInfo = ((AssemblyFileVersionAttribute)info).Version;

            try {
                string filename = ProcessOptions( args );

                if (filename == null)
                    return MC_OK;

                try {
                    inputFile = new FileStream( filename, FileMode.Open, FileAccess.Read, FileShare.Read );
                    inputFileInfo = filename + " - " + File.GetLastWriteTime( filename ).ToString();
                }
                catch (IOException x) {
                    string message;
                    inputFile = null;
                    if (x is FileNotFoundException)
                        message = String.Format( CultureInfo.InvariantCulture,
                            "Source file <{0}> not found{1}",
                            filename, Environment.NewLine );
                    else
                        message = String.Format( CultureInfo.InvariantCulture,
                            "Source file <{0}> could not be opened{1}",
                            filename, Environment.NewLine );
                    handler.AddError( 4, message, null ); // aast.AtStart;
                    return MC_FILEERROR;
                }

                scanner = new Lexers.Scanner( inputFile );
                scanner.SetHandler( handler );

                parser = new Parser.Parser( filename, inputFileInfo, scanner, handler );
                // 
                // If the parse is successful, then process the grammar.
                // Otherwise just report the errors that have been listed.
                //
                if ( parser.Parse() && !handler.Errors ) {
                    grammar = parser.Grammar;

                    if (Terminal.Max > 255)
                        // No ambiguating context possible since result appears in delimited error message
                        handler.ListError( null, 103, CharacterUtilities.MapCodepointToDisplayForm( Terminal.Max ), '\'' ); 

                    LALRGenerator generator = new LALRGenerator( grammar );
                    List<AutomatonState> states = generator.BuildStates();
                    generator.ComputeLookAhead();
                    generator.BuildParseTable();
                    if (!grammar.CheckGrammar())
                        throw new ArgumentException( "Non-terminating grammar" );
                    //
                    // If the grammar has non-terminating non-terms we cannot
                    // create a diagnostic report as the grammar is incomplete.
                    //
                    if (!handler.Errors) {
                        CodeGenerator emitter = new CodeGenerator( grammar );
                        emitter.Generate( states );
                    }

                    bool DoDiagnose = Diagnose && !grammar.HasNonTerminatingNonTerms;
                    if (Report || DoDiagnose) {
                        string htmlName = System.IO.Path.ChangeExtension( filename, ".report.html" );
                        try {
                            System.IO.FileStream htmlFile = new System.IO.FileStream( htmlName, System.IO.FileMode.Create );
                            System.IO.StreamWriter htmlWriter = new System.IO.StreamWriter( htmlFile );
                            Grammar.HtmlHeader( htmlWriter, filename );

                            if (Report && DoDiagnose)
                                grammar.GenerateCompoundReport( htmlWriter, inputFileInfo, states );
                            else if (Report)
                                grammar.GenerateReport( htmlWriter, inputFileInfo, states );

                            Grammar.HtmlTrailer( htmlWriter );

                            if (htmlFile != null) {
                                htmlWriter.Flush();
                                htmlFile.Close();
                            }
                        }
                        catch (System.IO.IOException) {
                            Console.Error.WriteLine( "Cannot create html output file {0}", htmlName );
                        }
                    }
                }
            }
            catch (System.Exception e) {
                if (e is TooManyErrorsException)
                    return MC_TOOMANYERRORS;
                Console.Error.WriteLine( "Unexpected Error {0}", e.Message );

                if (NoThrowOnError) {
                    // report the error, do not let it go into the void
                    Console.Error.WriteLine( e );
                    return MC_EXCEPTION;
                }
            }
            finally {
                if (handler.Errors || handler.Warnings)
                    handler.DumpAll( (scanner == null ? null : scanner.Buffer), Console.Error );
                if ((Listing || handler.Errors || handler.Warnings) && parser != null) {
                    string listName = parser.ListfileName;
                    StreamWriter listStream = ListingFile( listName );
                    if (listStream != null)
                        handler.MakeListing( scanner.Buffer, listStream, parser.SourceFileInfo, versionInfo );
                }
            }
            return MC_OK;
        }

        private static StreamWriter ListingFile( string outName ) {
            try {
                FileStream listFile = new FileStream( outName, FileMode.Create );
                if (Verbose) Console.Error.WriteLine( "GPPG: opened listing file <{0}>", outName );
                return new StreamWriter( listFile );
            }
            catch (IOException) {
                Console.Error.WriteLine( "GPPG: listing file <{0}> not opened", outName );
                return null;
            }
        }

        private static string ProcessOptions( string[] args ) {
            string filename = null;

            foreach (string arg in args) {
                if (arg[0] == '-' || arg[0] == '/') {
                    string command;
                    string argument = null;
                    // split off the ':' part
                    int colonIndex = arg.IndexOf( ':' );
                    if (colonIndex == -1) {
                        command = arg.Substring( 1 ).ToUpperInvariant();
                    }
                    else {
                        command = arg.Substring( 1, colonIndex - 1 ).ToUpperInvariant();
                        argument = arg.Substring( colonIndex + 1 );
                    }
                    switch (command) {
                        case "?":
                        case "H":
                        case "HELP":
                            DisplayHelp();
                            return null;
                        case "V":
                        case "VERSION":
                            DisplayVersion();
                            return null;
                        case "L":
                        case "NOLINES":
                        case "NO-LINES":
                            Lines = false;
                            break;
                        case "R":
                        case "REPORT":
                            Report = true;
                            if (Verbose)
                                Diagnose = true;
                            break;
                        case "D":
                        case "DEFINES":
                            Defines = true;
                            break;
                        case "CSTOKENFILE":
                            CsTokenFile = true;
                            break;
                        case "ERRORSTOCONSOLE":
                            ErrorsToConsole = true;
                            break;
                        case "GPLEX":
                            ForGplex = true;
                            break;
                        case "VERBOSE":
                            Verbose = true;
                            if (Report)
                                Diagnose = true;
                            break;
                        case "BABEL":
                            Babel = true;
                            ForGplex = true;
                            break;
                        case "DIAGNOSE":
                            // Obsolete, but still recognized
                            // for backward compatability      
                            Diagnose = true;
                            Report = true;
                            break;
                        case "CONFLICTS":
                            Conflicts = true;
                            Verbose = true;
                            break;
                        case "LISTING":
                            Listing = true;
                            break;
                        case "O":
                        case "OUT":
                        case "OUTPUT":
                            OutFileName = argument;
                            break;
                        case "LINE-FILENAME":
                        case "LINEFILENAME":
                            LinesFilename = argument;
                            break;
                        case "NOINFO":
                        case "NO-INFO":
                            IncludeInfo = false;
                            break;
                        case "NOTHROWONERROR":
                        case "NOTHROW":
                            NoThrowOnError = true;
                            break;
                        default:
                            Console.Error.WriteLine( "GPPG - Unrecognized option \"{0}\"", arg );
                            break;
                    }
                }
                else
                    filename = arg;
            }

            if (filename == null) {
                NoFilename = true;
                DisplayHelp();
            }
            return filename;
        }


        private static void DisplayHelp() {
            Console.WriteLine( "Usage gppg [options] filename" );
            Console.WriteLine();
            Console.WriteLine( "/babel          Generate class compatible with Managed Babel" );
            Console.WriteLine( "/conflicts      Emit \"conflicts\" file with full conflict details" );
            Console.WriteLine( "/csTokenFile    Emit tokens to separate C# file {basename}Tokens.cs" );
            Console.WriteLine( "/defines        Emit \"tokens\" file with token name list" );
            Console.WriteLine( "/errorsToConsole  Produce legacy console messages (not MSBUILD friendly)" );
            Console.WriteLine( "/gplex          Generate scanner base class for GPLEX" );
            Console.WriteLine( "/help           Display this help message" );
            Console.WriteLine( "/line-filename:name Point #line markers at file \"name\"" );
            Console.WriteLine( "/listing        Emit listing file, even if no errors" );
            Console.WriteLine( "/no-info        Do not write extra information to parser header comment" );
            Console.WriteLine( "/no-filename    Do not write the filename in the parser output file" );
            Console.WriteLine( "/no-lines       Suppress the generation of #line directives" );
            Console.WriteLine( "/noThrowOnError Do not exit without an error message" );
            Console.WriteLine( "/out:name       Name the parser output \"name\"" );
            Console.WriteLine( "/report         Write *.report.html file with LALR(1) parsing states" );
            Console.WriteLine( "/verbose        Display extra information to console and in reports" );
            Console.WriteLine( "/version        Display version information" );
            Console.WriteLine();
        }


        private static void DisplayVersion() {
            Assembly assm = Assembly.GetExecutingAssembly();
            object info = Attribute.GetCustomAttribute( assm, typeof( AssemblyFileVersionAttribute ) );
            versionInfo = ((AssemblyFileVersionAttribute)info).Version;

            Console.WriteLine( "Gardens Point Parser Generator (gppg) " + versionInfo );
            Console.WriteLine( "Copyright (c) 2005-2014 Wayne Kelly, John Gough, QUT" );
            Console.WriteLine( "Queensland University of Technology" );
            Console.WriteLine();
        }
    }
}
