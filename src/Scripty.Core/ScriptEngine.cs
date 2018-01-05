﻿using Buildalyzer.Workspaces;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Scripty.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Scripty.Core
{
    /// <summary>
    /// 
    /// </summary>
    public class ScriptEngine
    {
        private static readonly Assembly[] ReferenceAssemblies = new Assembly[]
        {
            typeof(object).Assembly, // mscorlib
            typeof(Workspace).Assembly, // Microsoft.CodeAnalysis.Workspaces
            typeof(ProjectInstance).Assembly, // Microsoft.Build.Execution
            typeof(ILogger).Assembly, // Microsoft.Extensions.Logging
            typeof(ScriptEngine).Assembly // Scripty.Core
        };

        private static readonly string[] Namespaces = new string[]
        {
            "System",
            "System.Collections.Generic",
            "System.IO",
            "System.Linq",
            "Microsoft.Extensions.Logging",
            "Microsoft.CodeAnalysis",
            "Microsoft.CodeAnalysis.Workspaces",
            "Microsoft.Build.Execution",
            "Scripty.Core"
        };

        // Copied from https://github.com/AArnott/CodeGeneration.Roslyn/blob/87c544b36de8b6cb4231e32ab6082d9ebd766d5f/src/CodeGeneration.Roslyn/DocumentTransform.cs
        private static readonly string GeneratedByAToolPreamble = @"// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------
".Replace("\r\n", "\n").Replace("\n", Environment.NewLine); // Normalize regardless of git checkout policy

        public ScriptEngine(string projectFilePath)
            : this(projectFilePath, (string)null)
        {
            LoggerFactory = new LoggerFactory();
        }

        public ScriptEngine(string projectFilePath, ILoggerFactory loggerFactory)
            : this(projectFilePath, (string)null)
        {
            LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public ScriptEngine(string projectFilePath, string solutionFilePath, ILoggerFactory loggerFactory)
            : this(projectFilePath, solutionFilePath)
        {
            LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public ScriptEngine(string projectFilePath, TextWriter logWriter)
            : this(projectFilePath, (string)null)
        {
            LoggerFactory = GetLoggerFactory(logWriter);
        }

        public ScriptEngine(string projectFilePath, string solutionFilePath, TextWriter logWriter)
            : this(projectFilePath, solutionFilePath)
        {
            LoggerFactory = GetLoggerFactory(logWriter);
        }

        public ScriptEngine(string projectFilePath, string solutionFilePath)
        {
            if (string.IsNullOrEmpty(projectFilePath))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(projectFilePath));
            }
            if (!Path.IsPathRooted(projectFilePath))
            {
                throw new ArgumentException("Project path must be absolute", nameof(projectFilePath));
            }
            ProjectFilePath = projectFilePath;

            // The solution path is optional. If it's provided, the solution will be loaded and 
            // the project found in the solution. If not, then the project is loaded directly.
            if (solutionFilePath != null)
            {
                if (!Path.IsPathRooted(solutionFilePath))
                {
                    throw new ArgumentException("Solution path must be absolute", nameof(solutionFilePath));
                }
            }
            SolutionFilePath = solutionFilePath;

            Project = new Project(this);
        }

        private static ILoggerFactory GetLoggerFactory(TextWriter logWriter)
        {
            if (logWriter == null)
            {
                throw new ArgumentNullException(nameof(logWriter));
            }
            LoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new TextWriterLoggerProvider(logWriter));
            return loggerFactory;
        }

        internal ILoggerFactory LoggerFactory { get; }

        public string ProjectFilePath { get; }

        public string SolutionFilePath { get; }

        public Project Project { get; }

        public async Task<bool> Evaluate(ScriptSource source, string outputPath)
        {
            if (outputPath == null)
            {
                throw new ArgumentNullException(nameof(outputPath));
            }

            ILogger logger = LoggerFactory.CreateLogger<ScriptEngine>();

            ScriptOptions options = ScriptOptions.Default
                .WithFilePath(source.FilePath)
                .WithReferences(ReferenceAssemblies)
                .WithImports(Namespaces);

            using (Stream outputStream = File.OpenWrite(outputPath))
            {
                using (StreamWriter outputWriter = new StreamWriter(outputStream))
                {
                    // Write the preamble
                    await outputWriter.WriteLineAsync(GeneratedByAToolPreamble);

                    // Evaluate the script
                    try
                    {
                        ScriptContext context = new ScriptContext(this, source.FilePath, outputWriter);
                        await CSharpScript.EvaluateAsync(source.Code, options, context);
                    }
                    catch (CompilationErrorException compilationError)
                    {
                        foreach (Diagnostic diagnostic in compilationError.Diagnostics)
                        {
                            logger.LogError(diagnostic.ToString());
                        }
                        return false;
                    }
                    catch (AggregateException aggregateException)
                    {
                        foreach (Exception ex in aggregateException.InnerExceptions)
                        {
                            logger.LogError(ex.ToString());
                        }
                        return false;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.ToString());
                        return false;
                    }
                    finally
                    {
                        // Flush and truncate the output file (in case it already had content)
                        outputWriter.Flush();
                        outputStream.SetLength(outputStream.Position);
                    }
                }
            }
            return true;
        }
    }
}
