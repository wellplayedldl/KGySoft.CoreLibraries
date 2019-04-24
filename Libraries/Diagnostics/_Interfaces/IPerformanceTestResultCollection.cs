﻿#region Copyright

///////////////////////////////////////////////////////////////////////////////
//  File: IPerformanceTestResultCollection.cs
///////////////////////////////////////////////////////////////////////////////
//  Copyright (C) KGy SOFT, 2005-2019 - All Rights Reserved
//
//  You should have received a copy of the LICENSE file at the top-level
//  directory of this distribution. If not, then this file is considered as
//  an illegal copy.
//
//  Unauthorized copying of this file, via any medium is strictly prohibited.
///////////////////////////////////////////////////////////////////////////////

#endregion

#region Usings

using System;
using System.Collections.Generic;
using System.IO;

#endregion

namespace KGySoft.Diagnostics
{
    /// <summary>
    /// Represents the performance test results of a <see cref="PerformanceTestBase"/> implementation.
    /// Items are sorted either by execution time (ascending - if <see cref="PerformanceTestBase.Iterations"/> was specified in the original test),
    /// by performed iterations (descending - if <see cref="PerformanceTestBase.TestTime"/> was specified),
    /// or by result size (ascending - if <see cref="PerformanceTestBase.SortBySize"/> was <see langword="true"/>).
    /// <br/>See the <strong>Examples</strong> section of the <see cref="PerformanceTest"/> class for some examples.
    /// </summary>
    public interface IPerformanceTestResultCollection :
#if NET35 || NET40
        IList<ITestCaseResult>
#else
        IReadOnlyList<ITestCaseResult>
#endif
    {
        #region Methods

        /// <summary>
        /// Dumps the results by the specified <paramref name="textWriter"/> by using predefined localizable resources.
        /// </summary>
        /// <param name="textWriter">The text writer to use for dumping the results.</param>
        /// <param name="dumpConfig"><see langword="true"/> to dump the test configuration; otherwise, <see langword="false"/>. This parameter is optional.
        /// <br/>Default value: <see langword="true"/>.</param>
        /// <param name="dumpReturnValue"><see langword="true"/> to dump also the return value of the test cases. Ignored, when the delegate type of the test has a <see cref="Void"/> return value. This parameter is optional.
        /// <br/>Default value: <see langword="false"/>.</param>
        /// <remarks>
        /// <para>If <paramref name="dumpConfig"/> is <see langword="true"/>, then please note that the configuration properties are taken from the
        /// <see cref="PerformanceTestBase"/> instance at the moment the results are dumped. If you change the configuration after retrieving a result the new values will be reflected in the dumped values.</para>
        /// <para>If <paramref name="dumpReturnValue"/> is <see langword="true"/>, then the lastly returned value will also be dumped. Can be useful for tests, which measure size instead of execution time.
        /// In this case it can make sense to set <see cref="PerformanceTestBase.SortBySize"/> to <see langword="true"/>.
        /// </para>
        /// </remarks>
        void DumpResults(TextWriter textWriter, bool dumpConfig = true, bool dumpReturnValue = false);

        #endregion
    }
}
