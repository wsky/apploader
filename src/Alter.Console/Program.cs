using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Configuration;

namespace Alter.Console
{
    /// <summary>
    /// 服务主宿主
    /// </summary>
    public class Program
    {
        static AppDomainLoader _loader;
        static ILog _log;
        static FileStream _file;
        static void Main(string[] args)
        {
            WriteTip("服务动态发布宿主启动");
            WriteTip("请求启动锁定");
            TryKeekAlive();

            //配置初始化
            Start();

            //简单cmd处理
            while (true)
            {
                if (HandleCommand()) break;
                Thread.Sleep(100);
            }

            WriteTip("卸载所有应用", true);
            _loader.Clear();
            WriteTip("服务宿主退出", true);
            Thread.Sleep(5000);
        }
        private static bool IsCommand(string cmd)
        {
            return System.Console.ReadLine().Equals(cmd, StringComparison.InvariantCultureIgnoreCase);
        }
        private static void WriteTip(string tip)
        {
            WriteTip(tip, false);
        }
        private static void WriteTip(string tip, bool log)
        {
            var info = string.Format("========={0}========", tip);
            if (log && _log != null)
                _log.Info(info);
            else
                System.Console.WriteLine(info);
        }
        private static void TryKeekAlive()
        {
            while (true)
            {
                try
                {
                    _file = new FileStream("KeekAlive", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    break;
                }
                catch
                {
                    Thread.Sleep(10000);
                }
            }
        }
        private static void PrepareErrorHandle()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (_log == null)
                    System.Console.WriteLine((e.ExceptionObject as Exception).Message);
                else if (e.IsTerminating)
                    _log.Fatal("-->发生严重错误|IsTerminating="
                        + e.IsTerminating, e.ExceptionObject as Exception);
                else
                    _log.Error("-->发生意外错误|IsTerminating="
                        + e.IsTerminating, e.ExceptionObject as Exception);
            };
        }
        //exit refresh clear
        private static bool HandleCommand()
        {
            if (IsCommand("exit"))
                return true;
            else if (IsCommand("refresh"))
            {
                _loader.Clear();
                _loader.Scan();
            }
            else if (IsCommand("clear"))
            {
                System.Console.Clear();
                WriteTip("服务动态发布宿主");
            }
            return false;
        }

        public static void Start()
        {
            PrepareErrorHandle();

            var root = ConfigurationManager.AppSettings["serviceRoot"];
            Directory.CreateDirectory(root);
            //配置初始化
            log4net.Config.XmlConfigurator.Configure();
            _log = new Log4NetLogger(log4net.LogManager.GetLogger(typeof(Program)));
            _loader = new AppDomainLoader(root, _log);
            WriteTip("开始扫描发布目录：" + ConfigurationManager.AppSettings["serviceRoot"], true);
            _loader.Scan();
            WriteTip("扫描完毕", true);

        }
        public static void Stop()
        {
            _loader.Clear();
        }
    }
}