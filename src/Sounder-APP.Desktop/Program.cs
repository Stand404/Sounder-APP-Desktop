using Avalonia;
using Sounder_APP.Services;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Sounder_APP
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // 注册全局异常处理 — 将崩溃信息写入日志文件
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                LogCrash($"=== CRASH at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n" +
                         $"Exception: {e.ExceptionObject}\n" +
                         $"IsTerminating: {e.IsTerminating}\n" +
                         $"=== END ===\n\n");
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                LogCrash($"=== TASK CRASH at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n" +
                         $"Exception: {e.Exception}\n" +
                         $"=== END ===\n\n");
                e.SetObserved();
            };

            // 单实例检测：如果不是第一个实例，将参数转发给主实例并退出
            if (!SingleInstanceService.TryAcquire(args))
                return;

            // 注册自定义 URL 协议
            UrlProtocolService.RegisterProtocol();

            try
            {
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex) when (ex.Message.Contains("XOpenDisplay"))
            {
                LogCrash($"XOpenDisplay failed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}\n");
                PrintX11Error();
                Environment.Exit(1);
            }
        }

        /// <summary>输出 X11 显示错误的友好提示</summary>
        private static void PrintX11Error()
        {
            Console.Error.WriteLine("============================================");
            Console.Error.WriteLine("  错误：无法打开 X11 显示。");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("  便携版（单文件）在部分 Linux 上需额外配置：");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("  方案一：设置自解压目录（推荐）");
            Console.Error.WriteLine("    export DOTNET_BUNDLE_EXTRACT_BASE_DIR=$HOME/.cache/sounder");
            Console.Error.WriteLine("    ./Sounder-APP");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("  方案二：使用 deb 包安装（兼容性最佳）");
            Console.Error.WriteLine("    sudo dpkg -i sounder-app_*.deb");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("  如果已在桌面环境，检查是否通过 SSH 运行：");
            Console.Error.WriteLine("    使用 ssh -X hostname 重新连接");
            Console.Error.WriteLine("============================================");
        }

        /// <summary>将崩溃日志写入本地应用数据目录</summary>
        private static void LogCrash(string message)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Sounder-APP");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "crash.log"), message);
            }
            catch { }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
#if DEBUG
                .WithDeveloperTools()
#endif
                .WithInterFont()
                .LogToTrace();
    }
}
