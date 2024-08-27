using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WatcherCore;

public class Watcher {
    public static readonly List<string> FileTypes =
        [".png", ".jpg", ".webp", ".bmp", ".gif", ".mp3", ".wav", ".ogg", ".flac", ".xnb", ".json"];

    public static readonly HashSet<string> IgnoreFolders = new([".git", "bin", "obj", ".idea", "Properties", "Localization", "Resource", ".vs"], StringComparer.OrdinalIgnoreCase);

    public static readonly string ShaderCompile = "ShaderCompile/ShaderCompile.exe";

    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly TreeItem _root;

    public string FilePath => Args.Path;
    public Args Args;
    public bool Silent { get; set; }

    public StringBuilder Code { get; } = new();

    public Watcher(Args args) {
        Args = args;
        _root = new(Path.GetFileName(FilePath), FilePath, null, true);

        Console.WriteLine("\n开始进行编译着色器......");
        CompileShader(FilePath);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("全部着色器编译完毕");
        Console.ResetColor();
        Console.WriteLine("\n正在生成资源引用...");
        LoadTree(FilePath, _root);
        GenerateCode();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("生成资源引用完毕");
        Console.ResetColor();

        _fileSystemWatcher = new(FilePath);
        _fileSystemWatcher.Created += FileSystemWatcher;
        _fileSystemWatcher.Deleted += FileSystemWatcher;
        _fileSystemWatcher.Renamed += FileSystemWatcher;
        _fileSystemWatcher.Changed += FileSystemWatcher;
        _fileSystemWatcher.Error += FileSystemWatcherOnError;
        _fileSystemWatcher.IncludeSubdirectories = true;
    }

    public void Start() {
        _fileSystemWatcher.EnableRaisingEvents = true;
    }

    public void Stop() {
        if (_fileSystemWatcher == null) {
            return;
        }
        _fileSystemWatcher.EnableRaisingEvents = false;
    }

    private void GenerateCode() {
        var folderPath = Path.Combine(FilePath, "Resource");
        if (Directory.Exists(folderPath))
            Directory.Delete(folderPath, true);
        Directory.CreateDirectory(folderPath);
        FileStream fileStream = File.Create(Path.Combine(folderPath, "R.cs"));
        using StreamWriter writer = new(fileStream);
        writer.Write(new GenerateCode(_root, Args).Generate());
    }

    private void LoadTree(string path, TreeItem treeItem, bool root = true) {
        if (!root || !Args.IgnoreRoot) {
            foreach (var file in Directory.GetFiles(path)) {
                if (!FileTypes.Contains(Path.GetExtension(file)))
                    continue;
                var relativePath = Path.GetRelativePath(FilePath, file);
                treeItem.CreateChild(Path.GetFileNameWithoutExtension(file), file, relativePath, false);
            }
        }

        foreach (var directory in Directory.GetDirectories(path)) {
            var relativePath = Path.GetRelativePath(FilePath, directory);
            if (IgnoreFolders.Contains(relativePath))
                continue;
            TreeItem dirTreeItem = treeItem.CreateChild(Path.GetFileName(directory), directory, relativePath);
            LoadTree(directory, dirTreeItem, false);
        }
    }

    private void CompileShader(string path) {
        foreach (var file in Directory.GetFiles(path))
            if (Path.GetExtension(file) == ".fx")
                CompileFx(file);

        foreach (var directory in Directory.GetDirectories(path))
            CompileShader(directory);
    }

    private void CompileFx(string path) {
        TrySetConsoleForegroundColor(ConsoleColor.Yellow);
        TryConsoleWrite("[编译着色器]  ");
        TrySetConsoleForegroundColor(ConsoleColor.Magenta);
        TryConsoleWrite(Path.GetRelativePath(FilePath, path));

        ProcessStartInfo startInfo = new() {
            FileName = ShaderCompile, // 替换为你要调用的外部工具路径
            Arguments = $"\"{path}\"", // 替换为要传入的参数
            RedirectStandardError = Silent,
            RedirectStandardOutput = Silent,
        };

        using var process = Process.Start(startInfo);
        if (process == null) {
            TrySetConsoleForegroundColor(ConsoleColor.Red);
            TryConsoleWriteLine("  ---无法启动进程!");
            Console.ResetColor();
            return;
        }
        process.WaitForExit();
        if (process.ExitCode == 0) {
            TrySetConsoleForegroundColor(ConsoleColor.Green);
            TryConsoleWriteLine("  ---编译成功");
            Console.ResetColor();
        }
        else {
            TrySetConsoleForegroundColor(ConsoleColor.Red);
            TryConsoleWriteLine("编译失败");
            Console.ResetColor();
        }
    }

    #region Callback

    private static DateTime _lastEventTime;
    private static string? _lastEventInfo;
    private readonly static string BACK_OFF = "\r";
    private readonly static string COMMAND_START = "> ";

    private void FileSystemWatcher(object sender, FileSystemEventArgs e) {
        var eventInfo = $"{e.Name} {e.FullPath} {e.ChangeType}";

        if (eventInfo == _lastEventInfo) {
            var elapsed = DateTime.Now - _lastEventTime;
            if (elapsed.TotalMilliseconds < 100) {
                return; // 忽略短时间内的重复事件
            }
        }
            

        _lastEventTime = DateTime.Now;
        _lastEventInfo = eventInfo;
        if (WatcherTask == null || WatcherTask.IsCompleted) {
            WatcherTask = Task.Run(() => FileSystemWatcherAsync(e));
        }
        else {
            WatcherTask = WatcherTask.ContinueWith(t => FileSystemWatcherAsync(e));
        }
    }
    private static Task? WatcherTask { get; set; }
    private void FileSystemWatcherAsync(FileSystemEventArgs e) {
        var relativePath = Path.GetRelativePath(FilePath, e.FullPath);

        var directory = Path.GetDirectoryName(relativePath);

        //忽略文件夹
        if (string.IsNullOrEmpty(directory)) {
            if (Args.IgnoreRoot) {
                return;
            }
        }
        else {
            var splits = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string? testDirectory = null;
            for (int i = 0; i < splits.Length; ++i) {
                testDirectory = testDirectory == null ? splits[i] : Path.Combine(testDirectory, splits[i]);
                if (IgnoreFolders.Contains(testDirectory)) {
                    return;
                }
            }
        }

        if (e.ChangeType == WatcherChangeTypes.Changed) {
            if (!e.FullPath.EndsWith(".fx"))
                return;
            TryConsoleWrite(BACK_OFF);
            TrySetConsoleForegroundColor(ConsoleColor.Blue);
            TryConsoleWrite("[着色器代码更改]  ");
            TrySetConsoleForegroundColor(ConsoleColor.Cyan);
            TryConsoleWrite(relativePath);
            TrySetConsoleForegroundColor(ConsoleColor.White);
            TryConsoleWriteLine($"  {DateTime.Now}");
            // TrySetConsoleForegroundColor(ConsoleColor.Green);
            // TryConsoleWriteLine("开始重新编译着色器......");
            Console.ResetColor();
            //编译着色器
            CompileFx(e.FullPath);
            TryConsoleWrite(COMMAND_START);
            return;
        }

        //编译着色器
        if (e.FullPath.EndsWith(".fx") && e.ChangeType != WatcherChangeTypes.Deleted) {
            TryConsoleWrite(BACK_OFF);
            CompileFx(e.FullPath);
            TryConsoleWrite(COMMAND_START);
        }

        //忽略文件类型
        if (!FileTypes.Contains(Path.GetExtension(e.FullPath)))
            return;

        //重新监测并生成代码
        _root.CleanChild();
        LoadTree(FilePath, _root);
        GenerateCode();
        
        TryConsoleWrite(BACK_OFF);
        // 打印监测信息
        switch (e.ChangeType) {
        case WatcherChangeTypes.Created:
            TrySetConsoleForegroundColor(ConsoleColor.Green);
            TryConsoleWrite("[文件创建]  ");
            TrySetConsoleForegroundColor(ConsoleColor.Cyan);
            TryConsoleWrite(relativePath);
            TrySetConsoleForegroundColor(ConsoleColor.White);
            TryConsoleWriteLine($"  {DateTime.Now}");
            break;
        case WatcherChangeTypes.Deleted:
            TrySetConsoleForegroundColor(ConsoleColor.Yellow);
            TryConsoleWrite("[文件删除]  ");
            TrySetConsoleForegroundColor(ConsoleColor.Cyan);
            TryConsoleWrite(relativePath);
            TrySetConsoleForegroundColor(ConsoleColor.White);
            TryConsoleWriteLine($"  {DateTime.Now}");
            break;
        case WatcherChangeTypes.Renamed:
            TrySetConsoleForegroundColor(ConsoleColor.Blue);
            TryConsoleWrite("[文件重复名]  ");
            TrySetConsoleForegroundColor(ConsoleColor.Cyan);
            TryConsoleWrite(relativePath);
            TrySetConsoleForegroundColor(ConsoleColor.White);
            TryConsoleWriteLine($"  {DateTime.Now}");
            break;
        case WatcherChangeTypes.All:
            break;
        default:
            TrySetConsoleForegroundColor(ConsoleColor.Red);
            TryConsoleWrite("[未知操作]  ");
            TrySetConsoleForegroundColor(ConsoleColor.Cyan);
            TryConsoleWrite(relativePath);
            TrySetConsoleForegroundColor(ConsoleColor.White);
            TryConsoleWriteLine($"  {DateTime.Now}");
            break;
        }
        Console.ResetColor();
        TryConsoleWrite(COMMAND_START);
    }

    private static void FileSystemWatcherOnError(object sender, ErrorEventArgs e) {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("监测解决方案时遇到以下错误！");
        Console.WriteLine(sender);
        Console.WriteLine(e.GetException().Message);
        Console.ResetColor();
    }

    #endregion

    private void TryConsoleWrite(string str) {
        if (!Silent) {
            Console.Write(str);
        }
    }
    private void TryConsoleWriteLine(string str) {
        if (!Silent) {
            Console.WriteLine(str);
        }
    }
    private void TrySetConsoleForegroundColor(ConsoleColor color) {
        if (!Silent) {
            Console.ForegroundColor = color;
        }
    }
}