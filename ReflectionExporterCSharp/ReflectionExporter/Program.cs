using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommandLine;


namespace ReflectionExporter
{
    class Program
    {
        public static string GetCode()
        {
            return @"

struct [[Meta::Reflectable]] Point
{
    float x;
    float y;

    __declspec(dllexport) [[Meta::Property(""X"")]] float [[Meta::EditorVisible]] [[Meta::Serialize]] GetX();
    __declspec(dllexport) [[Meta::Property(""X"")]] float [[Meta::EditorVisible]] [[Meta::Serialize]] SetX();
};
            ";
        }

        public class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option('i', "include", Required = false, HelpText = "Pass include directories")]
            public IEnumerable<string> IncludeDirectories { get; set; }
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

        static void Main(string[] args)
        {

            var compilation = CppAst.CppParser.Parse(GetCode());

            var builder = new StringBuilder();

            //if (compilation.HasErrors)
            {
                Console.WriteLine(compilation.Diagnostics.ToString());
            }

            foreach (var cppClass in compilation.Classes)
            {
                builder.AppendFormat("{0}\n", cppClass.Name);

                PrintAttributes(cppClass.Attributes, builder, 1);

                foreach (var function in cppClass.Functions)
                {
                    builder.AppendFormat("\tFunction: {0}\n", function.Name);
                    PrintAttributes(function.Attributes, builder, 2);
                }

                foreach (var field in cppClass.Fields)
                {
                    builder.AppendFormat("\tField: {0}\n", field.Name);
                    PrintAttributes(field.Attributes, builder, 2);
                }
            }

            Console.WriteLine(builder.ToString());


            //Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(arguments =>
            //{
            //    var options = new CppAst.CppParserOptions();
            //    options. = arguments.
            //    CppAst.CppParser.ParseFile("");
            //});
        }
    }
}
