﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CleanFlashCommon
{
    public class Uninstaller
    {
        private static readonly string[] PROCESSES_TO_KILL = new string[] {
            // Flash Center-related processes
            "fcbrowser", "fcbrowsermanager", "fclogin", "fctips", "flashcenter",
            "flashcenterservice", "flashcenteruninst", "flashplay", "update", "wow_helper",
            "dummy_cmd", "flashhelperservice",
            // Flash Player-related processes
            "flashplayerapp", "flashplayer_32_sa", "flashplayer_32_sa_debug",
            // Browsers that might be using Flash Player right now
            "opera", "iexplore", "chrome", "chromium", "brave", "vivaldi", "basilisk", "msedge",
            "seamonkey", "palemoon", "plugin-container"
        };

        public static void UninstallRegistry()
        {
            if (Environment.Is64BitOperatingSystem)
            {
                RegistryManager.ApplyRegistry(Properties.Resources.uninstallRegistry, Properties.Resources.uninstallRegistry64);
            }
            else
            {
                RegistryManager.ApplyRegistry(Properties.Resources.uninstallRegistry);
            }
        }

        public static void DeleteTask(string task)
        {
            ProcessRunner.RunUnmanagedProcess(
                new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/delete /tn \"" + task + "\" /f",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            );
        }

        public static void StopService(string service)
        {
            ProcessRunner.RunUnmanagedProcess(
                new ProcessStartInfo
                {
                    FileName = "net.exe",
                    Arguments = "stop \"" + service + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            );
        }

        public static void DeleteService(string service)
        {
            // First, stop the service.
            StopService(service);

            ProcessRunner.RunUnmanagedProcess(
                new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "delete \"" + service + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            );
        }

        public static void DeleteFlashCenter()
        {
            // Remove Flash Center from Program Files
            FileUtil.RecursiveDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FlashCenter"));

            if (Environment.Is64BitOperatingSystem)
            {
                // Remove Flash Center from Program Files (x86)
                FileUtil.RecursiveDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "FlashCenter"));
            }

            // Remove start menu shortcuts
            FileUtil.RecursiveDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "Flash Center"));

            // Remove Flash Center cache and user data
            FileUtil.RecursiveDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flash_Center"));

            // Remove shared start menu shortcuts
            FileUtil.RecursiveDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "Flash Center"));
            FileUtil.RecursiveDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Flash Center"));
            FileUtil.DeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Flash Player.lnk"));

            // Remove Desktop shortcut
            FileUtil.DeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Flash Center.lnk"));
            FileUtil.DeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Flash Player.lnk"));

            // Remove spyware dropped by Flash Center in the temporary folder   
            string tempFolder = Path.GetTempPath();

            foreach (string dir in Directory.GetDirectories(tempFolder))
            {
                string parentName = Path.GetFileName(dir);

                if (parentName.Length == 11 && parentName.EndsWith(".tmp"))
                {
                    FileUtil.RecursiveDelete(dir);
                }
            }

            // Remove Quick Launch shortcuts from Internet Explorer
            FileUtil.RecursiveDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Internet Explorer", "Quick Launch"), "Flash Center.lnk");
        }

        public static void DeleteFlashPlayer()
        {
            // Remove Macromedia folder from System32 and SysWOW64
            foreach (string dir in SystemInfo.GetMacromedPaths())
            {
                FileUtil.RecursiveDelete(dir);
            }

            // Remove Flash Player control panel applications
            foreach (string systemDir in SystemInfo.GetSystemPaths())
            {
                FileUtil.DeleteFile(Path.Combine(systemDir, "FlashPlayerApp.exe"));
                FileUtil.DeleteFile(Path.Combine(systemDir, "FlashPlayerCPLApp.cpl"));
            }
        }

        public static void StopProcesses()
        {
            // Stop all processes that might interfere with the install process
            List<Process> processes = Process.GetProcesses()
                .Where(process => PROCESSES_TO_KILL.Contains(process.ProcessName.ToLower()))
                .OrderBy(o => o.StartTime)
                .ToList();

            foreach (Process process in processes)
            {
                if (process.HasExited)
                {
                    // This process has already exited, no point to kill it
                    continue;
                }

                try
                {
                    process.Kill();
                    process.WaitForExit();
                }
                catch
                {
                    // Could not kill process...
                }
            }
        }

        public static void Uninstall(IProgressForm form)
        {
            // Uninstallation of Flash consists of the following steps:
            // 1. Delete all auto-updater tasks.
            // 2. Delete all Flash Player services.
            // 3. Delete all Flash Center services.
            // 4. Exit all browsers and other processes that may interfere with uninstallation.
            // 5. Remove all Flash Player references from the registry.
            // 6. Remove Flash Center files from the file system.
            // 7. Remove Flash Player files from the file system.

            form.UpdateProgressLabel("Stopping Flash auto-updater task...", true);
            DeleteTask("Adobe Flash Player Updater");
            form.UpdateProgressLabel("Stopping Flash auto-updater service...", true);
            DeleteService("AdobeFlashPlayerUpdateSvc");
            form.UpdateProgressLabel("Stopping Flash Center services...", true);
            DeleteService("Flash Helper Service");
            form.TickProgress();
            DeleteService("FlashCenterService");

            form.UpdateProgressLabel("Exiting all browsers...", true);
            StopProcesses();
            form.UpdateProgressLabel("Cleaning up registry...", true);
            UninstallRegistry();
            form.UpdateProgressLabel("Removing Flash Center...", true);
            DeleteFlashCenter();
            form.UpdateProgressLabel("Removing Flash Player...", true);
            DeleteFlashPlayer();
        }
    }
}
