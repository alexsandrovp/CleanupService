using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CleanupSvc
{
    class Cleaner
    {
        const string profilekey = @"Software\Microsoft\Windows NT\CurrentVersion\ProfileList";

        object locker = new object();

        TimeSpan antique = new TimeSpan(Service.FileMinimumAgeDays, 0, 0, 0, 0);

        public static Cleaner Instance { get; } = new Cleaner();

        private Cleaner() { }

        private static bool mustDelete(DateTime now, FileSystemInfo item, TimeSpan antique)
        {
            var span0 = now - item.CreationTimeUtc;
            var span1 = now - item.LastAccessTimeUtc;
            var span2 = now - item.LastWriteTimeUtc;
            return span0 >= antique && span1 >= antique && span2 >= antique && item.Exists;
        }

        private static bool mustDeleteDirectory(DateTime now, FileSystemInfo item, TimeSpan antique)
        {
            var span0 = now - item.CreationTimeUtc;
			//var span1 = now - item.LastAccessTimeUtc;
            var span2 = now - item.LastWriteTimeUtc;
			//return span0 >= antique && span1 >= antique && span2 >= antique && item.Exists;
            return span0 >= antique && span2 >= antique && item.Exists;
        }

        private static void deleteDirectory(DateTime now, DirectoryInfo item, TimeSpan antique, StringBuilder problems)
        {
            if (Service.Break) return;

            if (mustDeleteDirectory(now, item, antique))
            {
                try { item.Delete(); return; }
                catch (IOException)
                {
                    //potentially not empty
                    foreach (var subitem in item.GetDirectories())
                        deleteDirectory(now, subitem, antique, problems);

                    item.Refresh();
                    if (mustDeleteDirectory(now, item, antique))
                        item.Delete();
                }
            }
        }

        public List<string> Profiles { get; } = new List<string>();
        public List<string> CandidateFiles { get; } = new List<string>();
        public List<string> CandidateDirectories { get; } = new List<string>();

        public void RescanProfiles()
        {
            lock (locker)
            {
                try
                {
                    if (Service.Break) return;

                    EventLogger.Info("rescanning for temporary paths");
                    using (var hhive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    {
                        using (var hkey = hhive.OpenSubKey(profilekey, false))
                        {
                            Profiles.Clear();
                            foreach (var subkey in hkey.GetSubKeyNames())
                            {
                                if (Service.Break) return;
                                using (var hsubkey = hkey.OpenSubKey(subkey, false))
                                {
                                    string path = hsubkey.GetValue("profileimagepath", null) as string;
                                    if (path == null) continue;
                                    DirectoryInfo di = null;
                                    try
                                    {
                                        di = new DirectoryInfo(path);
                                    }
                                    catch (PathTooLongException)
                                    {
                                        di = new DirectoryInfo(path.PrefixForLongNames());
                                    }
                                    if (!di.Exists) continue;
                                    try
                                    {
                                        di = new DirectoryInfo(Path.Combine(di.FullName, @"appdata\local\temp"));
                                    }
                                    catch (PathTooLongException)
                                    {
                                        di = new DirectoryInfo(Path.Combine(di.FullName, @"appdata\local\temp").PrefixForLongNames());
                                    }
                                    if (!di.Exists) continue;
                                    Profiles.Add(di.FullName);
                                }
                            }
                        }
                    }

                    string msg = string.Join("\n", Profiles);
                    EventLogger.Info("working with the following profiles:\n" + msg);
                }
                catch (Exception e)
                {
                    EventLogger.Error("unexpected exception (detecting profiles)", e);
                }
            }
        }

        //list files that are almost expired => antique - threshold
        public void RebuildCandidateList()
        {
            lock (locker)
            {
                try
                {
                    if (Service.Break) return;

                    EventLogger.Info("rebuilding list of old temp files");
                    long total_length = 0, candidate_length = 0;
                    CandidateFiles.Clear();
                    CandidateDirectories.Clear();
                    DateTime now = DateTime.UtcNow;
                    for (int i = 0; i < Profiles.Count; ++i)
                    {
                        if (Service.Break) return;

                        DirectoryInfo di = new DirectoryInfo(Profiles[i]);
                        if (!di.Exists) continue;

                        try
                        {
                            foreach (var item in di.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                            {
                                if (Service.Break) return;

                                try
                                {
                                    long item_length = 0;
                                    string name = item.FullName;
                                    FileSystemInfo temp = null;

                                    bool isDir = item.IsDirectory();
                                    if (isDir)
                                    {
                                        try { temp = new DirectoryInfo(name); }
                                        catch (PathTooLongException) { name = item.FullName.PrefixForLongNames(); }
                                    }
                                    else
                                    {
                                        try { item_length = new FileInfo(name).Length; }
                                        catch (PathTooLongException)
                                        {
                                            name = item.FullName.PrefixForLongNames();
                                            try { item_length = new FileInfo(name).Length; } catch { }
                                        }
                                    }

                                    if (temp != null && !temp.Exists)
                                    {
                                        //use it just to avoid compiler optimizations on that code
                                        EventLogger.Warning("item does not exist: " + temp.FullName);
                                    }

                                    total_length += item_length;

                                    var span0 = now - item.CreationTimeUtc;
                                    var span1 = now - item.LastAccessTimeUtc;
                                    var span2 = now - item.LastWriteTimeUtc;
                                    if (span0 >= antique &&
                                        span1 >= antique &&
                                        span2 >= antique)
                                    {
                                        if (isDir) CandidateDirectories.Add(name);
                                        else
                                        {
                                            candidate_length += item_length;
                                            CandidateFiles.Add(name);
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    EventLogger.Warning("exception when enumerating temporary files (item will be ignored)", e);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            EventLogger.Warning("exception when enumerating temporary folder (profile will be ignored)", e);
                        }
                    }

                    CandidateDirectories.Sort((x, y) => { return y.Length.CompareTo(x.Length); });

                    EventLogger.Info(
                        string.Format(
                            "can potentially save {0} of a total of {1} in temporary files",
                            ByteSuffixes.GetString(candidate_length),
                            ByteSuffixes.GetString(total_length)
                            ));
                }
                catch (Exception e)
                {
                    EventLogger.Error("unexpected exception (enumerating)", e);
                }
            }
        }

        public void DeleteOldFiles()
        {
            lock (locker)
            {
                StringBuilder problems = new StringBuilder(1048576);

                try
                {
                    if (Service.Break) return;

                    EventLogger.Info("deleting old temp files");
                    if (CandidateFiles.Count == 0 && CandidateDirectories.Count == 0)
                    {
                        EventLogger.Info("nothing to delete");
                        return;
                    }

                    long saved = 0;
                    long skipped = 0;
                    long vanished = 0;
                    DateTime now = DateTime.UtcNow;
                    var list = CandidateFiles.ToArray();

                    foreach (var item in list)
                    {
                        if (Service.Break) return;
                        var file = new FileInfo(item);
                        if (file.Exists)
                        {
                            long itemLen = file.Length;
                            try
                            {
                                if (mustDelete(now, file, antique))
                                    file.Delete();
                                else skipped += file.Length;
                            }
                            catch (Exception e)
                            {
                                problems.AppendFormat("\nexception: {0}\t{1}", e.Message, item);
                                if (!(e is IOException))
                                    EventLogger.Warning("exception when deleting file", e);
                            }

                            try
                            {
                                file.Refresh();
                                if (!file.Exists)
                                {
                                    saved += itemLen;
                                    CandidateFiles.Remove(item);
                                }
                            }
                            catch (Exception e) { problems.AppendFormat("\nexception: {0}\t{1}", e.Message, item); }
                        }
                        else
                        {
                            CandidateFiles.Remove(item);
                            vanished += file.Length;
                        }
                    }

                    list = CandidateDirectories.ToArray();
                    foreach (var item in list)
                    {
                        if (Service.Break) return;
                        var dir = new DirectoryInfo(item);
                        if (dir.Exists)
                        {
							/*bool isempty = !Directory.EnumerateFileSystemEntries(item.FullName).Any()*/
                            try { deleteDirectory(now, dir, antique, problems); }
                            catch (Exception e)
                            {
                                problems.AppendFormat("\nexception: {0}\t{1}", e.Message, item);
                                if (!(e is IOException))
                                    EventLogger.Warning("exception when deleting directory", e);
                            }

                            try
                            {
                                dir.Refresh();
                                if (!dir.Exists) CandidateDirectories.Remove(item);
                            }
                            catch (Exception e)
                            {
                                problems.AppendFormat("\nexception: {0}\t{1}", e.Message, item);
                            }
                        }
                        else CandidateDirectories.Remove(item);
                    }

                    EventLogger.Info(string.Format("saved {0} bytes of disk space ({1})", saved, ByteSuffixes.GetString(saved)));
                    EventLogger.Info(string.Format("skipped {0} bytes of temp files ({1}) that were used since the list was compiled", skipped, ByteSuffixes.GetString(skipped)));
                    EventLogger.Info(string.Format("skipped {0} bytes of temp files ({1}) that were already deleted", vanished, ByteSuffixes.GetString(vanished)));
                }
                catch (Exception e)
                {
                    EventLogger.Error("unexpected exception (deleting)", e);
                }

                string s = problems.ToString();
                if (s.Length > 0) EventLogger.Warning("problems that happened while deleting:\n" + s);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
