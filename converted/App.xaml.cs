using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace VB2CS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // VB6 project Startup="frm": show the form's default instance
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Forms.frm.instance.Show();
        }
    }
}
