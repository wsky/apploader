using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Alter
{
    /// <summary>
    /// 声明AppDomain的加载入口
    /// </summary>
    public abstract class Entrance : MarshalByRefObject
    {
        /// <summary>
        /// AppDomain被加载后的入口方法
        /// </summary>
        /// <returns></returns>
        public abstract void Main();
        /// <summary>
        /// AppDomain的卸载方法
        /// </summary>
        public abstract void Unload();
    }
    //HACK:入口需要在新AppDomain中执行，若用接口需要使用者自行声明为MarshalByRefObject（remoting）
    interface IEntrance { void Main(); }
}