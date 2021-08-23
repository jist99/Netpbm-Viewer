using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Netpbm_Viewer
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private void Application_Startup(object sender, StartupEventArgs e)
		{
			MainWindow window = new MainWindow();

			if(e.Args.Length >= 1)
			{
				window.fp = e.Args[0];
				window.loadFile();
			}

			window.Show();
		}
	}
}
