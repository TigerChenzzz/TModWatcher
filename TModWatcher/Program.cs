using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WatcherCore;

namespace TModWatcher;

public class Program {
    public static Args Args { get; private set; } = null!;
    public static string WorkPath => Args.Path;
    public static Watcher Watcher { get; private set; } = null!;
    public static void Main(string[] args) {
        HandleArgs(args);
        Start();
        HandleCommandLoop();
    }

    private static void HandleArgs(string[] args) {
        Dictionary<string, string> arguments = new(StringComparer.OrdinalIgnoreCase);

        foreach (var arg in args) {
            if (!arg.StartsWith('-')) {
                arguments["path"] = arg;
                continue;
            }
            var parts = arg.TrimStart('-').Split('=', 2);
            if (parts.Length == 2)
                arguments[parts[0]] = parts[1];

        }
        T ProcessArgument<T>(string[] argAlters, Func<string, T> processor, T defaultValue) {
            foreach (var alter in argAlters) {
                if (arguments.TryGetValue(alter, out var value)) {
                    return processor(value);
                }
            }
            return defaultValue;
        }
        void HandleArgument(string[] argAlters, Action<string> handler){
            foreach (var alter in argAlters) {
                if (arguments.TryGetValue(alter, out var value)) {
                    handler(value);
                }
            }
        }
        Args = new(
            ProcessArgument(["path", "SlnPath", "sln_path"], s => s, AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')),
            ProcessArgument(["SnakeCase", "snake_case"], s => s == "true", true),
            ProcessArgument(["GenerateExtension", "generate_extension"], s => s == "true", true),
            ProcessArgument(["GenerateString", "generate_string"], s => s == "true", true),
            ProcessArgument(["IgnoreRoot", "ignore_root"], s => s == "true", false)
        );
        HandleArgument(["IgnoreFolders", "ignore_folders", "IgnoreFolder", "ignore_folder"], ignoreFolders => {
            foreach (var ignore in ignoreFolders.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                Watcher.IgnoreFolders.Add(ignore.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
            }
        });
    }
    #region Start
    private static void Start() {
        PrintTModWatcherWelcome();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n输入指令 exit 或 Ctrl + C 以退出程序");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n正在启动监听程序......");
        Console.ResetColor();

        if (!HasCsprojOrSlnFile(WorkPath)) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n{WorkPath}\n工作目录不是一个有效目录！并没有找到解决方案！");
            Console.ResetColor();
            return;
        }
        try {
            Watcher = new(Args);
            Watcher.Start();
        }
        catch (Exception e) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("监听程序启动失败!");
            Console.WriteLine(e);
            Console.ResetColor();
            return;
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n监听程序启动成功！");
        Console.ResetColor();
        Console.WriteLine($"\n正在监听项目:{WorkPath}");
    }
    private static void PrintTModWatcherWelcome() {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("欢迎使用 ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("TModWatcher！");
        Console.ResetColor();

        Console.Write("这款工具由 ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("TrifingZW ");
        Console.ResetColor();
        Console.WriteLine("开发，旨在帮助你 ");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("自动生成资源的C#引用并编译着色器文件");
        Console.ResetColor();
        Console.WriteLine("，让你的 Terraria 模组开发更加轻松！");

        Console.Write("本工具隶属于 ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("万物一心 ");
        Console.ResetColor();
        Console.Write("团队 (");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("https://github.com/ForOne-Club");
        Console.ResetColor();
        Console.WriteLine(")，一个致力于 Terraria 模组开发的团队。");

        Console.Write("如果你有任何问题，欢迎加入我们的QQ群：");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("574904188 ");
        Console.ResetColor();
        Console.Write("或者联系开发者QQ：");
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("3077446541");
        Console.ResetColor();

        Console.Write("你也可以访问开发者的GitHub主页：");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("https://github.com/TrifingZW ");
        Console.ResetColor();
        Console.WriteLine("获取更多信息。");

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("TModWatcher ");
        Console.ResetColor();
        Console.Write("基于 ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("MIT开源协议");
        Console.ResetColor();
        Console.WriteLine("，请自觉遵守协议规则。");
    }
    public static bool HasCsprojOrSlnFile(string directoryPath) {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            return false;

        // 获取文件夹中的所有文件
        var files = Directory.GetFiles(directoryPath);

        // 遍历文件，查找 .csproj 或 .sln 文件
        return files.Any(file =>
            Path.GetExtension(file).Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(file).Equals(".sln", StringComparison.OrdinalIgnoreCase));
    }
    #endregion
    #region Command
    private static void HandleCommandLoop() {
        while (true) {
            Console.Write("> ");
            var command = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(command)) {
                continue;
            }
            var splits = command.TrimStart().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (splits.Length == 0) {
                return;
            }
            if (Commands.TryGetValue(splits[0], out var commandAction)) {
                commandAction(splits.Length > 1 ? splits[1] : null);
            }
            else {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("未知命令! 输入 help 以获取命令列表");
                Console.ResetColor();
            }
        }
    }
    private static Dictionary<string, Action<string?>> Commands { get; } = new(StringComparer.OrdinalIgnoreCase) {
        { "help", _ => {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("""
                help: 获取命令列表
                silent: 获取或设置是否在文件变化时打印日志
                exit: 退出程序
                """);
            Console.ResetColor();
        } },
        { "silent", s => {
            if (s == null) {
                Console.WriteLine("目前的静默状态为 " + Watcher.Silent);
                return;
            }
            if (s is "true" or "True" or "1") {
                Watcher.Silent = true;
                Console.ResetColor();
                Console.WriteLine("静默状态已设置为 " + true);
            }
            else if (s is "false" or "False" or "0") {
                Watcher.Silent = false;
                Console.WriteLine("静默状态已设置为 " + false);
            }
            else {
                Console.WriteLine("参数错误, 请输入 silent true 或者 silent false");
            }
        } },
        { "exit", _ => Environment.Exit(0) },
    };
    #endregion
}