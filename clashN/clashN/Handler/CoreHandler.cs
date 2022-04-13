﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using clashN.Mode;
using clashN.Resx;

namespace clashN.Handler
{
    /// <summary>
    /// core进程处理类
    /// </summary>
    class CoreHandler
    {
        private static string coreConfigRes = Global.coreConfigFileName;
        private CoreInfo coreInfo;
        private Process _process;
        Action<bool, string> _updateFunc;

        public CoreHandler(Action<bool, string> update)
        {
            _updateFunc = update;
        }

        /// <summary>
        /// 载入Core
        /// </summary>
        public void LoadCore(Config config)
        {
            if (Global.reloadCore)
            {
                var item = ConfigHandler.GetDefaultProfile(ref config);
                if (item == null)
                {
                    CoreStop();
                    ShowMsg(false, ResUI.CheckProfileSettings);
                    return;
                }

                if (item.enableTun && !Utils.IsAdministrator())
                {
                    ShowMsg(false, ResUI.EnableTunModeFailed);
                    return;
                }

                SetCore(config, item);
                string fileName = Utils.GetPath(coreConfigRes);
                if (CoreConfigHandler.GenerateClientConfig(item, fileName, false, out string msg) != 0)
                {
                    CoreStop();
                    ShowMsg(false, msg);
                }
                else
                {
                    ShowMsg(true, msg);
                    CoreRestart(item);
                }
            }
        }


        /// <summary>
        /// Core重启
        /// </summary>
        private void CoreRestart(ProfileItem item)
        {
            CoreStop();
            CoreStart(item);
        }

        /// <summary>
        /// Core停止
        /// </summary>
        public void CoreStop()
        {
            try
            {
                if (_process != null)
                {
                    KillProcess(_process);
                    _process.Dispose();
                    _process = null;
                }
                else
                {
                    if (coreInfo == null || coreInfo.coreExes == null)
                    {
                        return;
                    }

                    foreach (string vName in coreInfo.coreExes)
                    {
                        Process[] existing = Process.GetProcessesByName(vName);
                        foreach (Process p in existing)
                        {
                            string path = p.MainModule.FileName;
                            if (path == $"{Utils.GetPath(vName)}.exe")
                            {
                                KillProcess(p);
                            }
                        }
                    }
                }

                //bool blExist = true;
                //if (processId > 0)
                //{
                //    Process p1 = Process.GetProcessById(processId);
                //    if (p1 != null)
                //    {
                //        p1.Kill();
                //        blExist = false;
                //    }
                //}
                //if (blExist)
                //{
                //    foreach (string vName in lstCore)
                //    {
                //        Process[] killPro = Process.GetProcessesByName(vName);
                //        foreach (Process p in killPro)
                //        {
                //            p.Kill();
                //        }
                //    }
                //}
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }
        /// <summary>
        /// Core停止
        /// </summary>
        public void CoreStopPid(int pid)
        {
            try
            {
                Process _p = Process.GetProcessById(pid);
                KillProcess(_p);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private string FindCoreExe(List<string> lstCoreTemp)
        {
            string fileName = string.Empty;
            foreach (string name in lstCoreTemp)
            {
                string vName = string.Format("{0}.exe", name);
                vName = Utils.GetPath(vName);
                if (File.Exists(vName))
                {
                    fileName = vName;
                    break;
                }
            }
            if (Utils.IsNullOrEmpty(fileName))
            {
                string msg = string.Format(ResUI.NotFoundCore, coreInfo.coreUrl);
                ShowMsg(false, msg);
            }
            return fileName;
        }

        /// <summary>
        /// Core启动
        /// </summary>
        private void CoreStart(ProfileItem item)
        {
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString()));

            try
            {
                string fileName = FindCoreExe(coreInfo.coreExes);
                if (fileName == "") return;

                //Portable Mode
                var arguments = coreInfo.arguments;
                if (Directory.Exists(Utils.GetPath("data")))
                {
                    arguments += $" -d {Utils.GetPath("")}";
                }

                Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        WorkingDirectory = Utils.StartupPath(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    }
                };
                if (item.enableTun)
                {
                    p.StartInfo.Verb = "runas";
                }
                p.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        string msg = e.Data + Environment.NewLine;
                        ShowMsg(false, msg);
                    }
                });
                p.Start();
                p.PriorityClass = ProcessPriorityClass.High;
                p.BeginOutputReadLine();
                //processId = p.Id;
                _process = p;

                if (p.WaitForExit(1000))
                {
                    throw new Exception(p.StandardError.ReadToEnd());
                }

                Global.processJob.AddProcess(p.Handle);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                string msg = ex.Message;
                ShowMsg(true, msg);
            }
        }

        /// <summary>
        /// 委托
        /// </summary>
        /// <param name="updateToTrayTooltip">是否更新托盘图标的工具提示</param>
        /// <param name="msg">输出到日志框</param>
        private void ShowMsg(bool updateToTrayTooltip, string msg)
        {
            _updateFunc(updateToTrayTooltip, msg);
        }

        private void KillProcess(Process p)
        {
            try
            {
                p.CloseMainWindow();
                p.WaitForExit(100);
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(100);
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private int SetCore(Config config, ProfileItem item)
        {
            if (item == null)
            {
                return -1;
            }
            var coreType = LazyConfig.Instance.GetCoreType(item);


            coreInfo = LazyConfig.Instance.GetCoreInfo(coreType);

            if (coreInfo == null)
            {
                return -1;
            }
            return 0;
        }
    }
}