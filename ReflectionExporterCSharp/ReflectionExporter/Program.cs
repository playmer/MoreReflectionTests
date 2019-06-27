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
        private StringBuilder mBuilder;
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

        public CppCodeGenerator()
        {
            mBuilder = new StringBuilder();
            mNestedScopes = 0;
        }

        public override string ToString()
        {
            return mBuilder.ToString();
        }

        public void OpenNamespace(string aNamespace)
        {
            AddLine($"namespace {aNamespace}");
            OpenScope();
        }

        public void AddExternalType(CppAst.CppClass aClass)
        {
            var fullTypeName = GetFullTypeName(aClass);
            var shortTypeName = aClass.Name;

            AddLine($"YTEDefineExternalType({fullTypeName})");
            OpenScope();

            AddLine($"RegisterType<{shortTypeName}>();");
            AddLine($"TypeBuilder<{shortTypeName}> builder;");

            foreach (var field in aClass.Fields)
            {
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
        public static string GetCode()
        {
            return @"

namespace 
{
    namespace YTE
    {
        struct [[Meta::Reflectable]] YTEStruct{};        
    }

    struct [[Meta::Reflectable]] namelessNamespaceStruct{};
}

struct [[Meta::Reflectable]] Vec3{ float x, y, z; };
struct [[Meta::Reflectable]] Mat3{ float mData[3][3] ; };

struct [[Meta::Reflectable]] Body {
   struct [[Meta::Reflectable]] myStruct{};
    
  float restitution;

  float massInverse;
  Mat3  inertiaTensor;

  Vec3 velocity;
  Vec3 angularVelocity;

  Vec3 impulse;
  Vec3 angularImpulse;

  void  SetMass(float m);
  float GetMass() const;
};
            ";
        }

        public class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option('s', "sources", Required = false, HelpText = "Pass files (.cpp files) to parse")]
            public List<string> Sources { get; set; }

            [Option('i', "include", Required = false, HelpText = "Pass include directories")]
            public List<string> IncludeDirectories { get; set; }
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

        static public void ClassParser(CppAst.CppClass aClass, CppCodeGenerator aGenerator)
        {
            bool found = false;

            if (null != aClass.Attributes)
            {
                foreach (var attribute in aClass.Attributes)
                {
                    if (attribute.Scope == "Meta" && attribute.Name == "Reflectable")
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (false == found)
            {
                return;
            }

            aGenerator.AddExternalType(aClass);


            foreach (var cppClass in aClass.Classes)
            {
                ClassParser(cppClass, aGenerator);
            }
        }

        static public void NamespaceParser(CppAst.CppNamespace aNamespace, CppCodeGenerator aGenerator)
        {
            aGenerator.OpenNamespace(aNamespace.Name);

            foreach (var cppClass in aNamespace.Classes)
            {
                ClassParser(cppClass, aGenerator);
            }

            foreach (var cppNamespace in aNamespace.Namespaces)
            {
                NamespaceParser(cppNamespace, aGenerator);
            }

            aGenerator.CloseNamespace();
        }


        static void Main(string[] aArguments)
        {
            if (0 == aArguments.Count())
            {
                var compilation = CppAst.CppParser.Parse(GetCode());
                var codeGen = new CppCodeGenerator();


                //if (compilation.HasErrors)
                {
                    Console.WriteLine(compilation.Diagnostics.ToString());
                }

                foreach (var cppNamespace in compilation.Namespaces)
                {
                    NamespaceParser(cppNamespace, codeGen);
                }

                foreach (var cppClass in compilation.Classes)
                {
                    ClassParser(cppClass, codeGen);
                }

                var str = codeGen.ToString();

                return;
            }

            Parser.Default.ParseArguments<Options>(aArguments).WithParsed<Options>(arguments =>
            {
                var options = new CppAst.CppParserOptions();
                options.ConfigureForWindowsMsvc(CppAst.CppTargetCpu.X86_64, CppAst.CppVisualStudioVersion.VS2019);

                // We have to use reflection to set this because the Sun the burns in the sky has forsaken us.
                var optionsType = typeof(CppAst.CppParserOptions);
                var includeFoldersProperty = optionsType.GetProperty("IncludeFolders");
                includeFoldersProperty.SetValue(options, arguments.IncludeDirectories);

                var compilation = CppAst.CppParser.ParseFiles(arguments.Sources, options);
                var codeGen = new CppCodeGenerator();

                //if (compilation.HasErrors)
                {
                    Console.WriteLine(compilation.Diagnostics.ToString());
                }

                foreach (var cppNamespace in compilation.Namespaces)
                {
                    NamespaceParser(cppNamespace, codeGen);
                }

                foreach (var cppClass in compilation.Classes)
                {
                    ClassParser(cppClass, codeGen);
                }

                var str = codeGen.ToString();
            });
        }
    }
}
