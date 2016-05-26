using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using FTPboxLib;

namespace FTPboxLib
{
    [RunInstaller(true)]
    public partial class Installer1 : System.Configuration.Install.Installer
    {
        public override void Uninstall(System.Collections.IDictionary savedState)
        {
            base.Uninstall(savedState);
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(Common.AppdataFolder, Microsoft.VisualBasic.FileIO.DeleteDirectoryOption.DeleteAllContents);
        }
    }
}
