using System.CommandLine;
using Linbik.CLI.Commands;

var rootCommand = new RootCommand("Linbik CLI — Setup and manage Linbik authentication services");

rootCommand.Subcommands.Add(InitCommand.Create());
rootCommand.Subcommands.Add(ExportConfigCommand.Create());
rootCommand.Subcommands.Add(StatusCommand.Create());
rootCommand.Subcommands.Add(DoctorCommand.Create());


var result = await rootCommand.Parse(args).InvokeAsync();


Console.WriteLine("Press any key to exit...");
Console.ReadKey();

return result;