using System.CommandLine;
using Linbik.CLI.Commands;

var rootCommand = new RootCommand("Linbik CLI — Setup and manage Linbik authentication services");

rootCommand.AddCommand(InitCommand.Create());
rootCommand.AddCommand(ExportConfigCommand.Create());
rootCommand.AddCommand(StatusCommand.Create());


var result = await rootCommand.InvokeAsync(args);


Console.WriteLine("Press any key to exit...");
Console.ReadKey();

return result;