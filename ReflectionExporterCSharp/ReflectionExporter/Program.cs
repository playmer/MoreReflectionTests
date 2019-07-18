using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CommandLine;

using System.Reflection;


namespace ReflectionExporter
{
    class CppCodeGenerator
    {
        readonly int mIndentSize = 2;
        private StringBuilder mIncludesBuilder;
        private StringBuilder mBuilder;
        private List<string> mClasses;
        string mTargetName;
        int mNestedScopes;

        private static string GetFullTypeName(CppAst.CppClass aClass)
        {
            var classes = new List<string>();

            var builder = new StringBuilder();

            var parent = aClass.Parent;

            while (null != parent && parent is CppAst.CppClass)
            {
                var parentClass = parent as CppAst.CppClass;
                classes.Add(parentClass.Name);
                parent = parentClass.Parent;
            }

            classes.Reverse();

            foreach(var cppClass in classes)
            {
                builder.Append(cppClass);
                builder.Append("::");
            }

            builder.Append(aClass.Name);

            return builder.ToString();
        }

        public CppCodeGenerator(string aTargetName)
        {
            mBuilder = new StringBuilder();
            mIncludesBuilder = new StringBuilder();
            mClasses = new List<string>();
            mTargetName = aTargetName;
            mNestedScopes = 0;

            mIncludesBuilder.Append($"#include \"SimpleReflection/Meta.hpp\"\n");
        }

        // Do not call this a second time, only call this when you're done with the object.
        public override string ToString()
        {
            // Generate a ReflectionInitialize function.

            OpenNamespace($"{mTargetName}_ReflectionInitialize");

            AddLine($"void InitializeReflection()");
            OpenScope();

            foreach (var reflectedClass in mClasses)
            {
                AddLine($"srefl::InitializeType<{reflectedClass}>();");
            }

            CloseScope();
            CloseNamespace();

            // Actually make a string to return.
            mIncludesBuilder.Append('\n');
            mIncludesBuilder.Append(mBuilder.ToString());
            return mIncludesBuilder.ToString();
        }

        public void OpenNamespace(string aNamespace)
        {
            AddLine($"namespace {aNamespace}");
            OpenScope();
        }

        public void AddExternalType(CppAst.CppClass aClass, StringBuilder aLog)
        {
            var fullTypeName = GetFullTypeName(aClass);
            var shortTypeName = aClass.Name;

            mClasses.Add(fullTypeName);

            mIncludesBuilder.Append($"#include \"{aClass.Span.Start.File}\"\n");

            AddLine($"sreflDefineExternalType({fullTypeName})");
            OpenScope();

            AddLine($"RegisterType<{shortTypeName}>();");
            AddLine($"TypeBuilder<{shortTypeName}> builder;");

            foreach (var field in aClass.Fields)
            {
                aLog.Append($"{field.Name}: ");

                // Check to see if we shouldn't expose this field.
                if (null != field.Attributes)
                {
                    foreach (var attribute in field.Attributes)
                    {
                        aLog.Append($"{attribute.Scope}::{attribute.Name}, ");
                        if (attribute.Scope == "Meta" && attribute.Name == "DoNotExpose")
                        {
                            continue;
                        }
                    }
                }

                aLog.Append("\n");

                AddLine($"builder.Field<&{shortTypeName}::{field.Name}>(\"{field.Name}\", PropertyBinding::GetSet);");
            }

            CloseScope();
        }

        public void CloseNamespace()
        {
            CloseScope();
        }

        public void OpenScope()
        {
            AddLine("{");
            ++mNestedScopes;
        }

        public void CloseScope()
        {
            --mNestedScopes;
            AddLine("}");
        }

        public void AddLine(string aLine, bool aComment = false)
        {
            var toIndentBy = mIndentSize * mNestedScopes;

            foreach(var line in aLine.Split('\n'))
            {
                mBuilder.Append(' ', toIndentBy);

                if (aComment)
                {
                    mBuilder.Append("// ");
                }

                mBuilder.AppendLine(line);
            }
        }
    };












    class Program
    {
        public class Options
        {
            [Option('v', "verbose", Default = false, Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option('s', "sources", Required = true, HelpText = "Pass files (.cpp files) to parse")]
            public IEnumerable<string> Sources { get; set; }

            [Option('i', "include", Required = true, HelpText = "Pass include directories")]
            public IEnumerable<string> IncludeDirectories { get; set; }

            [Option('o', "outputFile", Required = true, HelpText = "Pass where to export reflection.")]
            public string OutputFile { get; set; }

            [Option('t', "targetName", Required = true, HelpText = "Pass where to export reflection.")]
            public string TargetName { get; set; }
        }

        static void PrintAttributes(List<CppAst.CppAttribute> aAttributes, StringBuilder aBuilder, int aTabLevel)
        {
            string tabs1 = new String('\t', aTabLevel);
            string tabs2 = new String('\t', aTabLevel + 1);

            aAttributes?.ForEach(attribute =>
            {
                aBuilder.AppendFormat("{0}{1}\n", tabs1, attribute);

                if (null != attribute.Arguments && 0 != attribute.Arguments.Length)
                {
                    aBuilder.AppendFormat("{0}{1}\n", tabs2, attribute.Arguments);
                }
            });
        }

        static public void ClassParser(CppAst.CppClass aClass, CppCodeGenerator aGenerator, StringBuilder aLog, string aTargetName)
        {
            bool found = false;

            if (null != aClass.Attributes)
            {
                foreach (var attribute in aClass.Attributes)
                {
                    if (attribute.Scope == "Meta" && attribute.Name == "Reflectable")
                    {
                        var argument = attribute.Arguments.Trim('"');

                        if (argument == aTargetName)
                        {
                            found = true;
                            break;
                        }

                        aLog.Append($"{aClass.Name}: {argument}");
                        aLog.Append("\n");
                    }
                }
            }

            if (false == found)
            {
                return;
            }

            aGenerator.AddExternalType(aClass, aLog);


            foreach (var cppClass in aClass.Classes)
            {
                ClassParser(cppClass, aGenerator, aLog, aTargetName);
            }
        }

        static public void NamespaceParser(CppAst.CppNamespace aNamespace, CppCodeGenerator aGenerator, StringBuilder aLog, string aTargetName)
        {
            //aGenerator.OpenNamespace(aNamespace.Name);

            foreach (var cppClass in aNamespace.Classes)
            {
                ClassParser(cppClass, aGenerator, aLog, aTargetName);
            }

            foreach (var cppNamespace in aNamespace.Namespaces)
            {
                NamespaceParser(cppNamespace, aGenerator, aLog, aTargetName);
            }

            //aGenerator.CloseNamespace();
        }

        static void HandleOptions(Options aOptions)
        {
            var log = new StringBuilder();

            var additionalArguments = new List<string> { "-std=c++17", "-Wno-everything" };

            // Just printing out the passed command line for testing.
            var argumentString = CommandLine.Parser.Default.FormatCommandLine(aOptions);
            Console.WriteLine(argumentString);

            var options = new CppAst.CppParserOptions();
            options.ParseComments = false;
            options.ConfigureForWindowsMsvc(CppAst.CppTargetCpu.X86_64, CppAst.CppVisualStudioVersion.VS2019);

            // We have to use reflection to set this because the Sun that burns in the sky has forsaken us.
            var optionsType = typeof(CppAst.CppParserOptions);
            var includeFoldersProperty = optionsType.GetProperty("IncludeFolders");
            includeFoldersProperty.SetValue(options, aOptions.IncludeDirectories.ToList());

            var additionalArgumentsProperty = optionsType.GetProperty("AdditionalArguments");
            additionalArgumentsProperty.SetValue(options, additionalArguments);

            var compilation = CppAst.CppParser.ParseFiles(aOptions.Sources.ToList(), options);

            var textFile = RunCodeGen(aOptions, compilation, log);
            var headerFile = $"namespace {aOptions.TargetName}_ReflectionInitialize {{ void InitializeReflection(); }}";

            System.IO.File.WriteAllText(aOptions.OutputFile + ".cpp", textFile);
            System.IO.File.WriteAllText(aOptions.OutputFile + ".h", headerFile);
            System.IO.File.WriteAllText("ReflectonLogFile.txt", log.ToString());
        }

        static string RunCodeGen(Options aOptions, CppAst.CppCompilation aCompilation, StringBuilder aLog)
        {
            var codeGen = new CppCodeGenerator(aOptions.TargetName);

            if (aCompilation.HasErrors)
            {
                Console.WriteLine(aCompilation.Diagnostics.ToString());
            }

            foreach (var cppNamespace in aCompilation.Namespaces)
            {
                NamespaceParser(cppNamespace, codeGen, aLog, aOptions.TargetName);
            }

            foreach (var cppClass in aCompilation.Classes)
            {
                ClassParser(cppClass, codeGen, aLog, aOptions.TargetName);
            }

            return codeGen.ToString();
        }


        static void Main(string[] aArguments)
        {
            Parser.Default.ParseArguments<Options>(aArguments).WithParsed<Options>(arguments =>
            {
                HandleOptions(arguments);
            });
        }
    }
}
