using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace Alter.WinService
{
    public partial class AppLoaderService : ServiceBase
    {
        public AppLoaderService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Alter.Console.Program.Start();
        }

        protected override void OnStop()
        {
            Alter.Console.Program.Stop();
        }
    }
}
