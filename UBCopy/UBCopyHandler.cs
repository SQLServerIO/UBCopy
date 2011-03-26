//
// UBCopyHandler.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Management;
namespace UBCopy
{
    public static class UBCopySetup
    {
        public static Stack<string> FileList = new Stack<string>(50000);

        //hold command line options
        //public static string Sourcefile;
        public static string Destinationfile;
        public static bool Overwritedestination = true;
        //we set an inital buffer size to be on the safe side.
        public static int Buffersize = 16;
        public static bool Checksumfiles;
        public static bool Reportprogres;
        public static bool Movefile;
        public static int NumberThreadsFileSize = 1;

        public static bool Listlocked;

        public static readonly object DictonaryLocker = new object();

        public static void TraverseTree(string root)
        {
            var dirs = new Stack<string>(20);

            if (!Directory.Exists(root))
            {
                throw new ArgumentException();
            }
            dirs.Push(root);

            while (dirs.Count > 0)
            {
                var currentDir = dirs.Pop();
                string[] subDirs;
                try
                {
                    subDirs = Directory.GetDirectories(currentDir);
                }
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }
                catch (DirectoryNotFoundException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(currentDir);
                }

                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }

                catch (DirectoryNotFoundException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }
                foreach (var str in subDirs)
                    dirs.Push(str);

                foreach (var file in files)
                {
                    if (file.Length < 261)
                    {
                        try
                        {
                            FileList.Push(file);
                        }
                        catch (FileNotFoundException)
                        {
                            Console.WriteLine("File Not Found:{0} ", file);
                            continue;
                        }
                    }
                    else
                    {
                        Console.WriteLine("File Name or Path is too long:{0}", file);
                    }
                }
            }
        }

    }

    class UBCopyProcessor
    {
        //private static readonly object DictonaryLocker = new object();

        private readonly ManualResetEvent _doneEvent;

        public UBCopyProcessor(ManualResetEvent doneEvent)
        {
            _doneEvent = doneEvent;
        }

        public void MyProcessThreadPoolCallback(Object threadContext)
        {
            UBCopyFile();
            _doneEvent.Set();
        }

        private static void UBCopyFile()
        {
            //var sw = new Stopwatch();
            string file;
            lock (UBCopySetup.DictonaryLocker)
            {
                file = UBCopySetup.FileList.Pop();
            }
            // ReSharper disable AssignNullToNotNullAttribute
            string destinationfile = Path.Combine(UBCopySetup.Destinationfile, Path.GetFileName(file));
            // ReSharper restore AssignNullToNotNullAttribute
            var f = new FileInfo(file);
            var fileSize = f.Length;
            Debug.WriteLine("Thread                      :" + Thread.CurrentThread.ManagedThreadId);
            Debug.WriteLine("File Name                   : " + file);


            if (fileSize < UBCopySetup.NumberThreadsFileSize)
            {
                File.Copy(file, destinationfile, UBCopySetup.Overwritedestination);
            }
            else
            {
                AsyncUnbuffCopy.AsyncCopyFileUnbuffered(file, destinationfile, UBCopySetup.Overwritedestination,
                                                        UBCopySetup.Movefile,
                                                        UBCopySetup.Checksumfiles, UBCopySetup.Buffersize,
                                                        UBCopySetup.Reportprogres);
            }
        }
    }


    public class UBCopyHandler
    {
        public static int ProcessFiles(string inputfile, string outputfile, bool overwrite, bool movefile, bool checksum, int buffersize, bool reportprogress, int numberthreads, int numberthreadsfilesize)
        {
            UBCopySetup.Buffersize = buffersize;
            UBCopySetup.Checksumfiles = checksum;
            UBCopySetup.Destinationfile = outputfile;
            UBCopySetup.Movefile = movefile;
            UBCopySetup.Reportprogres = reportprogress;
            UBCopySetup.NumberThreadsFileSize = numberthreadsfilesize * 1024;

            try
            {
                // get the file attributes for file or directory
                var attr = File.GetAttributes(inputfile);

                //detect whether its a directory or file
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    UBCopySetup.TraverseTree(inputfile);
                }
                else
                {
                    UBCopySetup.FileList.Push(inputfile);
                }

            }
            catch (Exception)
            {
                Console.WriteLine("Invalid path or filename:{0}", inputfile);
                return 0;
            }

            //create the directory if it doesn't exist
            if (!Directory.Exists(outputfile))
            {
                try
                {
                    // ReSharper disable AssignNullToNotNullAttribute
                    Directory.CreateDirectory(Path.GetDirectoryName(outputfile));
                    // ReSharper restore AssignNullToNotNullAttribute
                }
                catch (Exception e)
                {
                    Console.WriteLine("Create Directory Failed.");
                    Console.WriteLine(e.Message);
                    throw;
                }
            }

            try
            {
                // get the file attributes for file or directory
                var attr = File.GetAttributes(outputfile);

                //detect whether its a directory or file
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    if (UBCopySetup.FileList.Count == 1)
                    {
                        Console.WriteLine("No Output File Name Specified.");
                        return 0;
                    }
                }
                else
                {
                    if (UBCopySetup.FileList.Count > 1)
                    {
                        Console.WriteLine("Cannot write multiple files to a single target file.");
                        return 0;
                    }
                }

            }
            catch (Exception)
            {
                Console.WriteLine("Invalid path or filename:{0}", outputfile);
                return 0;
            }
            var numprocs = numberthreads;
            var numlogprocs = numberthreads;

            if (numberthreads > 1)
            {

                try
                {
                    var mgmtObjects = new ManagementObjectSearcher("Select * from Win32_ComputerSystem");
                    foreach (var item in mgmtObjects.Get())
                    {
                        numprocs = Convert.ToInt32(item["NumberOfProcessors"].ToString());
                        numlogprocs = Convert.ToInt32(item["NumberOfLogicalProcessors"].ToString());
                    }

                    if (numlogprocs == 0)
                        numlogprocs = numprocs;

                    if (numprocs == 0)
                        numlogprocs = 1;

                    if (numberthreads > numprocs || numberthreads > numlogprocs)
                        numlogprocs = numprocs;
                }
                catch (Exception)
                {
                    numlogprocs = numberthreads;
                }
            }
            else
            {
                numlogprocs = 1;
            }
            Console.WriteLine("Number of Threads: {0}",numlogprocs);

            var totalCountToProcess = numlogprocs;

            if (UBCopySetup.FileList.Count < totalCountToProcess)
            {
                totalCountToProcess = UBCopySetup.FileList.Count;
            }
            
            Console.WriteLine("Number of Files To Process: {0}", UBCopySetup.FileList.Count);
            var ftph = UBCopySetup.FileList.Count;

            var doneEvents = new ManualResetEvent[totalCountToProcess];
            var hashFilesArray = new UBCopyProcessor[totalCountToProcess];
            var ftp = UBCopySetup.FileList.Count;

            var sw = new Stopwatch();
            sw.Start();

            while (ftp > 0)
            {
//                Console.WriteLine(UBCopySetup.FileList.Count);
                // Configure and launch threads using ThreadPool:
                if (ftp < totalCountToProcess)
                    totalCountToProcess = ftp;
                for (var i = 0; i < totalCountToProcess; i++)
                {
                    doneEvents[i] = new ManualResetEvent(false);
                    var p = new UBCopyProcessor(doneEvents[i]);
                    hashFilesArray[i] = p;
                    ThreadPool.QueueUserWorkItem(p.MyProcessThreadPoolCallback, i);
                }

                // Wait for all threads in pool to finished processing
                WaitHandle.WaitAll(doneEvents);
                ftp = UBCopySetup.FileList.Count;
            }
            sw.Stop();

            Console.WriteLine("UBCopy - ElapsedSeconds    : " + (sw.ElapsedMilliseconds / 1000.00));
            Console.WriteLine("Files Per Second: {0}",ftph/(sw.ElapsedMilliseconds / 1000.00));
            return 1;
        }
    }
}
