using System.CommandLine;
using Linbik.CLI.Commands;

var rootCommand = new RootCommand("Linbik CLI — Setup and manage Linbik authentication services");

rootCommand.AddCommand(InitCommand.Create());
rootCommand.AddCommand(ExportConfigCommand.Create());
rootCommand.AddCommand(StatusCommand.Create());

return await rootCommand.InvokeAsync(args);
