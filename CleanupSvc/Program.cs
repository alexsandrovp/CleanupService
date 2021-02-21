using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace CleanupSvc
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main()
		{
			ServiceBase[] ServicesToRun;
			ServicesToRun = new ServiceBase[]
			{
				new Service()
			};
			ServiceBase.Run(ServicesToRun);
		}

		public static string PrefixForLongNames(this string path)
		{
			if (path == null) return null;
			path = path.Trim();
			if (path.Length == 0 || path.StartsWith(@"\\?\")) return path;
			return @"\\?\" + path;
		}

		public static bool IsDirectory(this FileSystemInfo fsi)
		{
			return (fsi.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
		}
	}
}
