﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace _LibrariesTest
{
    using System.Globalization;
    using System.Threading;

    using KGySoft.Libraries;

    [TestClass]
    public class LanguageTest
    {
        [TestMethod]
        public void TestMethod()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            Language.FormattingLanguageChanged += (sender, e) => PrintThread("FormattingLanguageChanged");
            Language.FormattingLanguageChangedGlobal += (sender, e) => PrintThread("FormattingLanguageChangedGlobal " + (threadId == Thread.CurrentThread.ManagedThreadId));
            PrintThread("Main");
            Console.WriteLine("Setting en-GB in main");
            Language.FormattingLanguage = CultureInfo.GetCultureInfo("en-GB");
            Console.WriteLine();
            //ManualResetEvent mre = new ManualResetEvent(false);

            ThreadStart threadStart = () =>
                {
                    Console.WriteLine("Setting hu-HU in work thread");
                    Language.FormattingLanguage = CultureInfo.GetCultureInfo("hu-HU");
                    Console.WriteLine();
                    //mre.Set();
                };
            Thread t = new Thread(threadStart);
            t.Start();
            t.Join();
            PrintThread("Main");
        }

        private void PrintThread(string message)
        {
            Console.WriteLine(message + " ThreadID: {0}; Culture: {1}; UICulture: {2}", Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture);
        }
    }
}
