﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using KGySoft;
using KGySoft.CoreLibraries;
using KGySoft.Reflection;
using KGySoft.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace _LibrariesTest.Tests
{
    [TestClass]
    public class ResTests
    {
        private const string unavailableResourcePrefix = "Resource ID not found";
        private const string invalidResourcePrefix = "Resource text is not valid";
        private static readonly Random random = new Random();

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            LanguageSettings.DynamicResourceManagersSource = ResourceManagerSources.CompiledOnly;
        }

        [TestMethod]
        public void TestUnknownResource()
        {
            Assert.IsTrue(Reflector.RunMethod(typeof(Res), "Get", "unknown").ToString().StartsWith(unavailableResourcePrefix, StringComparison.Ordinal));
        }

        [TestMethod]
        public void TestInvalidResource()
        {
            Assert.IsTrue(Reflector.RunMethod(typeof(Res), "Get", "General_NotAnInstanceOfTypeFormat", new object[0]).ToString().StartsWith(invalidResourcePrefix, StringComparison.Ordinal));
        }

        [TestMethod]
        public void TestResources()
        {
            var obtainedMembers = new HashSet<string>();

            // note: these should be 3 different tests but if coverage is tested in ClassCleanup method, then the assert is suppressed
            CheckProperties(obtainedMembers);
            CheckMethods(obtainedMembers);
            CheckCoverage(obtainedMembers);
        }

        private void CheckProperties(HashSet<string> obtainedMembers)
        {
            PropertyInfo[] properties = typeof(Res).GetProperties(BindingFlags.Static | BindingFlags.NonPublic);
            foreach (PropertyInfo property in properties)
            {
                string value = property.GetValue(null, null).ToString();
                Assert.IsTrue(!value.StartsWith(unavailableResourcePrefix, StringComparison.Ordinal), $"{nameof(Res)}.{property.Name} refers to an undefined resource.");
                Assert.IsTrue(!value.ContainsAny("{", "}"), $"{nameof(Res)}.{property.Name} refers to a parameterized resource.");
                obtainedMembers.Add(property.Name);
            }
        }

        private void CheckMethods(HashSet<string> obtainedMembers)
        {
            IEnumerable<MethodInfo> methods = typeof(Res).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Where(m => m.IsAssembly);
            var generateSettings = new GenerateObjectSettings { AllowCreateObjectWithoutConstructor = true }; // for PropertyDescriptors
            foreach (MethodInfo mi in methods)
            {
                var method = mi.IsGenericMethodDefinition ? mi.MakeGenericMethod(random.NextObject(typeof(Enum)).GetType()) : mi;
                object[] parameters = method.GetParameters().Select(p => random.NextObject(p.ParameterType, generateSettings)).ToArray();
                string value = method.Invoke(null, parameters).ToString();
                Assert.IsTrue(!value.StartsWith(unavailableResourcePrefix, StringComparison.Ordinal), $"{nameof(Res)}.{method.Name} refers to an undefined resource.");
                Assert.IsTrue(!value.StartsWith(invalidResourcePrefix, StringComparison.Ordinal), $"{nameof(Res)}.{method.Name} uses too few parameters.");
                for (int i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    Assert.IsTrue(value.Contains(parameter.ToString(), StringComparison.Ordinal)
                        || mi.IsGenericMethodDefinition // Xxx<TEnum>(TEnum value) - not value but possible TValue is printed
                        || parameter is float f && value.Contains(f.ToString("P2"), StringComparison.Ordinal), // percentage format of float
                        $"{nameof(Res)}.{method.Name} does not use parameter #{i}.");}
                obtainedMembers.Add(method.Name);
            }
        }

        private void CheckCoverage(HashSet<string> obtainedMembers)
        {
            var rm = (ResourceManager)Reflector.GetField(typeof(Res), "resourceManager");
            ResourceSet rs = rm.GetResourceSet(CultureInfo.InvariantCulture, true, false);
            IDictionaryEnumerator enumerator = rs.GetEnumerator();
            var uncovered = new List<string>();
            while (enumerator.MoveNext())
            {
                // ReSharper disable once PossibleNullReferenceException
                string key = ((string)enumerator.Key).Replace("_", String.Empty);
                if (key.StartsWith("General", StringComparison.Ordinal))
                    key = key.Substring("General".Length);
                if (key.EndsWith("Format", StringComparison.Ordinal))
                    key = key.Substring(0, key.Length - "Format".Length);
                if (!obtainedMembers.Contains(key))
                    uncovered.Add((string)enumerator.Key);
            }

            Assert.IsTrue(uncovered.Count == 0, $"{uncovered.Count} orphan compiled resources detected:{Environment.NewLine}{String.Join(Environment.NewLine, uncovered.ToArray())}");
        }
    }
}