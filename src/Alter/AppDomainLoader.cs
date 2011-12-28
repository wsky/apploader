using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Security.Policy;
using System.Security;
using System.Security.Permissions;
using System.Threading;

namespace Alter
{
    /// <summary>
    /// 动态AppDomain加载器
    /// 每个Domain和父Domain完全隔离，不共享程序集
    /// 检测每个目录的*.dll文件变化自动重新加载
    /// <remarks>
    /// 根据目录分组加载，并自动监控目录改动重新加载
    /// 如：
    /// 自动扫描c:/root/
    /// 将c:/root/app1/加载为flag=app1的AppDomain
    /// 默认dll搜索目录为GAC，c:/root/app1/和c:/root/app1/bin
    /// </remarks>
    /// </summary>
    [Serializable]
    public class AppDomainLoader
    {
        //TODO:增加根目录监控
        private static readonly string _friendlyName = "____dynamicAppDomain_";
        private static readonly string _file_config = "*.config";
        private static readonly string _file_dll = "*.dll";
        private static readonly string _file_watch = "*.dll";
        private string _shadowCopyPath; 
        private ILog _log;
        private int _counter;
        /// <summary>
        /// 程序集目录组的根路径，如：c:/root/
        /// </summary>
        private string _root;
        /// <summary>
        /// 应用程序 键=AppDomain名称
        /// </summary>
        private IDictionary<string, App> _apps;
        /// <summary>
        /// 初始化加载器
        /// </summary>
        /// <param name="root">程序集目录组的根路径，如：c:/root/</param>
        /// <param name="log"></param>
        public AppDomainLoader(string root, ILog log)
        {
            this._log = log;
            this._root = root;
            this._counter = 0;
            this._apps = new Dictionary<string, App>();
            this._shadowCopyPath = Path.Combine(this._root, "shadowcopy");
        }
        /// <summary>
        /// 开始扫描
        /// </summary>
        public void Scan()
        {
            Directory.GetDirectories(this._root)
                .Where(o => !o.Equals(this._shadowCopyPath, StringComparison.InvariantCultureIgnoreCase) && !this._apps.ContainsKey(o))
                .ToList()
                .ForEach(o => this.LoadFrom(o));
        }
        /// <summary>
        /// 卸载全部
        /// </summary>
        public void Clear()
        {
            this._apps.Keys.ToList().ForEach(o => this.Unload(this._apps[o]));
        }
        private void Reload(App app)
        {
            if (app.Locked) return;
            lock (app)
            {
                if (app.Locked) return;
                app.Locked = true;
            }
            if (this.Unload(app))
                this.LoadFrom(app.Path);
        }
        private bool Unload(App app)
        {
            if (!string.IsNullOrEmpty(app.EntranceTypeName))
                this.UnloadApp(app);

            var i = 0;
            bool flag;
            while (!(flag = this.UnloadAppDomain(app.Domain)) && i++ < 3) Thread.Sleep(10);

            //仅当成功卸载才继续
            if (flag)
            {
                app.Watcher.Dispose();
                this._apps.Remove(app.Key);
                this._log.InfoFormat("卸载App#{0}", app.Key);
            }
            else
                this._log.ErrorFormat("卸载App#{0}但发生意外，没能正确卸载，请查看上述日志", app.Key);

            return flag;
        }
        private void UnloadApp(App app)
        {
            try
            {
                (app.Domain.CreateInstanceAndUnwrap(app.EntranceAssemblyName, app.EntranceTypeName) as Entrance).Unload();
            }
            catch (Exception e)
            {
                this._log.Error("调用Unload()卸载时发生异常", e);
            }
        }
        private bool UnloadAppDomain(AppDomain appDomain)
        {
            try
            {
                AppDomain.Unload(appDomain);
                return true;
            }
            catch (Exception e)
            {
                this._log.Error("卸载AppDomain异常，将重试", e);
                return false;
            }
        }
        private void LoadFrom(string assemblyPath)
        {
            var app = new App() { Path = assemblyPath, Key = _friendlyName + (++this._counter) };
            //初始化AppDomain
            this.PrepareAppDomain(app);
            //初始化目录监控
            this.PrepareWatcher(app);
            //加入缓存
            this._apps.Add(app.Key, app);
            //加载程序集
            Directory.GetFiles(assemblyPath
                , _file_dll
                , SearchOption.AllDirectories)
                .ToList()
                .ForEach(o => this.LoadTo(app, o));
            //尝试入口初始化
            this.Main(app);
            this._log.InfoFormat("AppDomain#{0}初始化完毕", app.Key);
        }
        private void Main(App app)
        {
            if (string.IsNullOrEmpty(app.EntranceTypeName))
                return;

            this._log.InfoFormat("发现入口类型{0}，{1}", app.EntranceTypeName, app.EntranceAssemblyName);

            try
            {
                (app.Domain.CreateInstanceAndUnwrap(app.EntranceAssemblyName, app.EntranceTypeName) as Entrance).Main();
            }
            catch (Exception e)
            {
                this._log.Error("调用Main()初始化异常", e);
            }
        }
        private void PrepareAppDomain(App app)
        {
            //appdomain设置
            var setup = new AppDomainSetup();
            setup.ApplicationName = app.Key;
            //将目录设置为外接程序集目录
            setup.ApplicationBase = app.Path;
            //将私有bin目录指向加载器宿主目录
            setup.PrivateBinPathProbe = "*";
            setup.PrivateBinPath = app.Path + ";bin";
            //对程序集启用影复制，使得assemblyPath下的文件可以在运行时被更新
            setup.ShadowCopyFiles = "true";
            setup.CachePath = this._shadowCopyPath;
            //设置配置文件
            var appConfig = Directory.GetFiles(app.Path, _file_config, SearchOption.TopDirectoryOnly);
            if (appConfig.Length > 0) setup.ConfigurationFile = appConfig[0];
            //创建appdomain
            app.Domain = AppDomain.CreateDomain(app.Key
                , AppDomain.CurrentDomain.Evidence
                , setup);
            //注册toolkit
            this.PrepareUtility(app);
            //安全级别
            //var level = PolicyLevel.CreateAppDomainLevel();
            //var ps = level.GetNamedPermissionSet("Internet");
            //ps.AddPermission(new FileIOPermission(FileIOPermissionAccess.AllAccess, app.Path));
            //level.RootCodeGroup.PolicyStatement = new PolicyStatement(ps);
            //app.Domain.SetAppDomainPolicy(level);

            this._log.InfoFormat("从路径{0}创建AppDomain#{1}并加载了Taobao.Infrastructure.Toolkit", app.Path, app.Key);
            this._log.DebugFormat("ApplicationName={0}", setup.ApplicationName);
            this._log.DebugFormat("ApplicationBase={0}", setup.ApplicationBase);
            this._log.DebugFormat("PrivateBinPathProbe={0}", setup.PrivateBinPathProbe);
            this._log.DebugFormat("PrivateBinPath={0}", setup.PrivateBinPath);
            this._log.DebugFormat("ConfigurationFile={0}", setup.ConfigurationFile);
            this._log.DebugFormat("ShadowCopyFiles={0}", setup.ShadowCopyFiles);
            this._log.DebugFormat("ShadowCopyDirectories={0}", setup.ShadowCopyDirectories);
            this._log.DebugFormat("CachePath={0}", setup.CachePath);
        }
        private void PrepareUtility(App app)
        {
            try
            {
                app.Utility = app.Domain.CreateInstanceAndUnwrap("Taobao.Infrastructure.Toolkit"
                    , typeof(Facade).FullName) as Facade;
            }
            catch (Exception e)
            {
                this._log.Warn("为注册Toolkit时发生意外错误，可能该目录下不存在程序集Taobao.Infrastructure.Toolkit", e);
            }
        }
        private void PrepareWatcher(App app)
        {
            var watcher = new FileSystemWatcher(app.Path, _file_watch);
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
            watcher.Deleted += (s, e) => { this._log.InfoFormat("检测到目录{0}下发生删除", app.Path); Thread.Sleep(5000); this.Reload(app); };
            watcher.Created += (s, e) => { this._log.InfoFormat("检测到目录{0}下发生新增", app.Path); Thread.Sleep(5000); this.Reload(app); };
            watcher.Changed += (s, e) => { this._log.InfoFormat("检测到目录{0}下发生改变", app.Path); Thread.Sleep(5000); this.Reload(app); };
            app.Watcher = watcher;
            this._log.DebugFormat("对目录{0}设置了FileSystemWatcher，检测文件类型为{1}", app.Path, _file_watch);
        }
        private void LoadTo(App app, string assemblyString)
        {
            try
            {
                var appDomain = app.Domain;
                var entranceTypeName = app.Utility.LoadAssemblyFromFile(assemblyString);
                if (!string.IsNullOrEmpty(entranceTypeName))
                {
                    app.EntranceTypeName = entranceTypeName;
                    app.EntranceAssemblyName = this.ParseAssemblyName(assemblyString);
                }
                this._log.DebugFormat("为AppDomain#{0}载入程序集{1}", app.Key, assemblyString);
            }
            catch (Exception e)
            {
                this._log.Error("加载程序集" + assemblyString + "时发生异常", e);
            }
        }

        /// <summary>
        /// 应用 AppDomainWrapper
        /// </summary>
        private class App
        {
            /// <summary>
            /// 是否被锁定
            /// </summary>
            public bool Locked { get; set; }
            /// <summary>
            /// 标识
            /// </summary>
            public string Key { get; set; }
            /// <summary>
            /// 加载路径
            /// </summary>
            public string Path { get; set; }
            /// <summary>
            /// AppDomain
            /// </summary>
            public AppDomain Domain { get; set; }
            /// <summary>
            /// 目录监控
            /// </summary>
            public FileSystemWatcher Watcher { get; set; }
            /// <summary>
            /// domain工具
            /// </summary>
            public Facade Utility { get; set; }
            /// <summary>
            /// 入口程序集名称
            /// </summary>
            public string EntranceAssemblyName { get; set; }
            /// <summary>
            /// 入口类型名称
            /// </summary>
            public string EntranceTypeName { get; set; }
        }
        private string ParseAssemblyName(string name)
        {
            return name.Substring(name.LastIndexOf(@"\") + 1).Replace(".dll", "");
        }
    }
}