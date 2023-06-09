using HMCon;
using HMCon.Export;
using HMCon.Formats;
using HMCon.Import;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static HMCon.ConsoleOutput;
using static HMCon.HMConManager;

namespace HMConConsole
{
	public class Program
	{

		internal static List<string> commandQueue = new List<string>();

		public static void Main(string[] launchArgs)
		{

			bool loadModules = true;
			foreach (var a in launchArgs) if (a == "nomodules") loadModules = false;
			Initialize(loadModules ? AppContext.BaseDirectory : null);
			ErrorOccurred += OnConsoleError;

			WriteLine("---------------------------------");
			WriteLine("HEIGHTMAP CONVERTER V1.1");
			WriteLine("---------------------------------");
			while (true)
			{
				BeginNewJob();

				if (currentJob == null)
				{
					return;
				}

				currentJob.NextFile();
				if (currentJob.CurrentData == null) continue;

				if (currentJob.CurrentData.isValid)
				{
					if (!GetExportSettings(currentJob.batchMode))
					{
						currentJob = null;
						continue;
					}

					currentJob.outputPath = GetExportPath(currentJob.batchMode);

					currentJob.ExportAll();

					WriteLine("---------------------------------");
					currentJob = null;
				}
			}
		}

		static void OnConsoleError(string msg)
		{
			commandQueue.Clear();
		}

		static void BeginNewJob()
		{
			int result = GetInputFiles(out var fileList);
			if (result < 0) return; //Do nothing, terminate the application
			currentJob = new Job()
			{
				batchMode = result > 0
			};
			currentJob.AddInputFiles(fileList.ToArray());

			//Add console feedback
			currentJob.FileImported += (int i, string s) =>
			{

			};
			currentJob.FileImportFailed += (int i, string s, Exception e) =>
			{
				WriteError("IMPORT FAILED: " + s);
				WriteError(e.ToString());
			};
			currentJob.FileExported += (int i, string s) =>
			{
				if (!currentJob.batchMode)
				{
					WriteSuccess("EXPORT SUCCESSFUL");
				}
				else
				{
					WriteSuccess($"EXPORT {i + 1}/{currentJob.InputFileList.Count} SUCCESSFUL");
				}
			};
			currentJob.FileExportFailed += (int i, string s, Exception e) =>
			{
				if (!currentJob.batchMode)
				{
					WriteError("EXPORT FAILED: " + s);
				}
				else
				{
					WriteError($"EXPORT {i}/{currentJob.InputFileList.Count} FAILED:");
				}
				WriteError(e.ToString());
			};
			currentJob.ExportCompleted += () =>
			{
				if (currentJob.batchMode)
				{
					WriteSuccess("DONE!");
				}
			};
		}

		static void CreateCommandQueue(string commandInput)
		{
			if(commandInput.ToLower().StartsWith("exec ")) commandInput = commandInput.Substring(5);
			commandInput = commandInput.Replace("\"", "");
			try
			{
				var lines = File.ReadAllLines(commandInput);
				if(commandQueue.Count + lines.Length > 100)
				{
					WriteError("Command queue overflow (> 100). Commands not added.");
				}
				else
				{
					commandQueue.InsertRange(0, lines);
				}
			}
			catch(Exception e)
			{
				WriteError($"Failed to add commands from file to queue ({commandInput}): {e.Message}");
			}
		}

		static int GetInputFiles(out List<string> files)
		{
			WriteLine("Enter path to the input file:");
			WriteLine("or type 'batch' and a path to perform batch operations");
			string input = GetInput();
			files = new List<string>();
			//input = input.Replace("\"", "");
			if(input.ToLower().StartsWith("exec"))
			{
				//Add commands to queue
				CreateCommandQueue(input);
				input = GetInput();
			}

			if (input.ToLower().StartsWith("quit"))
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

		static bool GetExportSettings(bool batch)
		{
			if (!GetExportOptions(batch)) return false;
			while (!ExportManager.ValidateExportSettings(currentJob.exportSettings, currentJob.CurrentData))
			{
				WriteError("Cannot export with the current settings / format!");
				if (!GetExportOptions(batch)) return false;
			}
			return true;
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

		static bool GetExportOptions(bool batch)
		{
			WriteLine("--------------------");
			if (batch) WriteLine("Note: The following export options will be applied to all files in the batch");
			WriteLine("* = Required setting");
			WriteLine("Export options:");
			WriteListEntry("format N..", "Export to the specified format(s)", 0, true);
			foreach (var f in FileFormatManager.GetSupportedFormats())
			{
				WriteListEntry(f.CommandKey, f.Description, 1, false);
			}
			foreach (var c in CommandHandler.ConsoleCommands)
			{
				WriteListEntry(c.command, c.description, 0, false);
			}
			WriteListEntry("modify X..", "Modification commands", 0, false);
			foreach (var m in CommandHandler.ModificationCommands)
			{
				WriteListEntry(m.command, m.description, 1, false);
			}
			if (batch)
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
				input = GetInput();
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

				var r = HandleCommand(cmd, args, batch);
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
			else if (cmd == "format")
			{
				if (args.Length > 0)
				{
					currentJob.exportSettings.SetOutputFormats(args, false);
					string str = "";
					foreach (FileFormat ff in currentJob.exportSettings.outputFormats)
					{
						str += " " + ff.Identifier;
					}
					if (str == "") str = " <NONE>";
					WriteLine("Exporting to the following format(s):" + str);
				}
				else
				{
					WriteWarning("A list of formats is required!");
				}
				return null;
			}
			else if (cmd == "modify")
			{
				if (args.Length > 0)
				{
					List<string> argList = new List<string>(args);
					cmd = argList[0];
					argList.RemoveAt(0);
					args = argList.ToArray();
					foreach (var c in CommandHandler.ModificationCommands)
					{
						if (c.command == cmd)
						{
							try
							{
								var mod = c.ExecuteCommand(currentJob, args);
								if (mod != null)
								{
									currentJob.modificationChain.AddModifier(mod);
								}
							}
							catch (Exception e)
							{
								WriteWarning(e.Message);
								WriteWarning($"Usage: {c.command} {c.argsHint}");
							}
							return null;
						}
					}
					WriteWarning("Unknown modifier: " + cmd);
				}
				return null;
			}
			else if(cmd == "define")
			{
				if(args.Length >= 2)
				{
					currentJob.variables.Add(args[0], args[1]);
				}
				else
				{
					WriteError("Not enough arguments.");
				}
				return null;
			}
			else if(cmd == "definep")
			{
				if(args.Length >= 1)
				{
					string prompt;
					if(args.Length >= 2)
					{
						prompt = args[1] + ":";
					}
					else
					{
						prompt = $"Enter value for variable '{args[0]}':";
					}
					WriteLine(prompt);
					currentJob.variables.Add(args[0], GetInput(false));
				}
				else
				{
					WriteError("Not enough arguments.");
				}
				return null;
			}
			foreach (var c in CommandHandler.ConsoleCommands)
			{
				if (c.command == cmd)
				{
					c.ExecuteCommand(currentJob, args);
					return null;
				}
			}
			if (batch)
			{
				if (cmd == "join")
				{
					WriteWarning("to do"); //TODO
				}
				else if (cmd == "equalizeheightmaps")
				{
					float low = float.MaxValue;
					float high = float.MinValue;
					float avg = 0;
					int i = 0;
					foreach (string path in currentJob.InputFileList)
					{
						if (Path.GetExtension(path).ToLower() == ".asc")
						{
							i++;
							ASCImporter.GetDataInfo(path, out float ascLow, out float ascHigh, out float ascAvg);
							WriteLine(i + "/" + currentJob.InputFileList.Count);
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

		public static string GetInput(bool allowQueued = true)
		{
			Console.CursorVisible = true;
			string s;

			if (commandQueue.Count > 0 && allowQueued)
			{
				s = commandQueue[0];
				commandQueue.RemoveAt(0);
				WriteAutoTask("> " + s);
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Gray;
				Console.Write("> ");
				s = Console.ReadLine();
				Console.ResetColor();
			}

			//Parse variables
			if(currentJob != null)
			{
				s = currentJob.ParseVariables(s);
			}

			return s;
		}

		public static string GetInputPath()
		{
			return GetInput().Replace("\"", "");
		}
	}
}
