using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Timers;

namespace CleanupSvc
{
    public partial class Service : ServiceBase
    {
        public static volatile bool Break = false;
        public static int FileMinimumAgeDays { get; private set; } = Config.Default.FileMinimumAgeDays;
        public static int DeleteOldFilesMinutes { get; private set; } = Config.Default.DeleteOldFilesMinutes;
        public static int RebuildCandidateListMinutes { get; private set; } = Config.Default.RebuildCandidateListMinutes;
        public static int RescanProfilesIntervalMinutes { get; private set; } = Config.Default.RescanProfilesIntervalMinutes;

        Timer RescanProfiles;
        Timer RebuildCandidateList;
        Timer DeleteOldFiles;

        public Service()
        {
            InitializeComponent();

            StringBuilder warning = new StringBuilder();
            if (FileMinimumAgeDays < 1)
            {
                warning.Append("FileMinimumAgeDays=1;");
                FileMinimumAgeDays = 1;
            }
            if (DeleteOldFilesMinutes < 60)
            {
                warning.Append("DeleteOldFilesMinutes=60;");
                DeleteOldFilesMinutes = 60;
            }
            if (RebuildCandidateListMinutes < 300)
            {
                warning.Append("RebuildCandidateListMinutes=300;");
                RebuildCandidateListMinutes = 300;
            }
            else if (RebuildCandidateListMinutes > 14400)
            {
                warning.Append("RebuildCandidateListMinutes=13399;");
                RebuildCandidateListMinutes = 13399;
            }
            if (RescanProfilesIntervalMinutes < 60)
            {
                warning.Append("RescanProfilesIntervalMinutes=60;");
                RescanProfilesIntervalMinutes = 60;
            }
            if (warning.Length > 0)
            {
                EventLogger.Warning("forcing " + warning.ToString());
            }

            RescanProfiles = new Timer(RescanProfilesIntervalMinutes * 60000);
            RebuildCandidateList = new Timer(RebuildCandidateListMinutes * 60000);
            DeleteOldFiles = new Timer(DeleteOldFilesMinutes * 60000);

            RescanProfiles.AutoReset = false;
            RescanProfiles.Elapsed += RescanProfiles_Elapsed;

            RebuildCandidateList.AutoReset = false;
            RebuildCandidateList.Elapsed += RebuildCandidateList_Elapsed;

            DeleteOldFiles.AutoReset = false;
            DeleteOldFiles.Elapsed += DeleteOldFiles_Elapsed;
        }

        protected override void OnStart(string[] args)
        {
#if DEBUG
            while (!Debugger.IsAttached) ;
#endif
            Break = false;

            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                Cleaner.Instance.RescanProfiles();
                Cleaner.Instance.RebuildCandidateList();
                Cleaner.Instance.DeleteOldFiles();

                DeleteOldFiles.Enabled = true;
                RescanProfiles.Enabled = true;
                RebuildCandidateList.Enabled = true;
            });
        }

        protected override void OnStop()
        {
            Break = true;
        }

        protected override void OnContinue()
        {
            Break = false;
            base.OnContinue();
        }

        protected override void OnPause()
        {
            Break = true;
            base.OnPause();
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            return base.OnPowerEvent(powerStatus);
        }

        protected override void OnShutdown()
        {
            Break = true;
            base.OnShutdown();
        }

        private void DeleteOldFiles_Elapsed(object sender, ElapsedEventArgs e)
        {
            DeleteOldFiles.Enabled = false;
            Cleaner.Instance.DeleteOldFiles();
            DeleteOldFiles.Enabled = true;
        }

        private void RebuildCandidateList_Elapsed(object sender, ElapsedEventArgs e)
        {
            RebuildCandidateList.Enabled = false;
            Cleaner.Instance.RebuildCandidateList();
            RebuildCandidateList.Enabled = true;
        }

        private void RescanProfiles_Elapsed(object sender, ElapsedEventArgs e)
        {
            RescanProfiles.Enabled = false;
            Cleaner.Instance.RescanProfiles();
            RescanProfiles.Enabled = true;
        }
    }
}
