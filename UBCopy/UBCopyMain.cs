//
// UBCopyMain.cs
//
// Authors:
//  Wesley D. Brown <wes@planetarydb.com>
//
// Copyright (C) 2010 SQLServerIO (http://www.SQLServerIO.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Diagnostics;
using NDesk.Options;
using log4net;

namespace UBCopy
{
    class UBCopyMain
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UBCopyMain));
        private static readonly bool IsDebugEnabled = Log.IsDebugEnabled;

        //hold command line options
        private static string _sourcefile;
        private static string _destinationfile;
        private static bool _overwritedestination = true;
        //we set an inital buffer size to be on the safe side.
        private static int _buffersize = 16;
        private static int _threads = 2;
        private static int _bytessecond = 0;
        private static int _smallfilesize = 16;
        private static bool _checksumfiles;
        private static bool _reportprogres;
        private static bool _movefile;

        private static int Main(string[] args)
        {
            if (IsDebugEnabled)
            {
                Log.DebugFormat("ArchiveTable started at {0}", DateTime.Now);
            }

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            Console.WriteLine("UBCopy " + version);
            if (IsDebugEnabled)
            {
                Log.Debug("Version " + version);
            }
            var parseerr = ParseCommandLine(args);
            if (parseerr == 1)
            {
                return 0;
            }
            try
            {
                if (IsDebugEnabled)
                {
                    Log.Debug("Environment.UserInteractive: " + Environment.UserInteractive);
                }
                //if you are running without an interactive command shell then we disable the fancy reporting feature
                if (Environment.UserInteractive == false)
                {
                    _reportprogres = false;
                }

                var sw = new Stopwatch();
                sw.Start();
                try
                {
                    UBCopyHandler.ProcessFiles(_sourcefile, _destinationfile, _overwritedestination,
                                                            _movefile,
                                                            _checksumfiles, _buffersize, _reportprogres, _threads, _smallfilesize,_bytessecond);
                }
                catch (Exception ex)
                {
                    if (IsDebugEnabled)
                    {
                        Log.Error(ex);
                    }
                    throw;
                }

                sw.Stop();
                Log.InfoFormat("Number of Byes Copied : {0}", UBCopySetup.BytesCopied);
                Log.InfoFormat("Elapsed Seconds  : {0}", sw.ElapsedMilliseconds / 1000.00);
                Log.Info("Done.");
#if DEBUG
                {
                    Console.ReadKey();
                }
#endif
                return 1;

            }
            catch (Exception e)
            {
                Log.Fatal("File Copy Aborted!");
                Log.Fatal(e);

                Console.WriteLine("Error: File copy aborted");
                Console.WriteLine(e.Message);
#if DEBUG
                {
                    Console.ReadKey();
                }
#endif
                return 0;

            }

        }

        static public int ParseCommandLine(string[] args)
        {
            bool showHelp = false;

            var p = new OptionSet
                        {
                          { "s:|sourcefile:", "The file you wish to copy",
                          v => _sourcefile = v },

                          { "d:|destinationfile:", "The target file you wish to write",
                          v => _destinationfile = v},

                          { "o:|overwritedestination:", "True if you want to overwrite the destination file if it exists",
                          (bool v) => _overwritedestination = v},

                          { "m:|movefile:", "True if you want to copy the file to new location and delete from the old location",
                          (bool v) => _movefile = v},

                          { "c:|checksum:", "True if you want use MD5 hash to verify the destination file is the same as the source file",
                          (bool v) => _checksumfiles = v},

                          { "b:|buffersize:", "size in Megabytes, maximum of 32",
                          (int v) => _buffersize = v},

                          { "t:|threads:", "number of threads to use for small file copying",
                          (int v) => _threads = v},

                          { "l:|LimitBandwidth:", "Maxmum number of bytes a second sets thread count to 1",
                          (int v) => _bytessecond = v},

                          { "z:|filesize:", "smallest file size for threaded copy, in megabytes",
                          (int v) => _smallfilesize = v},

                          { "r:|reportprogress:", "True give a visual indicator of the copy progress",
                          (bool v) => _reportprogres = v},

                          { "?|h|help",  "show this message and exit", 
                          v => showHelp = v != null },
                        };

            try
            {
                p.Parse(args);
            }

            catch (OptionException e)
            {
                Console.Write("UBCopy Error: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `UBCopy --help' for more information.");
                return 1;
            }

            if (args.Length == 0)
            {
                ShowHelp("Error: please specify some commands....", p);
                return 1;
            }

            if (_sourcefile == null || _destinationfile == null && !showHelp)
            {
                ShowHelp("Error: You must specify a file to copy (-s) and a file to copy to (-d).", p);
                return 1;
            }

            if (showHelp)
            {
                ShowHelp(p);
                return 1;
            }
            return 0;
        }

        static void ShowHelp(string message, OptionSet p)
        {
            Console.WriteLine(message);
            Console.WriteLine("Usage: UBCopy [OPTIONS]");
            Console.WriteLine("copy files using unbuffered IO and asyncronus buffers");
            Console.WriteLine();
            Console.WriteLine("Options: ");
            p.WriteOptionDescriptions(Console.Out);
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: UBCopy [OPTIONS]");
            Console.WriteLine("copy files using unbuffered IO and asyncronus buffers");
            Console.WriteLine();
            Console.WriteLine("Options: ");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}
