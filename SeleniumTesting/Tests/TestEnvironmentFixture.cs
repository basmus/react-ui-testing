﻿using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Text;
using System.Threading;

using NUnit.Framework;

using SKBKontur.SeleniumTesting.Tests.Helpers;
using SKBKontur.SeleniumTesting.Tests.TestEnvironment;

namespace SKBKontur.SeleniumTesting.Tests
{
    [SetUpFixture]
    [SaveScreenshotOfFailure]
    public class TestEnvironmentFixture
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            if (TeamCityEnvironment.IsExecutionViaTeamCity)
            {
                KillWebPackDevServer();
                KillChromeDrivers();
                chromeDriverProcess = CreateChromeDriverProcess();
                chromeDriverProcess.Start();

                WaitResponse("http://localhost:9515/");

                webServerProcess = CreateWebServerProcess();
                webServerProcess.Start();

                WaitResponse("http://localhost:8083/");
            }

            BrowserSetUp.SetUp();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            BrowserSetUp.TearDown();
            if (TeamCityEnvironment.IsExecutionViaTeamCity)
            {
                KillProcessAndChildren(webServerProcess.Id);
                chromeDriverProcess.CloseMainWindow();
                chromeDriverProcess.WaitForExit(10000);
            }
        }

        private static string GetCommandLine(Process process)
        {
            try
            {
                var commandLine = new StringBuilder(process.MainModule.FileName);

                commandLine.Append(" ");
                using(var searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
                {
                    foreach(var @object in searcher.Get())
                    {
                        commandLine.Append(@object["CommandLine"]);
                        commandLine.Append(" ");
                    }
                }

                return commandLine.ToString();
            }
            catch
            {
                return "";
            }
        }

        private static void KillWebPackDevServer()
        {
            var processes = Process.GetProcesses();
            foreach(var process in processes)
            {
                if(GetCommandLine(process).Contains("8ae78075-b41d-4cb5-bda6-1de5c329f05f"))
                {
                    KillProcessAndChildren(process.Id);
                }
            }
        }

        private static void KillChromeDrivers()
        {
            var processes = Process.GetProcessesByName("chromedriver");
            foreach(var process in processes)
            {
                process.Kill();
            }
        }

        private static void WaitResponse(string url)
        {
            for(var i = 0; i < 2000; i++)
            {
                var httpResponse = (HttpWebRequest)WebRequest.CreateHttp(url);
                httpResponse.Timeout = (int)TimeSpan.FromHours(1).TotalMilliseconds;
                try
                {
                    using(var response = httpResponse.GetResponse())
                    {
                        using(var responseStream = response.GetResponseStream())
                        {
                            using(var responseStreamReader = new StreamReader(responseStream))
                            {
                                responseStreamReader.ReadToEnd();
                                return;
                            }
                        }
                    }
                }
                catch(WebException exception)
                {
                    if(exception.Response == null)
                    {
                        Thread.Sleep(2000);
                    }
                    else
                    {
                        using(var responseStream = exception.Response.GetResponseStream())
                        {
                            using(var responseStreamReader = new StreamReader(responseStream))
                            {
                                responseStreamReader.ReadToEnd();
                                return;
                            }
                        }
                    }
                }
                catch
                {
                    Thread.Sleep(2000);
                }
            }
            throw new Exception("Cannot wait response");
        }

        private static Process CreateWebServerProcess()
        {
            var processStartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = Path.Combine(PathUtils.FindProjectRootFolder(), "startTestPages.bat"),
                };

            return new Process {StartInfo = processStartInfo};
        }

        private static Process CreateChromeDriverProcess()
        {
            var chromeProcessStartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = Path.Combine(PathUtils.FindProjectRootFolder(), "Assemblies", "WebDriver", "chromedriver.exe"),
                };
            return new Process
                {
                    StartInfo = chromeProcessStartInfo
                };
        }

        private static void KillProcessAndChildren(int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach(ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch(ArgumentException)
            {
                /* process already exited */
            }
        }

        private Process chromeDriverProcess;
        private Process webServerProcess;
    }
}