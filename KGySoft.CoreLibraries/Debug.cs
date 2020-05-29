﻿#region Copyright

///////////////////////////////////////////////////////////////////////////////
//  File: Debug.cs
///////////////////////////////////////////////////////////////////////////////
//  Copyright (C) KGy SOFT, 2005-2020 - All Rights Reserved
//
//  You should have received a copy of the LICENSE file at the top-level
//  directory of this distribution. If not, then this file is considered as
//  an illegal copy.
//
//  Unauthorized copying of this file, via any medium is strictly prohibited.
///////////////////////////////////////////////////////////////////////////////

#endregion

#region Usings

using System.Diagnostics;
using SystemDebug = System.Diagnostics.Debug;

#endregion

namespace KGySoft
{
    internal static class Debug
    {
        #region Methods

#if !NETFRAMEWORK
        private static bool everAttached; 
#endif

        [Conditional("DEBUG")]
        internal static void Assert(bool condition, string message = null)
        {
#if NETFRAMEWORK
            SystemDebug.Assert(condition, message);
#else
            if (!condition)
                Fail(message);
#endif
        }

        [Conditional("DEBUG")]
        internal static void Fail(string message)
        {
#if NETFRAMEWORK
            SystemDebug.Fail(message);
#else
            SystemDebug.WriteLine("Debug failure occurred - " + (message ?? "No message"));

            // preventing the attach dialog come up if already attached it once
            if (!everAttached)
                everAttached = Debugger.IsAttached;
            if (!everAttached)
            {
                Debugger.Launch();
                everAttached = true;
            }
            else
                Debugger.Break();
#endif
        }

        [Conditional("DEBUG")]
        internal static void WriteLine(string message) => SystemDebug.WriteLine(message);

        #endregion
    }
}