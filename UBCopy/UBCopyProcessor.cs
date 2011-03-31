using System;
using System.IO;
using System.Threading;
using log4net;
using UBCopy;

namespace TestMultithreadFileCopy
{
    class UBCopyProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UBCopyProcessor));

        private readonly ManualResetEvent _doneEvent;

        private static readonly object Locker = "";

        /// <summary>
        /// called to spool up copy
        /// </summary>
        /// <param name="doneEvent"></param>
        public UBCopyProcessor(ManualResetEvent doneEvent)
        {
            _doneEvent = doneEvent;
        }
        /// <summary>
        /// setup callback
        /// </summary>
        /// <param name="threadContext"></param>
        public void MyProcessThreadPoolCallback(Object threadContext)
        {
            UBCopyFile();
            _doneEvent.Set();
        }

        /// <summary>
        /// handles the file copy
        /// </summary>
        private static void UBCopyFile()
        {
            string file;
            //lock (UBCopySetup.FileList)
            lock (UBCopySetup.DictonaryLocker)
            {
                file = UBCopySetup.FileList.Pop();
                Log.DebugFormat("POP FILE: {0}", file);
            }
            if (String.IsNullOrEmpty(file))
            {
                throw new Exception("File Name Cannot Be Null");
            }
            var destinationfile = Path.Combine(UBCopySetup.Destinationfile, file.Replace(Path.GetPathRoot(file), ""));
            var fileSize = new FileInfo(file);

            Log.DebugFormat("File Size: {0}", fileSize.Length);
            lock (Locker)
            {
                UBCopySetup.BytesCopied += fileSize.Length;
            }
            if (fileSize.Length < UBCopySetup.SynchronousFileCopySize)
            {
                var asyncUnbufferedCopy = new AsyncUnbuffCopy();
                asyncUnbufferedCopy.AsyncCopyFileUnbuffered(file, destinationfile, UBCopySetup.Overwritedestination,
                                                            UBCopySetup.Movefile,
                                                            UBCopySetup.Checksumfiles, UBCopySetup.Buffersize,
                                                            UBCopySetup.Reportprogres);
            }
            else
            {
                AsyncUnbuffCopyStatic.AsyncCopyFileUnbuffered(file, destinationfile, UBCopySetup.Overwritedestination,
                                                            UBCopySetup.Movefile,
                                                            UBCopySetup.Checksumfiles, UBCopySetup.Buffersize,
                                                            UBCopySetup.Reportprogres);
            }
        }
    }
}