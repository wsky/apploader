using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.ServiceProcess;

namespace Alter.WinService
{
    [RunInstaller(true)] 
    public class ProjectInstaller : System.Configuration.Install.Installer
    {
        private ServiceInstaller _serviceInstaller;
        private ServiceProcessInstaller _processInstaller;

        public ProjectInstaller()
        {
            this._processInstaller = new ServiceProcessInstaller();
            this._processInstaller.Account = ServiceAccount.LocalSystem;
            //this._processInstaller.Account = ServiceAccount.User;
            //this._processInstaller.Username = @"hz\service";

            this._serviceInstaller = new ServiceInstaller();
            this._serviceInstaller.StartType = ServiceStartMode.Automatic;
            this._serviceInstaller.ServiceName = "Alter.AppLoaderWinService";

            Installers.Add(this._serviceInstaller);
            Installers.Add(this._processInstaller);
        }
    }
}
