using System;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading;
using log4net;

namespace UBCopy
{
    class BufferedCopy
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BufferedCopy));
        private static readonly bool IsDebugEnabled = Log.IsDebugEnabled;

        const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;

        public static int SyncCopyFileUnbuffered(string inputfile, string outputfile, int buffersize,int bytessecond, out byte[] readhash)
        {
            var md5 = MD5.Create();
            var throttleSw = new Stopwatch();
            long elapsedms = 0;

            if (IsDebugEnabled)
            {
                Log.Debug("Starting SyncCopyFileUnbuffered");
            }

            var buffer = new byte[buffersize];
            FileStream infile;
            FileStream outfile;
            Log.DebugFormat("attempting to lock file {0}", inputfile);
            try
            {
                infile = new FileStream(inputfile,
                                            FileMode.Open, FileAccess.Read, FileShare.None, buffersize,
                                            FileFlagNoBuffering | FileOptions.SequentialScan);

            }
            catch (Exception)
            {
                Log.Debug(inputfile);
                throw;
            }

            try
            {
                outfile = new FileStream(outputfile, FileMode.Create, FileAccess.Write,
                                             FileShare.None, buffersize, FileOptions.WriteThrough);

            }
            catch (Exception)
            {
                Log.Debug(outputfile);
                throw;
            }

            outfile.SetLength(infile.Length);
            try
            {
                int bytesRead;
                while ((bytesRead = infile.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (bytessecond > 0)
                    {
                        throttleSw.Start();
                        outfile.Write(buffer, 0, bytesRead);
                        if (UBCopySetup.Checksumfiles)
                            md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                        throttleSw.Stop();
                        Log.DebugFormat("Time To Write: {0} ", throttleSw.ElapsedMilliseconds);
                        elapsedms = throttleSw.ElapsedMilliseconds;

                        if (bytessecond >= bytesRead && elapsedms < 1000)
                        {
                            Throttle(elapsedms);
                            throttleSw.Reset();
                        }
                    }
                    else
                    {
                        outfile.Write(buffer, 0, bytesRead);
                        if (UBCopySetup.Checksumfiles)
                            md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                    }

                }
                // For last block:
                if (UBCopySetup.Checksumfiles)
                {
                    md5.TransformFinalBlock(buffer, 0, bytesRead);
                    readhash = md5.Hash;
                }
                else
                {
                    readhash = new byte[1];
                }

            }
            catch (Exception e)
            {
                if (IsDebugEnabled)
                {
                    Log.Debug("Exeption on file copy abort and delete partially copied output.");
                    Log.Debug(e);
                }
                if (File.Exists(outputfile))
                    File.Delete(outputfile);
                readhash = new byte[1];
                return 0;
            }
            finally
            {
                outfile.Close();
                outfile.Dispose();
                infile.Close();
                infile.Dispose();
            }
            Log.InfoFormat("Unbuffered Sync File {0} Done", inputfile);
            if (IsDebugEnabled)
            {
                Log.Debug("Exit SyncCopyFileUnbuffered");
            }

            return 0;
        }
        /// <summary>
        /// Throttles for the specified buffer by milliseconds
        /// </summary>
        /// <param name="elapsedMilliseconds">number of milliseconds elapsed</param>
        static private void Throttle(long elapsedMilliseconds)
        {
            if (elapsedMilliseconds > 0)
            {
                // Calculate the time to sleep.
                int toSleep = (int)(1000 - elapsedMilliseconds);

                if (toSleep > 1)
                {
                    Log.Debug("Throttling");
                    try
                    {
                        // The time to sleep is more then a millisecond, so sleep.
                        Thread.Sleep(toSleep);
                    }
                    catch (ThreadAbortException)
                    {
                        // Eatup ThreadAbortException.
                    }
                }
            }
        }

    }
}