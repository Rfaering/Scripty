﻿using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Scripty.Core.Tests.Output
{
    [TestFixture]
    public class FormatterFixture
    {
        static readonly string ProjectFilePath = Path.GetFullPath($"{AppDomain.CurrentDomain.BaseDirectory}/../../Scripty.Core.Tests.csproj");
        static readonly string BinDirectory = Path.GetFullPath($"{AppDomain.CurrentDomain.BaseDirectory}/Output");

        const string FakeFormatterScript = "FakeFormatter.csx";
        const string NotFormattedOutput = "FakeFormatter.NoFormat.cs";
        const string ExpectedNotFormattedOutput = "FakeFormatter.NoFormat.cs.expected";
        const string FormattedOutput = "FakeFormatter.Formatted.cs";
        const string ExpectedFormattedOutput = "FakeFormatter.Formatted.cs.expected";
        const string UserFormattedOutput = "FakeFormatter.UserFormatted.cs";
        const string ExpectedUserFormattedOutput = "FakeFormatter.UserFormatted.cs.expected";

        private ScriptEngine _engine;

        [SetUp]
        public void Init()
        {
            _engine = new ScriptEngine(ProjectFilePath);

            File.Delete(GetFilePath(NotFormattedOutput));
            File.Delete(GetFilePath(FormattedOutput));
            File.Delete(GetFilePath(UserFormattedOutput));
        }
        
        [Test]
        public void TestFormatter()
        {
            var result = _engine.Evaluate(new ScriptSource(GetFilePath(FakeFormatterScript), GetFileContent(FakeFormatterScript))).Result;
            
            Assert.That(File.Exists(GetFilePath(NotFormattedOutput)));
            Assert.AreEqual(GetFileContent(ExpectedNotFormattedOutput), GetFileContent(NotFormattedOutput));

            Assert.That(File.Exists(GetFilePath(FormattedOutput)));
            Assert.AreEqual(GetFileContent(ExpectedFormattedOutput), GetFileContent(FormattedOutput));

            Assert.That(File.Exists(GetFilePath(UserFormattedOutput)));
            Assert.AreEqual(GetFileContent(ExpectedUserFormattedOutput), GetFileContent(UserFormattedOutput));
        }

        string GetFilePath(string fileName)
        {
            return Path.Combine(BinDirectory, fileName);
        }

        string GetFileContent(string fileName)
        {
            return File.ReadAllText(GetFilePath(fileName));
        }
    }
}
