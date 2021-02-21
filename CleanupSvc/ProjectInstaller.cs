using System.Collections;
using System.ComponentModel;
using System.Diagnostics;

namespace CleanupSvc
{
	[RunInstaller(true)]
	public partial class ProjectInstaller : System.Configuration.Install.Installer
	{
		public ProjectInstaller()
		{
			InitializeComponent();
		}

		public override void Install(IDictionary stateSaver)
		{
			base.Install(stateSaver);
			if (!EventLog.SourceExists(EventLogger.eventSource))
				EventLog.CreateEventSource(EventLogger.eventSource, EventLogger.eventLog);
		}

		public override void Uninstall(IDictionary savedState)
		{
			base.Uninstall(savedState);
			if (EventLog.SourceExists(EventLogger.eventSource))
				EventLog.DeleteEventSource(EventLogger.eventSource);
		}
	}
}
