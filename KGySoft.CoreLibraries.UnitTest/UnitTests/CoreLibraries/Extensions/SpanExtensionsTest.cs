﻿#if !(NETFRAMEWORK || NETSTANDARD2_0 || NETCOREAPP2_0)
#region Copyright

///////////////////////////////////////////////////////////////////////////////
//  File: SpanExtensionsTest.cs
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

using System;

using KGySoft.Reflection;

using NUnit.Framework;

#endregion

namespace KGySoft.CoreLibraries.UnitTests.CoreLibraries.Extensions
{
    [TestFixture]
    public class SpanExtensionsTest : TestBase
    {
        #region Methods

        [Test]
        public void ReadToWhiteSpaceTest()
        {
            ReadOnlySpan<char> ss = null;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadToWhiteSpace().ToString());

            ss = ReadOnlySpan<char>.Empty;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadToWhiteSpace().ToString());
            Assert.IsTrue(ss.IsEmpty);

            ss = "alpha beta\tgamma\r\ndelta ";
            Assert.AreEqual("alpha", ss.ReadToWhiteSpace().ToString());
            Assert.AreEqual("beta", ss.ReadToWhiteSpace().ToString());
            Assert.AreEqual("gamma", ss.ReadToWhiteSpace().ToString());
            Assert.AreEqual("", ss.ReadToWhiteSpace().ToString());
            Assert.AreEqual("delta", ss.ReadToWhiteSpace().ToString());
            Assert.AreEqual("", ss.ReadToWhiteSpace().ToString());
            Assert.IsTrue(ss.IsEmpty);
        }

        [Test]
        public void ReadToSeparatorCharTest()
        {
            var sep = ' ';
            ReadOnlySpan<char> ss = null;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadToSeparator(sep).ToString());

            ss = ReadOnlySpan<char>.Empty;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadToSeparator(sep).ToString());
            Assert.IsTrue(ss.IsEmpty);

            ss = "alpha, beta gamma  delta ";
            Assert.AreEqual("alpha,", ss.ReadToSeparator(sep).ToString());
            Assert.AreEqual("beta", ss.ReadToSeparator(sep).ToString());
            Assert.AreEqual("gamma", ss.ReadToSeparator(sep).ToString());
            Assert.AreEqual("", ss.ReadToSeparator(sep).ToString());
            Assert.AreEqual("delta", ss.ReadToSeparator(sep).ToString());
            Assert.AreEqual("", ss.ReadToSeparator(sep).ToString());
            Assert.IsTrue(ss.IsEmpty);
        }

        [Test]
        public void ReadToSeparatorSpanTest()
        {
            ReadOnlySpan<char> ss = null;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadToSeparator(ReadOnlySpan<char>.Empty).ToString());
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadToSeparator(" ").ToString());
            Assert.IsTrue(ss.IsEmpty);

            ss = ReadOnlySpan<char>.Empty;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadToSeparator(ReadOnlySpan<char>.Empty).ToString());
            Assert.IsTrue(ss.IsEmpty);

            ss = " ".AsSpan();
            Assert.AreEqual(" ", ss.ReadToSeparator(ReadOnlySpan<char>.Empty).ToString());
            Assert.IsTrue(ss.IsEmpty);

            ss = "alpha, beta gamma  delta ";
            ReadOnlySpan<char> sep = ", ";
            Assert.AreEqual("alpha", ss.ReadToSeparator(sep).ToString());
            Assert.AreEqual("beta gamma  delta ", ss.ReadToSeparator(sep).ToString());
            Assert.IsTrue(ss.IsEmpty);
        }

        [Test]
        public void ReadToSeparatorCharArrayTest()
        {
            char[] sep = { ' ', ',' };

            ReadOnlySpan<char> ss = null;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadToSeparator(Reflector.EmptyArray<char>()).ToString());
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadToSeparator(sep).ToString());
            Assert.IsTrue(ss.IsEmpty);

            ss = ReadOnlySpan<char>.Empty;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadToSeparator(sep).ToString());
            Assert.IsTrue(ss.IsEmpty);

            ss = " ".AsSpan();
            Assert.AreEqual(" ", ss.ReadToSeparator(Reflector.EmptyArray<char>()).ToString());
            Assert.IsTrue(ss.IsEmpty);

            ss = "alpha, beta ";
            Assert.AreEqual("alpha", ss.ReadToSeparator(sep).ToString());
            Assert.AreEqual("", ss.ReadToSeparator(sep).ToString());
            Assert.AreEqual("beta", ss.ReadToSeparator(sep).ToString());
            Assert.AreEqual("", ss.ReadToSeparator(sep).ToString());
            Assert.IsTrue(ss.IsEmpty);
        }

        [Test]
        public void ReadLineTest()
        {
            ReadOnlySpan<char> ss = null;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadLine().ToString());

            ss = ReadOnlySpan<char>.Empty;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.ReadLine().ToString());
            Assert.IsTrue(ss.IsEmpty);

            ss = "Line1\r\nLine2\rLine3\nLine4";
            Assert.AreEqual("Line1", ss.ReadLine().ToString());
            Assert.AreEqual("Line2", ss.ReadLine().ToString());
            Assert.AreEqual("Line3", ss.ReadLine().ToString());
            Assert.AreEqual("Line4", ss.ReadLine().ToString());
            Assert.IsTrue(ss.IsEmpty);
        }

        [Test]
        public void ReadTest()
        {
            ReadOnlySpan<char> ss = null;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.Read(1).ToString());

            ss = ReadOnlySpan<char>.Empty;
            Assert.AreEqual(ReadOnlySpan<char>.Empty.ToString(), ss.Read(1).ToString());
            Assert.IsTrue(ss.IsEmpty);

            ss = "123";
            Assert.AreEqual("1", ss.Read(1).ToString());
            Assert.AreEqual("23", ss.ToString());
            Assert.AreEqual("23", ss.Read(10).ToString());
            Assert.IsTrue(ss.IsEmpty);
        }

        [TestCase(null, null)]
        [TestCase("", "")]
        [TestCase("a", "a")]
        [TestCase("alpha", "alpha")]
        [TestCase("\"", "\"")]
        [TestCase("'", "'")]
        [TestCase("'\"", "'\"")]
        [TestCase("\"\"", "")]
        [TestCase("''", "")]
        [TestCase("'a'", "a")]
        [TestCase("\"a\"", "a")]
        public void RemoveQuotesTest(string s, string expectedResult)
        {
            Assert.AreEqual(expectedResult ?? String.Empty, s.AsSpan().RemoveQuotes().ToString());
            Assert.AreEqual(expectedResult?.ToCharArray() ?? Reflector.EmptyArray<char>(), (s?.ToCharArray() ?? Reflector.EmptyArray<char>()).AsSpan().RemoveQuotes().ToArray());
        }

        [TestCaseGeneric(null, null, TypeArguments = new[] { typeof(ConsoleColor) })]
        [TestCaseGeneric("x", null, TypeArguments = new[] { typeof(ConsoleColor) })]
        [TestCaseGeneric("Black", ConsoleColor.Black, TypeArguments = new[] { typeof(ConsoleColor) })]
        [TestCaseGeneric("-1", (ConsoleColor)(-1), TypeArguments = new[] { typeof(ConsoleColor) })]
        public void ToEnumTest<TEnum>(string s, TEnum? expectedResult)
            where TEnum : struct, Enum
        {
            Assert.AreEqual(s.AsSpan().ToEnum<TEnum>(), expectedResult);
        }

        #endregion
    }
}
#endif