//
// AsyncUnbuffCopy.cs
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
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace UBCopy
{
    internal class AsyncUnbuffCopy
    {
        //file names
        private static string _inputfile;
        private static string _outputfile;

        //checksum holders
        private static string _infilechecksum;
        private static string _outfilechecksum;

        //show write progress
        private static bool _reportprogress;

        //cursor position
        private static int _origRow;
        private static int _origCol;

        //number of chunks to copy
        private static int _numchunks;

        //syncronization object
        private static readonly object Locker1 = new object();

        //buffer size
        public static int CopyBufferSize;
        private static long _infilesize;

        //buffer read
        public static byte[] Buffer1;
        private static int _bytesRead1;

        //buffer overlap
        public static byte[] Buffer2;
        private static bool _buffer2Dirty;
        private static int _bytesRead2;

        //buffer write
        public static byte[] Buffer3;

        //total bytes read
        private static long _totalbytesread;
        private static long _totalbyteswritten;

        //filestreams
        private static FileStream _infile;
        private static FileStream _outfile;

        //secret sauce for unbuffered IO
        const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;

        private static void AsyncReadFile()
        {
            //open input file
            try
            {
                _infile = new FileStream(_inputfile, FileMode.Open, FileAccess.Read, FileShare.None, CopyBufferSize,
                                         FileFlagNoBuffering);

                //get input file length.
                _infilesize = _infile.Length;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to open for read");
                Debug.WriteLine(e.Message);
                throw;
            }
            //if we have data read it
            while (_totalbytesread < _infilesize)
            {
                Debug.WriteLine("Read buffer one");
                _bytesRead1 = _infile.Read(Buffer1, 0, CopyBufferSize);
                lock (Locker1)
                {
                    while (_buffer2Dirty) Monitor.Wait(Locker1);
                    Debug.WriteLine("Copy buffer overlap read");
                    Buffer.BlockCopy(Buffer1, 0, Buffer2, 0, _bytesRead1);
                    _bytesRead2 = _bytesRead1;
                    _buffer2Dirty = true;
                    _totalbytesread = _totalbytesread + _bytesRead1;
                    Monitor.Pulse(Locker1);
                }
            }
            //clean up open handle
            _infile.Close();
            _infile.Dispose();
        }


        private static void AsyncWriteFile()
        {
            //open output file set length to prevent growth and file fragmentation and close it.
            //We have to do it this way so we can do unbuffered writes to it later 
            try
            {
                _outfile = new FileStream(_outputfile, FileMode.Create, FileAccess.Write, FileShare.None, 8,
                                          FileOptions.WriteThrough);
                _outfile.SetLength(_infilesize);
                _outfile.Close();
                _outfile.Dispose();
            }
            catch(Exception e)
            {
                Debug.WriteLine("Failed to open for write");
                Debug.WriteLine(e.Message);
                throw;
            }

            //open file for write unbuffered
            _outfile = new FileStream(_outputfile, FileMode.Open, FileAccess.Write, FileShare.None, 8, FileOptions.WriteThrough | FileFlagNoBuffering);


            double pctinc = 0.0;
            double progress =pctinc;

            //progress stuff
            if (_reportprogress)
            {
                if (_numchunks > 0)
                {
                    pctinc = 100.00/_numchunks;
                }
            }

            while (_totalbyteswritten < _infilesize - CopyBufferSize)
            {
                lock (Locker1)
                {
                    while (!_buffer2Dirty) Monitor.Wait(Locker1);
                    Debug.WriteLine(_totalbyteswritten);
                    Debug.WriteLine("copy buffer overlap write");
                    Buffer.BlockCopy(Buffer2, 0, Buffer3, 0, _bytesRead2);
                    _buffer2Dirty = false;
                    Monitor.Pulse(Locker1);
                    //fancy dan in place percent update on each write.
                    if (_reportprogress)
                    {
                        Console.SetCursorPosition(_origCol, _origRow);
                        progress = progress + pctinc;
                        Console.Write("%{0}", Math.Round(progress, 0));
                    }
                    _totalbyteswritten = _totalbyteswritten + CopyBufferSize;
                }
                Debug.WriteLine("Write buffer three");
                _outfile.Write(Buffer3, 0, CopyBufferSize);
            }
            //close the file handle that was using unbuffered and write through
            _outfile.Close();
            _outfile.Dispose();

            lock (Locker1)
            {
                while (!_buffer2Dirty) Monitor.Wait(Locker1);

                //just looking at the stats and flags from the read/write threads
                Debug.WriteLine(_totalbytesread);
                Debug.WriteLine(_totalbyteswritten);
                Debug.WriteLine(_buffer2Dirty);
                Debug.WriteLine(_bytesRead2);

                //open file for write buffered We do this so we can write the tail of the file
                //it is a cludge but hey you get what you get in C#
                _outfile = new FileStream(_outputfile, FileMode.Open, FileAccess.Write, FileShare.None, 8,
                                          FileOptions.WriteThrough);

                //this should always be true but I haven't run all the edge cases yet
                if (_buffer2Dirty)
                {
                    //go to the right position in the file
                    _outfile.Seek(_infilesize - _bytesRead2, 0);
                    //peek at the file possition
                    Debug.WriteLine(_outfile.Position);
                    //flush the last buffer syncronus and buffered.
                    _outfile.Write(Buffer2, 0, _bytesRead2);
                    //check our position again
                    Debug.WriteLine(_outfile.Position);
                    Debug.WriteLine(_outfile.Length);
                }
            }
            //close the file handle that was using unbuffered and write through
            _outfile.Close();
            _outfile.Dispose();
        }

        public static int AsyncCopyFileUnbuffered(string inputfile, string outputfile, bool overwrite, bool checksum, int buffersize, bool reportprogress)
        {
            Debug.WriteLine(inputfile);
            Debug.WriteLine(outputfile);
            Debug.WriteLine(overwrite);
            Debug.WriteLine(checksum);
            Debug.WriteLine(buffersize);
            Debug.WriteLine(reportprogress);

            //report write progress
            _reportprogress = reportprogress;

            //set file name globals
            _inputfile = inputfile;
            _outputfile = outputfile;

            //setup single buffer size, remember this will be x3.
            CopyBufferSize = buffersize * 1024 * 1024;

            //debug show if we are an even multiple of the file size
            Debug.WriteLine(_infilesize / (float)CopyBufferSize);

            //buffer read
            Buffer1 = new byte[CopyBufferSize];

            //buffer overlap
            Buffer2 = new byte[CopyBufferSize];

            //buffer write
            Buffer3 = new byte[CopyBufferSize];

            //if the overwrite flag is set to false check to see if the file is there.
            if (File.Exists(outputfile) && !overwrite)
            {
                Console.WriteLine("Destination File Exists!");
                return 0;
            }

            //create the directory if it doesn't exist
            if (!Directory.Exists(outputfile))
            {
                // ReSharper disable AssignNullToNotNullAttribute
                Directory.CreateDirectory(Path.GetDirectoryName(outputfile));
                // ReSharper restore AssignNullToNotNullAttribute
            }

            //create read thread and start it.
            var readfile = new Thread(AsyncReadFile) { Name = "ReadThread", IsBackground = true };
            readfile.Start();

            //get input file size for later use
            var f = new FileInfo(_inputfile);
            long s1 = f.Length;

            //get number of buffer sized chunks used to correctly display percent complete.
            _numchunks = (int)(s1 / CopyBufferSize);

            Thread.Sleep(1000);

            //create write thread and start it.
            var writefile = new Thread(AsyncWriteFile) { Name = "WriteThread", IsBackground = true };
            writefile.Start();

            Console.WriteLine("File Copy Started");

            if (_reportprogress)
            {
                //set fancy curor position
                _origRow = Console.CursorTop;
                _origCol = Console.CursorLeft;
            }
            //wait for threads to finish
            readfile.Join();
            writefile.Join();
            Console.WriteLine();
            Console.WriteLine("File Copy Done");

            if (checksum)
            {
                Console.WriteLine("Checksum Source File Started");
                //create checksum read file thread and start it.
                var checksumreadfile = new Thread(GetMD5HashFromInputFile) { Name = "checksumreadfile", IsBackground = true };
                checksumreadfile.Start();

                Console.WriteLine("Checksum Destination File Started");
                //create checksum write file thread and start it.
                var checksumwritefile = new Thread(GetMD5HashFromOutputFile) { Name = "checksumwritefile", IsBackground = true };
                checksumwritefile.Start();

                //hang out until the checksums are done.
                checksumreadfile.Join();
                checksumwritefile.Join();

                if (_infilechecksum.Equals(_outfilechecksum))
                {
                    Console.WriteLine("Checksum Verified");
                }
                else
                {
                    Console.WriteLine("Checksum Failed");
                    Console.WriteLine("Input File Checksum : {0}", _infilechecksum);
                    Console.WriteLine("Output File Checksum: {0}", _outfilechecksum);
                }
            }
            return 1;
        }

        //hash input file
        public static void GetMD5HashFromInputFile()
        {
            var fs = new FileStream(_inputfile, FileMode.Open, FileAccess.Read, FileShare.None, CopyBufferSize);
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(fs);
            fs.Close();

            var sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            _infilechecksum = sb.ToString();
        }

        //hash output file
        public static void GetMD5HashFromOutputFile()
        {
            var fs = new FileStream(_outputfile, FileMode.Open, FileAccess.Read, FileShare.None, CopyBufferSize);
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(fs);
            fs.Close();

            var sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            _outfilechecksum = sb.ToString();
        }
    }
}