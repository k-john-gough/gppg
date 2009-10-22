// Gardens Point Parser Generator
// Copyright (c) Wayne Kelly, QUT 2005-2007
// (see accompanying GPPGcopyright.rtf)


using System;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using QUT.GPGen.Parser;
using QUT.GPGen.Lexers;

[assembly: CLSCompliant(true)]
namespace QUT.GPGen
{
    class GPCG
    {
        public static bool Babel;
        public static bool Lines = true;
        public static bool Report;
        public static bool Defines;
        public static bool ForGplex;
        public static bool Diagnose;
        public static bool Verbose;
        public static bool Conflicts;
        public static bool Listing;
        public static string versionInfo;
        
        private static void Main(string[] args)
        {
            Stream inputFile = null;
            Grammar grammar = null;
            ErrorHandler handler = new ErrorHandler();
            Lexers.Scanner scanner = null;
            Parser.Parser parser = null;

            Assembly assm = Assembly.GetExecutingAssembly();
            object info = Attribute.GetCustomAttribute(assm, typeof(AssemblyFileVersionAttribute));
            versionInfo = ((AssemblyFileVersionAttribute)info).Version;

            try
            {
                string filename = ProcessOptions(args);

                if (filename == null)
                    return;

                try
                {
                    inputFile = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                catch (IOException)
                {
                    inputFile = null;
                    string message = String.Format(CultureInfo.InvariantCulture, "Source file <{0}> not found{1}", filename, Environment.NewLine);
                    handler.AddError(message, null); // aast.AtStart;
                    throw;
                }

                scanner = new Lexers.Scanner(inputFile);
                scanner.SetHandler(handler);

                parser = new Parser.Parser(filename, scanner, handler);
                // 
                // If the parse is successful, then process the grammar.
                // Otherwise just report the errors that have been listed.
                //
                if (parser.Parse())
                {
                    grammar = parser.Grammar;

                    if (Terminal.Max > 255)
                        handler.ListError(null, 103, CharacterUtilities.Map(Terminal.Max), '\'');

                    LALRGenerator generator = new LALRGenerator(grammar);
                    List<AutomatonState> states = generator.BuildStates();
                    generator.ComputeLookAhead();
                    generator.BuildParseTable();
                    if (!grammar.CheckGrammar(handler))
                        throw new ArgumentException("Non-terminating grammar");
                    //
                    // If the grammar has non-terminating non-terms we cannot
                    // create a diagnostic report as the grammar is incomplete.
                    //
                    bool DoDiagnose = Diagnose && !grammar.HasNonTerminatingNonTerms;

                    if (Report || DoDiagnose)
                    {
                        string htmlName = System.IO.Path.ChangeExtension(filename, ".report.html");
                        try
                        {
                            System.IO.FileStream htmlFile = new System.IO.FileStream(htmlName, System.IO.FileMode.Create);
                            System.IO.StreamWriter htmlWriter = new System.IO.StreamWriter(htmlFile);
                            Grammar.HtmlHeader(htmlWriter, filename);

                            if (Report && DoDiagnose)
                                grammar.GenerateCompoundReport(htmlWriter, filename, states);
                            else if (Report)
                                grammar.GenerateReport(htmlWriter, filename, states);

                            Grammar.HtmlTrailer(htmlWriter);

                            if (htmlFile != null)
                            {
                                htmlWriter.Flush();
                                htmlFile.Close();
                            }
                        }
                        catch (System.IO.IOException)
                        {
                            Console.Error.WriteLine("Cannot create html output file {0}", htmlName);
                        }
                    }
                    else if (!handler.Errors)
                    {
                        CodeGenerator code = new CodeGenerator();
                        code.Generate(states, grammar);
                    }
                }
            }
            catch (System.Exception e)
            {
                Console.Error.WriteLine("Unexpected Error {0}", e.Message); 
                throw; // Now rethrow the caught exception.
            }
            finally
            {
                if ((handler.Errors || handler.Warnings) && scanner != null)
                    handler.DumpAll(scanner.Buffer, Console.Error);
                if (Listing || handler.Errors || handler.Warnings)
                {
                    string listName = parser.ListfileName;
                    StreamWriter listStream = ListingFile(listName);
                    if (listStream != null)
                        handler.MakeListing(scanner.Buffer, listStream, parser.SourceFileName, versionInfo);
                }
            }
        }

        private static StreamWriter ListingFile(string outName)
        {
            try
            {
                FileStream listFile = new FileStream(outName, FileMode.Create);
                if (Verbose) Console.Error.WriteLine("GPPG: opened listing file <{0}>", outName);
                return new StreamWriter(listFile);
            }
            catch (IOException)
            {
                Console.Error.WriteLine("GPPG: listing file <{0}> not opened", outName);
                return null;
            }
        }

        private static string ProcessOptions(string[] args)
        {
            string filename = null;

            foreach (string arg in args)
            {
                if (arg[0] == '-' || arg[0] == '/')
                {
                    string command = arg.Substring(1).ToUpperInvariant();
                    switch (command)
                    {
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
                    }
                }
                else
                    filename = arg;
            }

            if (filename == null)
                DisplayHelp();

            return filename;
        }


        private static void DisplayHelp()
        {
            Console.WriteLine("Usage gppg [options] filename");
            Console.WriteLine();
            Console.WriteLine("/babel:      Generate class compatible with Managed Babel");
            Console.WriteLine("/conflicts:  Emit \"conflicts\" file with full conflict details");
            Console.WriteLine("/defines:    Emit \"tokens\" file with token name list");
            // Console.WriteLine("/diagnose:   Write *.report.html file with LALR(1) state information");
            Console.WriteLine("/gplex:      Generate scanner base class for GPLEX");
            Console.WriteLine("/help:       Display this help message");
            Console.WriteLine("/listing:    Emit listing file, even if no errors");
            Console.WriteLine("/no-lines:   Suppress the generation of #line directives");
            Console.WriteLine("/report:     Write *.report.html file with LALR(1) parsing states");
            Console.WriteLine("/verbose:    Display extra information to console and in reports");
            Console.WriteLine("/version:    Display version information");
            Console.WriteLine();
        }


        private static void DisplayVersion()
        {
            Assembly assm = Assembly.GetExecutingAssembly();
            object info = Attribute.GetCustomAttribute(assm, typeof(AssemblyFileVersionAttribute));
            versionInfo = ((AssemblyFileVersionAttribute)info).Version;

            Console.WriteLine("Gardens Point Parser Generator (gppg) " + versionInfo);
            Console.WriteLine("Copyright (c) 2005-2009 Wayne Kelly, QUT");
            Console.WriteLine("w.kelly@qut.ed.au");
            Console.WriteLine("Queensland University of Technology");
            Console.WriteLine();
        }
    }
}
