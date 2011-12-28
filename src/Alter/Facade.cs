using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Configuration;

namespace Alter
{
    /// <summary>
    /// 运行于独立domain中
    /// </summary>
    public class Facade : MarshalByRefObject
    {
        /// <summary>
        /// 从文件中加载程序集
        /// </summary>
        /// <param name="path"></param>
        /// <returns>若含有入口则返回入口类型全名</returns>
        public string LoadAssemblyFromFile(string path)
        {
            var entranceTypeName = ConfigurationManager.AppSettings["AppDomainLoaderEntrance"];
            //加载到appdomain
            var assembly = Assembly.LoadFrom(path);//LoadFile会导致锁定

            if (string.IsNullOrEmpty(entranceTypeName))
            {
                var entrance = assembly.GetType("Entrance", false);
                return entrance == null ? string.Empty : entrance.FullName;
            }
            else
            {
                var entrance = Type.GetType(entranceTypeName, false);
                return entrance == null || entrance.Assembly.FullName != assembly.FullName
                    ? string.Empty
                    : entrance.FullName;
            }
        }
    }
}