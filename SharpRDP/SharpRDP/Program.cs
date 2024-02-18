using System;
using System.IO.Compression;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace SharpRDP
{
    public class Program
    {
        const string LayoutArgumentKey = "layout";
        const string AmericanLayoutId = "US";
        const string JsonFolderPath = "Json";

        static void HowTo()
        {
            Console.WriteLine("SharpRDP");
            Console.WriteLine("");
            Console.WriteLine("  Regular RDP Connection");
            Console.WriteLine("    SharpRDP.exe computername=domain.target command=\"C:\\Temp\\file.exe\" username=domain\\user password=password");
            Console.WriteLine("  Exec as child process of cmd or ps ");
            Console.WriteLine("    SharpRDP.exe computername=domain.target command=\"C:\\Temp\\file.exe\" username=domain\\user password=password exec=cmd");
            Console.WriteLine("  Use restricted admin mode");
            Console.WriteLine("    SharpRDP.exe computername=domain.target command=\"C:\\Temp\\file.exe\"");
            Console.WriteLine("  Connect first host drives");
            Console.WriteLine("    SharpRDP.exe computername=domain.target command=\"\\\\tsclient\\C\\Temp\\file.exe\" username=domain\\user password=password connectdrive=true");
            Console.WriteLine("  Ask to take over RDP session if another used is logged in (workstation)");
            Console.WriteLine("    SharpRDP.exe computername=domain.target command=\"C:\\Temp\\file.exe\" username=domain\\user password=password takeover=true");
            Console.WriteLine("  Network level authentication");
            Console.WriteLine("    SharpRDP.exe computername=domain.target command=\"C:\\Temp\\file.exe\" username=domain\\user password=password nla=true");
            Console.WriteLine("  Execute command elevated through Run Dialog");
            Console.WriteLine("    SharpRDP.exe computername=domain.target command=\"C:\\Temp\\file.exe\" username=domain\\user password=password elevated=winr");
            Console.WriteLine("  Execute command elevated through task manager");
            Console.WriteLine("    SharpRDP.exe computername=domain.target command=\"C:\\Temp\\file.exe\" username=domain\\user password=password elevated=taskmgr");
        }
        static void Main(string[] args)
        {
            List<Layout> registredLayouts = new List<Layout>();

            AppDomain.CurrentDomain.AssemblyResolve += (sender, argtwo) =>
            {
                Assembly thisAssembly = Assembly.GetEntryAssembly();
                String resourceName = string.Format("SharpRDP.{0}.dll.bin",
                    new AssemblyName(argtwo.Name).Name);
                var assembly = Assembly.GetExecutingAssembly();
                using (var rs = assembly.GetManifestResourceStream(resourceName))
                using (var zs = new DeflateStream(rs, CompressionMode.Decompress))
                using (var ms = new MemoryStream())
                {
                    zs.CopyTo(ms);
                    return Assembly.Load(ms.ToArray());
                }
            };

            var arguments = new Dictionary<string, string>();
            foreach (string argument in args)
            {
                int idx = argument.IndexOf('=');
                if (idx > 0)
                    arguments[argument.Substring(0, idx)] = argument.Substring(idx + 1);
            }

            ConfigureLayoutConfigurations(arguments, registredLayouts);

            string username = string.Empty;
            string domain = string.Empty;
            string password = string.Empty;
            string command = string.Empty;
            string execElevated = string.Empty;
            int port = 3389;
            string execw = "";
            bool connectdrive = false;
            bool takeover = false;
            bool nla = false;

            if (arguments.ContainsKey("username"))
            {
                if (!arguments.ContainsKey("password"))
                {
                    Console.WriteLine("[X] Error: A password is required");
                    return;
                }
                else
                {
                    if (arguments["username"].Contains("\\"))
                    {
                        string[] tmp = arguments["username"].Split('\\');
                        domain = tmp[0];
                        username = tmp[1];
                    }
                    else
                    {
                        domain = ".";
                        username = arguments["username"];
                    }
                    password = arguments["password"];
                }
            }

            if (arguments.ContainsKey("password") && !arguments.ContainsKey("username"))
            {
                Console.WriteLine("[X] Error: A username is required");
                return;
            }
            if (arguments.ContainsKey("computername") && (arguments.ContainsKey("command")))
            {
                Client rdpconn = new Client();
                command = arguments["command"];
                if (arguments.ContainsKey("exec"))
                {
                    if (arguments["exec"].ToLower() == "cmd")
                    {
                        execw = "cmd";
                    }
                    else if (arguments["exec"].ToLower() == "powershell" || arguments["exec"].ToLower() == "ps")
                    {
                        execw = "powershell";
                    }
                }

                if (arguments.ContainsKey("elevated"))
                {
                    if (arguments["elevated"].ToLower() == "true" || arguments["elevated"].ToLower() == "win+r" || arguments["elevated"].ToLower() == "winr")
                    {
                        execElevated = "winr";
                    }
                    else if (arguments["elevated"].ToLower() == "taskmgr" || arguments["elevated"].ToLower() == "taskmanager")
                    {
                        execElevated = "taskmgr";
                    }
                    else
                    {
                        execElevated = string.Empty;
                    }
                }

                if (arguments.ContainsKey("connectdrive"))
                {
                    if (arguments["connectdrive"].ToLower() == "true")
                    {
                        connectdrive = true;
                    }
                }

                if (arguments.ContainsKey("takeover"))
                {
                    if (arguments["takeover"].ToLower() == "true")
                    {
                        takeover = true;
                    }
                }

                if (arguments.ContainsKey("nla"))
                {
                    if (arguments["nla"].ToLower() == "true")
                    {
                        nla = true;
                    }
                }

                if (arguments.ContainsKey("port"))
                {
                    if (int.TryParse(arguments["port"], out int portValue))
                    {
                        port = portValue;
                    }
                    else
                    {
                        Console.WriteLine("Port value is not valid.");
                    }
                }

                Layout chosenLayout = null;

                if (arguments.ContainsKey(LayoutArgumentKey))
                {
                    var layoutId = arguments[LayoutArgumentKey];

                    chosenLayout = registredLayouts
                        .FirstOrDefault(layout => layout.Id == layoutId);
                }

                if (chosenLayout == null)
                {
                    chosenLayout = registredLayouts.First();
                }

                string[] computerNames = arguments["computername"].Split(',');
                foreach (string server in computerNames)
                {
                    rdpconn.CreateRdpConnection(server, username, domain, port, password,
                        command, execw, execElevated, connectdrive, takeover, nla, chosenLayout.Keycode);
                }
            }
            else
            {
                HowTo();
                return;
            }

        }

        private static void ConfigureLayoutConfigurations(Dictionary<string, string> arguments, List<Layout> registredLayouts)
        {
            if (!arguments.ContainsKey(LayoutArgumentKey))
            {
                registredLayouts.Add(new Layout()
                {
                    Id = AmericanLayoutId,
                    Keycode = GetKeyCodesForAmericanLayout()
                });

                return;
            }

            var layouts = new List<Layout>();

            foreach (var fichierPath in Directory.GetFiles(JsonFolderPath, "*.json"))
            {
                try
                {
                    string contenuJson = File.ReadAllText(fichierPath);
                    Layout objet = JsonSerializer.Deserialize<Layout>(contenuJson);

                    if (objet != null)
                    {
                        layouts.Add(objet); // Ajouter l'objet désérialisé à la liste
                    }
                }
                catch (JsonException e)
                {
                    Console.WriteLine($"Erreur lors de la désérialisation de {Path.GetFileName(fichierPath)}: {e.Message}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Erreur inattendue lors de la lecture de {Path.GetFileName(fichierPath)}: {e.Message}");
                }
            }

            registredLayouts = layouts;
        }


        private static Dictionary<string, Code> GetKeyCodesForAmericanLayout()
        {
            Dictionary<string, Code> keycode = new Dictionary<string, Code>();

            keycode["Esc"] = new Code(new[] { false, true }, new[] { 0x01 });
            keycode["Enter+down"] = new Code(new[] { false }, new[] { 0x1c });
            keycode["Enter+up"] = new Code(new[] { true }, new[] { 0x1c });
            keycode["Win"] = new Code(new[] { false, true }, new[] { 0x15b });
            keycode["Down"] = new Code(new[] { false, true }, new[] { 0x150 });
            keycode["Right"] = new Code(new[] { false, true }, new[] { 0x14d });
            keycode["Left"] = new Code(new[] { false, true }, new[] { 0x14b });
            keycode["Alt"] = new Code(new[] { false, true }, new[] { 0x38 });
            keycode["Shift"] = new Code(new[] { false, true }, new[] { 0x2a });
            keycode["Space"] = new Code(new[] { false, true }, new[] { 0x39 });
            keycode["Tab"] = new Code(new[] { false, true }, new[] { 0x0f });

            keycode["Calc"] = new Code(new[] { false, true }, new[] { 0x121, 0x121 });
            keycode["Paste"] = new Code(new[] { false, true }, new[] { 0x10a, 0x10a });

            keycode["1"] = new Code(new[] { false, true }, new[] { 0x02 });
            keycode["2"] = new Code(new[] { false, true }, new[] { 0x03 });
            keycode["3"] = new Code(new[] { false, true }, new[] { 0x04 });
            keycode["4"] = new Code(new[] { false, true }, new[] { 0x05 });
            keycode["5"] = new Code(new[] { false, true }, new[] { 0x06 });
            keycode["6"] = new Code(new[] { false, true }, new[] { 0x07 });
            keycode["7"] = new Code(new[] { false, true }, new[] { 0x08 });
            keycode["8"] = new Code(new[] { false, true }, new[] { 0x09 });
            keycode["9"] = new Code(new[] { false, true }, new[] { 0x0a });
            keycode["0"] = new Code(new[] { false, true }, new[] { 0x0b });
            keycode["-"] = new Code(new[] { false, true }, new[] { 0x0c });

            keycode["a"] = new Code(new[] { false, true }, new[] { 0x1e });
            keycode["b"] = new Code(new[] { false, true }, new[] { 0x30 });
            keycode["c"] = new Code(new[] { false, true }, new[] { 0x2e });
            keycode["d"] = new Code(new[] { false, true }, new[] { 0x20 });
            keycode["e"] = new Code(new[] { false, true }, new[] { 0x12 });
            keycode["f"] = new Code(new[] { false, true }, new[] { 0x21 });
            keycode["g"] = new Code(new[] { false, true }, new[] { 0x22 });
            keycode["h"] = new Code(new[] { false, true }, new[] { 0x23 });
            keycode["i"] = new Code(new[] { false, true }, new[] { 0x17 });
            keycode["j"] = new Code(new[] { false, true }, new[] { 0x24 });
            keycode["k"] = new Code(new[] { false, true }, new[] { 0x25 });
            keycode["l"] = new Code(new[] { false, true }, new[] { 0x26 });
            keycode["m"] = new Code(new[] { false, true }, new[] { 0x32 });
            keycode["n"] = new Code(new[] { false, true }, new[] { 0x31 });
            keycode["o"] = new Code(new[] { false, true }, new[] { 0x18 });
            keycode["p"] = new Code(new[] { false, true }, new[] { 0x19 });
            keycode["q"] = new Code(new[] { false, true }, new[] { 0x10 });
            keycode["r"] = new Code(new[] { false, true }, new[] { 0x13 });
            keycode["s"] = new Code(new[] { false, true }, new[] { 0x1f });
            keycode["t"] = new Code(new[] { false, true }, new[] { 0x14 });
            keycode["u"] = new Code(new[] { false, true }, new[] { 0x16 });
            keycode["v"] = new Code(new[] { false, true }, new[] { 0x2f });
            keycode["w"] = new Code(new[] { false, true }, new[] { 0x11 });
            keycode["x"] = new Code(new[] { false, true }, new[] { 0x2d });
            keycode["y"] = new Code(new[] { false, true }, new[] { 0x15 });
            keycode["z"] = new Code(new[] { false, true }, new[] { 0x2c });
            keycode[" "] = new Code(new[] { false, true }, new[] { 0x39 });

            keycode["A"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x1e });
            keycode["B"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x30 });
            keycode["C"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x2e });
            keycode["D"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x20 });
            keycode["E"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x12 });
            keycode["F"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x21 });
            keycode["G"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x22 });
            keycode["H"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x23 });
            keycode["I"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x17 });
            keycode["J"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x24 });
            keycode["K"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x25 });
            keycode["L"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x26 });
            keycode["M"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x32 });
            keycode["N"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x31 });
            keycode["O"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x18 });
            keycode["P"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x19 });
            keycode["Q"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x10 });
            keycode["R"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x13 });
            keycode["S"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x1f });
            keycode["T"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x14 });
            keycode["U"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x16 });
            keycode["V"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x2f });
            keycode["W"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x11 });
            keycode["X"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x2d });
            keycode["Y"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x15 });
            keycode["Z"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x2c });

            keycode[","] = new Code(new[] { false, true }, new[] { 0x33 });
            keycode["."] = new Code(new[] { false, true }, new[] { 0x34 });
            keycode["/"] = new Code(new[] { false, true }, new[] { 0x35 });
            keycode["["] = new Code(new[] { false, true }, new[] { 0x1a });
            keycode["]"] = new Code(new[] { false, true }, new[] { 0x1b });
            keycode["\\"] = new Code(new[] { false, true }, new[] { 0x2b });
            keycode[";"] = new Code(new[] { false, true }, new[] { 0x27 });
            keycode["'"] = new Code(new[] { false, true }, new[] { 0x28 });
            keycode["="] = new Code(new[] { false, true }, new[] { 0x0D });
            keycode["+"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x0D });
            keycode["<"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x33 });
            keycode[">"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x34 });
            keycode["\""] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x28 });
            keycode[":"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x27 });
            keycode["|"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x2b });
            keycode["&"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x08 });
            keycode["%"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x06 });
            keycode["("] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x0a });
            keycode[")"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x0b });

            keycode["Win+R+down"] = new Code(new[] { false, false }, new[] { 0x15b, 0x13 });
            keycode["Win+R+up"] = new Code(new[] { true, true }, new[] { 0x15b, 0x13 });
            keycode["Win+D"] = new Code(new[] { false, false, true, true }, new[] { 0x15b, 0x20 });
            keycode["Alt+Shift"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x2a });
            keycode["Alt+Space"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x39 });
            keycode["Ctrl+Shift"] = new Code(new[] { false, false, true, true }, new[] { 0x1d, 0x2a });
            keycode["Alt+F4"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x3e });
            keycode["Ctrl+V"] = new Code(new[] { false, false, true, true }, new[] { 0x1d, 0x2f });
            keycode["Alt+F"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x21 });

            keycode["Ctrl+Shift+down"] = new Code(new[] { false, false }, new[] { 0x1d, 0x2a });
            keycode["Ctrl+Shift+up"] = new Code(new[] { true, true }, new[] { 0x1d, 0x2a });

            return keycode;
        }

    }
}