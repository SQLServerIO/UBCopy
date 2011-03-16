using System;
using System.Collections;
using System.IO;
using log4net.ObjectRenderer;

namespace UBCopy
{
    public class ExceptionRenderer : IObjectRenderer
    {
        public void RenderObject(RendererMap rendererMap, object obj, TextWriter writer)
        {
            var thrown = obj as Exception;
            while (thrown != null)
            {
                RenderException(thrown, writer);
                thrown = thrown.InnerException;
            }
        }

        private static void RenderException(Exception ex, TextWriter writer)
        {
            writer.WriteLine(string.Format("Type: {0}", ex.GetType().FullName));
            writer.WriteLine(string.Format("Message: {0}", ex.Message));
            writer.WriteLine(string.Format("Source: {0}", ex.Source));
            writer.WriteLine(string.Format("TargetSite: {0}", ex.TargetSite));
            RenderExceptionData(ex, writer);
            writer.WriteLine(string.Format("StackTrace: {0}", ex.StackTrace));
        }

        private static void RenderExceptionData(Exception ex, TextWriter writer)
        {
            foreach (DictionaryEntry entry in ex.Data)
            {
                writer.WriteLine(string.Format("{0}: {1}", entry.Key, entry.Value));
            }
        }
    }
}
