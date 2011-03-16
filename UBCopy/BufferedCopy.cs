using System;
using System.IO;
using log4net;

namespace UBCopy
{
    class BufferedCopy
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BufferedCopy));
        private static readonly bool IsDebugEnabled = Log.IsDebugEnabled;

        const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;

        public static int SyncCopyFileUnbuffered(string inputfile, string outputfile, bool overwrite, bool movefile, bool checksum, int buffersize, bool reportprogress)
        {
            if (IsDebugEnabled)
            {
                Log.Debug("Starting SyncCopyFileUnbuffered");
            }

            var buffer = new byte[buffersize * 1024 * 1024];

            //if the overwrite flag is set to false check to see if the file is there.
            if (File.Exists(outputfile) && !overwrite)
            {
                if (IsDebugEnabled)
                {
                    Log.Debug("Destination File Exists!");
                }
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

            var infile = new FileStream(inputfile,
                                        FileMode.Open, FileAccess.Read, FileShare.None, 8,
                                        FileFlagNoBuffering | FileOptions.SequentialScan);
            var outfile = new FileStream(outputfile, FileMode.Create, FileAccess.Write,
                                         FileShare.None, 8, FileOptions.WriteThrough);
            outfile.SetLength(infile.Length);
            try
            {
                int bytesRead;
                while ((bytesRead = infile.Read(buffer, 0, buffer.Length)) != 0)
                {
                    outfile.Write(buffer, 0, bytesRead);
                }
            }
            catch(Exception e)
            {
                if (IsDebugEnabled)
                {
                    Log.Debug("Exeption on file copy abort and delete partially copied output.");
                    Log.Debug(e);
                }
                if (File.Exists(outputfile))
                    File.Delete(outputfile);
                return 0;
            }
            finally
            {
                outfile.Close();
                outfile.Dispose();
                infile.Close();
                infile.Dispose();
            }

            if (movefile && File.Exists(inputfile) && File.Exists(outputfile))
            {
                try
                {
                    File.Delete(inputfile);
                    return 1;
                }
                catch (Exception e)
                {
                    if (IsDebugEnabled)
                    {
                        Log.Debug("Exeption on file delete on move.");
                        Log.Debug(e);
                    }
                    throw;
                }
            }
            if (IsDebugEnabled)
            {
                Log.Debug("Exit SyncCopyFileUnbuffered");
            }

            return 0;
        }
    }
}