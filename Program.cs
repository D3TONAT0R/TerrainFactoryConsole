using TerrainFactory;
using TerrainFactory.Commands;
using TerrainFactory.Export;
using TerrainFactory.Formats;
using TerrainFactory.Import;
using TerrainFactory.Modification;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static TerrainFactory.ConsoleOutput;
using static TerrainFactory.TerrainFactoryManager;

namespace TerrainFactoryConsole
{
	public class Program
	{

		internal static Project project;

		public static void Main(string[] launchArgs)
		{
			try
			{
				if(launchArgs.Contains("nomodules")) ModuleDirectories.Clear();
				Initialize();

				WriteBox("TerrainFactory v1.2");

				while(true)
				{
					CreateNewProject();

					if(project == null)
					{
						return;
					}

					project.ImportNext();
					if(project.CurrentData == null) continue;

					bool result = GetExportOptions();
					if(!result)
					{
						//Export was aborted via command
						continue;
					}

					project.OutputPath = GetExportPath(project.ForceBatchNamingPattern);

					project.ProcessAll();

					WriteLine("---------------------------------");
					project = null;
				}
			}
			catch(Exception e)
			{
				WriteError("An unhandled exception was thrown:");
				WriteError(e.ToString());
				WriteError("---------------------------------");
				WriteError("Press any key to terminate the application.");
				Console.ReadKey();
			}
		}

		/*
		static CommandResult ExecuteCommandInput(bool isAfterImport, string prompt = null)
		{
			var input = CommandHandler.GetInput(project, prompt, true);
			CommandParser.ParseCommandInput(input, out string cmd, out string[] args);
			if(isAfterImport)
			{
				if(cmd == "abort")
				{
					return CommandResult.RequestsQuit;
				}
			}
			else
			{
				if(cmd == "exit")
				{
					return CommandResult.RequestsQuit;
				}
			}

			foreach(var c in CommandHandler.ListValidCommands(isAfterImport ? CommandAttribute.ContextFlags.AfterImport : CommandAttribute.ContextFlags.BeforeImport))
			{
				if(cmd == c.attribute.commandName)
				{
					bool result = (bool)c.method.Invoke(null, new object[] { project, args });
					return result ? CommandResult.Success : CommandResult.Failed;
				}
			}

			//No command was executed
			return CommandResult.None;
		}
		*/

		static void CreateNewProject()
		{
			int result = GetInputFiles(out var fileList);
			if(result < 0) return; //Do nothing, terminate the application
			project = new Project()
			{
				ForceBatchNamingPattern = result > 0
			};
			project.AddInputFiles(fileList.ToArray());

			//Add console feedback
			project.FileImported += (int i, string s) =>
			{

			};
			project.FileImportFailed += (int i, string s, Exception e) =>
			{
				WriteError("IMPORT FAILED: " + s);
				WriteError(e.ToString());
			};
			project.FileExported += (int i, string s) =>
			{
				if(!project.ForceBatchNamingPattern)
				{
					WriteSuccess("EXPORT SUCCESSFUL");
				}
				else
				{
					WriteSuccess($"EXPORT {i + 1}/{project.InputFileList.Count} SUCCESSFUL");
				}
			};
			project.FileExportFailed += (int i, string s, Exception e) =>
			{
				if(!project.ForceBatchNamingPattern)
				{
					WriteError("EXPORT FAILED: " + s);
				}
				else
				{
					WriteError($"EXPORT {i}/{project.InputFileList.Count} FAILED:");
				}
				WriteError(e.ToString());
			};
			project.ExportCompleted += () =>
			{
				if(project.ForceBatchNamingPattern)
				{
					WriteSuccess("DONE!");
				}
			};
		}

		static int GetInputFiles(out List<string> files)
		{
			WriteLine("Enter path to input file:");
			WriteLine("or type 'batch' and a path to perform batch operations");

			/*
			CommandResult result = CommandResult.None;
			while(result == CommandResult.None)
			{
				string input = CommandHandler.GetInput(null);
				result = CommandHandler.ExecuteCommand(null, input, CommandAttribute.ContextFlags.BeforeImport);
				

			}
			*/

			string input = CommandHandler.GetInput(project);
			files = new List<string>();
			//input = input.Replace("\"", "");
			/*
			if(input.ToLower().StartsWith("exec"))
			{
				//Add commands to queue
				CommandHandler.AddCommandsToQueue(in)
				CommandHandler.CreateCommandQueue(input);
				input = CommandHandler.GetInput(project);
			}
			*/

			if (input.ToLower().StartsWith("exit"))
			{
				return -1;
			}
			else if (input.ToLower().StartsWith("batch"))
			{
				if (input.Length > 6)
				{
					input = input.Substring(6);
					if (Directory.Exists(input))
					{
						WriteLine("Starting batch in directory " + input + " ...");
						foreach (string f in Directory.GetFiles(input, "*", SearchOption.AllDirectories))
						{
							if (ImportManager.CanImport(f))
							{
								files.Add(f);
							}
							else
							{
								WriteWarning($"Skipping file 'f', unknown or unsupported file type.");
							}
						}
						WriteLine(files.Count + " files have been added to the batch queue");
					}
					return 1;
				}
				else
				{
					files.Add("");
					return 0;
				}
			}
			else
			{
				files.Add(input);
				WriteLine("Reading file " + input + " ...");
				return 0;
			}
		}

		static string GetExportPath(bool mustBeDirectory)
		{
			if (mustBeDirectory)
			{
				WriteLine("Enter destination path:");
			}
			else
			{
				WriteLine("Enter path and filename to write the file(s):");
			}
			var path = GetInputPath();
			if (mustBeDirectory)
			{
				while (!Directory.Exists(path))
				{
					WriteWarning("Directory not found!");
					path = GetInputPath();
				}
			}
			else
			{
				while (!Directory.Exists(Path.GetDirectoryName(path)))
				{
					WriteWarning("Directory not found!");
					path = GetInputPath();
				}
			}
			return path;
		}

		static void WriteListEntry(string cmd, string desc, int indentLevel, bool required)
		{
			string s = required ? "*" : "";
			s = s.PadRight((indentLevel + 1) * 4);
			s += cmd;
			s = s.PadRight(24);
			s += desc;
			WriteLine(s);
		}

		static bool GetExportOptions()
		{
			WriteLine("--------------------");
			if (project.HasMultipleInputs) WriteLine("Note: The following export options will be applied to all files in this batch.");
			WriteLine("* = Required setting");
			WriteLine("Export options:");
			WriteListEntry("format N..", "Export to the specified format(s)", 0, true);
			foreach (var f in FileFormatManager.GetSupportedFormats(FileFormat.FileSupportFlags.Export))
			{
				WriteListEntry(f.CommandKey, f.Description, 1, false);
			}
			foreach (var c in CommandHandler.Commands)
			{
				WriteListEntry(c.attribute.commandName, c.attribute.desc, 0, false);
			}
			WriteListEntry("mod X..", "Modification commands", 0, false);
			foreach (var m in CommandHandler.ModifierCommands)
			{
				WriteListEntry(m.attribute.commandName, m.attribute.desc, 1, false);
			}
			if (project.HasMultipleInputs)
			{
				WriteLineSpecial("Batch export options:");
				WriteLineSpecial("    join                Joins all files into one large file");
				WriteLineSpecial("    equalizeheightmaps  Equalizes all heightmaps with the same low and high values");
			}
			WriteLine("");
			WriteLine("Type 'export' when ready to export");
			WriteLine("Type 'abort' to abort export");
			WriteLine("--------------------");
			string input;
			while (true)
			{
				input = CommandHandler.GetInput(project);
				while (input.Contains("  ")) input = input.Replace("  ", " "); //Remove all double spaces

				string cmd = input.Split(' ')[0].ToLower();
				string argsString = "";
				if(input.Length > cmd.Length + 1)
				{
					argsString = input.Substring(cmd.Length + 1);
				}

				var args = Regex.Matches(argsString, @"[\""].+?[\""]|[^ ]+")
				.Cast<Match>()
				.Select(x => x.Value.Trim('"'))
				.ToArray();

				var r = HandleCommand(cmd, args, project.HasMultipleInputs);
				if (r.HasValue)
				{
					return r.Value;
				}
			}
		}

		static bool? HandleCommand(string cmd, string[] args, bool batch)
		{
			if (cmd == "export")
			{
				return true;
			}
			else if (cmd == "abort")
			{
				WriteWarning("Export aborted");
				return false;
			}
			else if (cmd == "mod")
			{
				if (args.Length > 0)
				{
					List<string> argList = new List<string>(args);
					cmd = argList[0];
					argList.RemoveAt(0);
					args = argList.ToArray();
					foreach (var modCommand in CommandHandler.ModifierCommands)
					{
						if (modCommand.attribute.commandName == cmd)
						{
							try
							{
								var mod = (Modifier)modCommand.method.Invoke(null, new object[] { project, args });
								if (mod != null)
								{
									project.modificationChain.AddModifier(mod);
								}
								else
								{
									throw new NullReferenceException();
								}
							}
							catch (Exception e)
							{
								WriteWarning(e.Message);
								WriteWarning($"Usage: {modCommand.attribute.commandName} {modCommand.attribute.args}");
							}
							return null;
						}
					}
					WriteWarning("Unknown modifier: " + cmd);
				}
				return null;
			}
			foreach (var c in CommandHandler.Commands)
			{
				if (c.attribute.commandName == cmd)
				{
					c.method.Invoke(null, new object[] { project, args });
					return null;
				}
			}
			if (batch)
			{
				if (cmd == "equalizeheightmaps")
				{
					float low = float.MaxValue;
					float high = float.MinValue;
					float avg = 0;
					int i = 0;
					foreach (string path in project.InputFileList)
					{
						if (Path.GetExtension(path).ToLower() == ".asc")
						{
							i++;
							ASCImporter.GetDataInfo(path, out float ascLow, out float ascHigh, out float ascAvg);
							WriteLine(i + "/" + project.InputFileList.Count);
							low = Math.Min(low, ascLow);
							high = Math.Max(high, ascHigh);
							avg += ascAvg;
						}
						else
						{
							WriteError(path + " is not a ASC file!");
						}
					}
					avg /= i;
					WriteLine("Success:");
					WriteLine("    lowest:   " + low);
					WriteLine("    highest:  " + high);
					WriteLine("    average:  " + avg);
					return null;
				}
			}
			else
			{
				WriteWarning("Unknown option: " + cmd);
			}
			return null;
		}

		public static string GetInputPath()
		{
			return CommandHandler.GetInput(project, null).Replace("\"", "");
		}
	}
}
