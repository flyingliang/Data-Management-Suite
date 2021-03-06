﻿using System;
using FalconSoft.Data.Management.Common;

namespace FalconSoft.Data.Console
{
    public class Logger : ILogger
    {
        private readonly log4net.ILog _log;

        public Logger()
        {
            log4net.Config.XmlConfigurator.Configure();

            _log = log4net.LogManager.GetLogger("Main Log");
        }

        public void Debug(string message)
        {
            _log.Debug(message);
            System.Console.WriteLine(message);
        }

        public void Debug(string message, Exception exception)
        {
            _log.Debug(message, exception);
            System.Console.WriteLine(message + string.Format("Exception : {0}", exception.Message));
        }

        public void DebugFormat(string format, params object[] args)
        {
            _log.DebugFormat(format, args);
            System.Console.WriteLine(format, args);
        }

        public void Error(string message)
        {
            _log.Error(message);
            System.Console.WriteLine(message);
        }

        public void Error(string message, Exception exception)
        {
            _log.Error(message, exception);
            System.Console.WriteLine(message + string.Format("Exception : {0}", exception.Message));
        }

        public void ErrorFormat(string format, params object[] args)
        {
            _log.ErrorFormat(format, args);
            System.Console.WriteLine(format, args);
        }

        public void Info(string message)
        {
            _log.Info(message);
            System.Console.WriteLine(message);
        }

        public void InfoFormat(string format, params object[] args)
        {
            _log.InfoFormat(format, args);
            System.Console.WriteLine(format, args);
        }

        public void Warn(object message)
        {
            _log.Warn(message);
            System.Console.WriteLine(message);
        }

        public void Warn(object message, Exception exception)
        {
            _log.Warn(message, exception);
            System.Console.WriteLine(message + string.Format("Exception : {0}", exception.Message));
        }

        public void WarnFormat(string format, params object[] args)
        {
            _log.WarnFormat(format, args);
            System.Console.WriteLine(format, args);
        }
    }
}
