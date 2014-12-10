namespace SharpMTProto.Utils
{
    using System;

    public interface ILog
    {
        void Debug(string text);
        void Debug(Exception exception);
        void Debug(Exception exception, string message);
        void Warning(string message);
        void Error(string message);
        void Error(Exception exception, string message);
    }

    public class StandardLog : ILog
    {
        public void Debug(string message)
        {
            Write(message, "DEBUG");
        }

        public void Debug(Exception exception)
        {
            Debug(exception.Message);
        }

        public void Debug(Exception exception, string message)
        {
            Debug(string.Format("{0} Exception: {1}.", message, exception.Message));
        }

        public void Warning(string message)
        {
            Write(message, "WARNING");
        }

        public void Error(string message)
        {
#if PCL
            Write(message, "ERROR");
#else
            System.Diagnostics.Debug.Fail(message);
#endif
        }

        public void Error(Exception exception, string message)
        {
#if PCL
            Write(string.Format("{0} Exception: {1}.", message, exception.Message), "ERROR");
#else
            System.Diagnostics.Debug.Fail(message, exception.Message);
#endif
        }

        private void Write(string message, string category)
        {
            System.Diagnostics.Debug.WriteLine("[{0}] {1}", category, message);
        }
    }

    public class LogManager
    {
        private static readonly ILog Log = new StandardLog();

        public static ILog GetCurrentClassLogger()
        {
            // TODO: implement normal logging.
            return Log;
        }
    }
}
