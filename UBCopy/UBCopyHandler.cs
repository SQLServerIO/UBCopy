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
using System.Diagnostics;
using System.IO;
using System.Threading;
using log4net;
using TestMultithreadFileCopy;

namespace UBCopy
{
    public class UBCopyHandler
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UBCopyHandler));

        public static int ProcessFiles(string inputfile, string outputfile, bool overwrite, bool movefile, bool checksum, int buffersize, bool reportprogress, int numberthreads, int synchronousFileCopySize, int bytesSecond)
        {
            if (string.IsNullOrEmpty(outputfile))
                throw new Exception("Target cannot be empty");

            var inputIsFile = false;
            UBCopySetup.Buffersize = buffersize;
            UBCopySetup.Checksumfiles = checksum;
            UBCopySetup.Destinationfile = outputfile;
            UBCopySetup.Movefile = movefile;
            UBCopySetup.Reportprogres = reportprogress;
            UBCopySetup.SynchronousFileCopySize = synchronousFileCopySize * 1024 * 1024;
            UBCopySetup.BytesSecond = bytesSecond;
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
                    inputIsFile = true;
                    UBCopySetup.FileList.Push(inputfile);
                }

            }
            catch (Exception)
            {
                Log.ErrorFormat("Invalid path or filename:{0}", inputfile);
                return 0;
            }

            if (inputIsFile)
            {
                var outputExistsAttributes = IsFileOrDirectoryAndExists(outputfile);

                if (outputExistsAttributes == 1)
                {
                    if (UBCopySetup.FileList.Count > 1)
                    {
                        Log.Error("Cannot write multiple files to a single target file.");
                        return 0;
                    }
                }
                if (outputExistsAttributes == 2)
                {
                    if (UBCopySetup.FileList.Count == 1)
                    {
                        Log.Error("No Output File Name Specified.");
                        return 0;
                    }
                }
                //if (outputExistsAttributes == 3)
                //{
                //    try
                //    {
                //        var di = Directory.CreateDirectory(outputfile);
                //        Log.DebugFormat("The directory was created successfully at {0}.",
                //                        Directory.GetCreationTime(outputfile));
                //        Log.Debug(di.Attributes);
                //    }
                //    catch (Exception)
                //    {
                //        if (File.Exists(outputfile))
                //        {
                //            Log.Fatal("Create Output Directory Failed.");
                //            throw;
                //        }
                //    }
                //}
            }

            if (UBCopySetup.FileList.Count < numberthreads)
            {
                numberthreads = UBCopySetup.FileList.Count;
            }

            if (inputIsFile)
                numberthreads = 1;

            Log.InfoFormat("Number of Files To Process: {0}", UBCopySetup.FileList.Count);
            var sw = new Stopwatch();
            sw.Start();
            var ftph = UBCopySetup.FileList.Count;

            if (numberthreads == 1)
            {
                foreach (var file in UBCopySetup.FileList)
                {

                    var destinationfile = inputIsFile == false
                                              ? Path.Combine(UBCopySetup.Destinationfile,
                                                             file.Replace(Path.GetPathRoot(file), ""))
                                              : UBCopySetup.Destinationfile;
                    var fileSize = new FileInfo(file);

                    Log.DebugFormat("File Size: {0}", fileSize.Length);
                    UBCopySetup.BytesCopied += fileSize.Length;

                    AsyncUnbuffCopyStatic.AsyncCopyFileUnbuffered(file, destinationfile,
                                                                  UBCopySetup.Overwritedestination,
                                                                  UBCopySetup.Movefile,
                                                                  UBCopySetup.Checksumfiles, UBCopySetup.Buffersize,
                                                                  UBCopySetup.Reportprogres, UBCopySetup.BytesSecond);
                }

            }
            else
            {
                var doneEvents = new ManualResetEvent[numberthreads];
                var hashFilesArray = new UBCopyProcessor[numberthreads];
                var ftp = UBCopySetup.FileList.Count;


                while (ftp > 0)
                {
                    // Configure and launch threads using ThreadPool:
                    if (ftp < numberthreads)
                        numberthreads = ftp;
                    for (var i = 0; i < numberthreads; i++)
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
            }
            sw.Stop();
            Log.InfoFormat("ElapsedSeconds      : {0}", (sw.ElapsedMilliseconds / 1000.00));
            Log.InfoFormat("Files Per Second    : {0}", ftph / (sw.ElapsedMilliseconds / 1000.00));
            Log.InfoFormat("Megabytes per Second: {0}", (UBCopySetup.BytesCopied / (sw.ElapsedMilliseconds / 1000.00)) / 1048576);
            return 1;
        }

        private static int IsFileOrDirectoryAndExists(string path)
        {
            try
            {
                // get the file attributes for file or directory
                var attr = File.GetAttributes(path);

                //detect whether its a directory or file
                return (attr & FileAttributes.Directory) == FileAttributes.Directory ? 2 : 1;
            }
            catch
            {
                return 3;
            }
        }
    }
}
