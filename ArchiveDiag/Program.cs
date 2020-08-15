﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ArchiveDiag;
using CommandLine;
using ConLib;
using ConLib.Console;
using ConLib.HTML;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using static ConLib.PrettyConsole;

namespace ICSharpCode.SharpZipLib.ArchiveDiag
{
	public class Program
	{
		public class Options
		{
			[Value(0, HelpText = "Input filename")]
			public string Filename { get; set; }

			[Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages")]
			public bool Verbose { get; set; }

			[Option('q', "quiet")]
			public bool Quiet { get; set; }

			[Option('h', "no-html-report")]
			public bool SkipHtmlReport { get; set; }

			[Option('e', "eval", HelpText = "Run the input file as a C# script and create a report from the resulting stream")]
			public bool Evaluate { get; set; }
		}


		static int Main(string[] args)
        {

	        Parser.Default.ParseArguments<Options>(args)
		        .WithParsed<Options>(o =>
		        {
			        Stream inputStream;
			        var outputFile = $"{o.Filename}.html";
			        var inputFile = Path.GetFileName(o.Filename);

					if (o.Evaluate)
					{
						inputFile = $"script:{inputFile}";
						try
						{
							using var fs = File.OpenRead(o.Filename);
							using var sr = new StreamReader(fs);

							var opts = ScriptOptions.Default
								.WithFilePath(o.Filename)
								.WithImports(
									"System",
									"System.IO",
									"System.Text",
									"System.Collections.Generic",
									"ICSharpCode.SharpZipLib", 
									"ICSharpCode.SharpZipLib.Core",
									"ICSharpCode.SharpZipLib.Zip")
								.WithReferences(typeof(ZipOutputStream).Assembly);

							var task =
								CSharpScript.EvaluateAsync<byte[]>(sr.ReadToEnd(), opts);
							if (task.Wait(TimeSpan.FromSeconds(30)))
							{
								inputStream = new MemoryStream(task.Result);
							}
							else throw new TimeoutException("Script evaluation timed out");
						}
						catch (Exception x)
						{
							Console.WriteLine($"Failed to evaluate input script: {x}");
							return;
						}

			        }
			        else
			        {
						inputStream = File.OpenRead(o.Filename);

					}

			        using var outputStream = File.Open(outputFile, FileMode.Create);
					using var htmlWriter = new HTMLWriter(outputStream);

					var runner = new ArchiveDiagRunner(inputStream, inputFile);

					runner.Run(new ConsoleWriter(), new HTMLWriter(outputStream));

					htmlWriter.Flush();
					htmlWriter.Dispose();

				});

	        return 0;
        }
		
	}



	static class ZipExtraDataExtensions
    {
	    public static IEnumerable<(ExtraDataType, Range)> EnumerateTags(this ZipExtraData zed)
	    {
		    var index = 0;

		    var data = zed.GetEntryData();

			while (index < data.Length - 3)
		    {
			    var tag = data[index++] + (data[index++] << 8);
			    var length = data[index++] + (data[index++] << 8);
			    yield return ((ExtraDataType)tag, new Range(index, index+length));
			    index += length;
		    }

	    }
    }


	internal static class StringExtensions
	{
		internal static string Ellipsis(this string source, int maxLength)
			=> source.Length > maxLength - 3
				? source.Substring(0, maxLength - 3) + "..."
				: source;
	}

}
