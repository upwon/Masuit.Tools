﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Masuit.Tools.Hardware
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="s"></param>
    public delegate void OnLogicalDiskProc(string s);

    /// <summary>
    /// 硬件信息，部分功能需要C++支持
    /// </summary>
    public static partial class SystemInfo
    {
        #region 字段

        private const int GwHwndfirst = 0;
        private const int GwHwndnext = 2;
        private const int GwlStyle = -16;
        private const int WsVisible = 268435456;
        private const int WsBorder = 8388608;
        private static readonly PerformanceCounter PcCpuLoad; //CPU计数器 

        private static readonly PerformanceCounter MemoryCounter = new PerformanceCounter();
        private static readonly PerformanceCounter CpuCounter = new PerformanceCounter();
        private static readonly PerformanceCounter DiskReadCounter = new PerformanceCounter();
        private static readonly PerformanceCounter DiskWriteCounter = new PerformanceCounter();

        private static readonly string[] InstanceNames;
        private static readonly PerformanceCounter[] NetRecvCounters;
        private static readonly PerformanceCounter[] NetSentCounters;

        #endregion

        #region 构造函数 

        /// <summary>
        /// 静态构造函数
        /// </summary>
        static SystemInfo()
        {
            //初始化CPU计数器 
            PcCpuLoad = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            PcCpuLoad.MachineName = ".";
            PcCpuLoad.NextValue();

            //CPU个数 
            ProcessorCount = Environment.ProcessorCount;

            //获得物理内存 
            ManagementClass mc = new ManagementClass("Win32_ComputerSystem");
            ManagementObjectCollection moc = mc.GetInstances();
            foreach (ManagementBaseObject mo in moc)
            {
                if (mo["TotalPhysicalMemory"] != null)
                {
                    PhysicalMemory = long.Parse(mo["TotalPhysicalMemory"].ToString());
                }
            }

            PerformanceCounterCategory cat = new PerformanceCounterCategory("Network Interface");
            InstanceNames = cat.GetInstanceNames();
            NetRecvCounters = new PerformanceCounter[InstanceNames.Length];
            for (int i = 0; i < InstanceNames.Length; i++) NetRecvCounters[i] = new PerformanceCounter();

            NetSentCounters = new PerformanceCounter[InstanceNames.Length];
            for (int i = 0; i < InstanceNames.Length; i++) NetSentCounters[i] = new PerformanceCounter();

            CompactFormat = false;
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        public static bool CompactFormat { get; set; }

        #region CPU核心 

        /// <summary>
        /// 获取CPU核心数 
        /// </summary>
        public static int ProcessorCount { get; }

        #endregion

        #region CPU占用率 

        /// <summary>
        /// 获取CPU占用率 %
        /// </summary>
        public static float CpuLoad => PcCpuLoad.NextValue();

        #endregion

        #region 可用内存 

        /// <summary>
        /// 获取可用内存
        /// </summary>
        public static long MemoryAvailable
        {
            get
            {
                long availablebytes = 0;
                ManagementClass mos = new ManagementClass("Win32_OperatingSystem");
                foreach (var o in mos.GetInstances())
                {
                    var mo = (ManagementObject)o;
                    if (mo["FreePhysicalMemory"] != null) availablebytes = 1024 * long.Parse(mo["FreePhysicalMemory"].ToString());
                }
                return availablebytes;
            }
        }

        #endregion

        #region 物理内存 

        /// <summary>
        /// 获取物理内存
        /// </summary>
        public static long PhysicalMemory { get; }

        #endregion

        #region 查找所有应用程序标题 

        /// <summary>
        /// 查找所有应用程序标题 
        /// </summary>
        /// <param name="handle">应用程序标题范型</param>
        /// <returns>所有应用程序集合</returns>
        public static ArrayList FindAllApps(int handle)
        {
            ArrayList apps = new ArrayList();

            int hwCurr = GetWindow(handle, GwHwndfirst);

            while (hwCurr > 0)
            {
                int IsTask = WsVisible | WsBorder;
                int lngStyle = GetWindowLongA(hwCurr, GwlStyle);
                bool taskWindow = (lngStyle & IsTask) == IsTask;
                if (taskWindow)
                {
                    int length = GetWindowTextLength(new IntPtr(hwCurr));
                    StringBuilder sb = new StringBuilder(2 * length + 1);
                    GetWindowText(hwCurr, sb, sb.Capacity);
                    string strTitle = sb.ToString();
                    if (!string.IsNullOrEmpty(strTitle)) apps.Add(strTitle);
                }
                hwCurr = GetWindow(hwCurr, GwHwndnext);
            }

            return apps;
        }

        #endregion

        #region 获取CPU的数量

        /// <summary>
        /// 获取CPU的数量
        /// </summary>
        /// <returns>CPU的数量</returns>
        public static int GetCpuCount()
        {
            ManagementClass m = new ManagementClass("Win32_Processor");
            ManagementObjectCollection mn = m.GetInstances();
            return mn.Count;
        }

        #endregion

        #region 获取CPU信息

        /// <summary>
        /// 获取CPU信息
        /// </summary>
        /// <returns>CPU信息</returns>
        public static List<CpuInfo> GetCpuInfo()
        {
            List<CpuInfo> list = new List<CpuInfo>();
            ManagementObjectSearcher mySearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (var o in mySearcher.Get())
            {
                var myObject = (ManagementObject)o;
                list.Add(new CpuInfo
                {
                    CpuLoad = CpuLoad,
                    NumberOfLogicalProcessors = ProcessorCount,
                    CurrentClockSpeed = myObject.Properties["CurrentClockSpeed"].Value.ToString(),
                    Manufacturer = myObject.Properties["Manufacturer"].Value.ToString(),
                    MaxClockSpeed = myObject.Properties["MaxClockSpeed"].Value.ToString(),
                    Type = myObject.Properties["Name"].Value.ToString(),
                    DataWidth = myObject.Properties["DataWidth"].Value.ToString(),
                    DeviceID = myObject.Properties["DeviceID"].Value.ToString(),
                    NumberOfCores = Convert.ToInt32(myObject.Properties["NumberOfCores"].Value),
                    Temperature = GetCPUTemperature()
                });
            }

            return list;
        }

        #endregion

        #region 获取内存信息

        /// <summary>
        /// 获取内存信息
        /// </summary>
        /// <returns>内存信息</returns>
        public static RamInfo GetRamInfo() => new RamInfo
        {
            MemoryAvailable = GetFreePhysicalMemory(),
            PhysicalMemory = GetTotalPhysicalMemory(),
            TotalPageFile = GetTotalVirtualMemory(),
            AvailablePageFile = GetTotalVirtualMemory() - GetUsedVirtualMemory(),
            AvailableVirtual = 1 - GetUsageVirtualMemory(),
            TotalVirtual = 1 - GetUsedPhysicalMemory()
        };

        #endregion

        #region 获取CPU温度

        /// <summary>
        /// 获取CPU温度
        /// </summary>
        /// <returns>CPU温度</returns>
        public static double GetCPUTemperature()
        {
            string str = "";

            ManagementObjectSearcher vManagementObjectSearcher = new ManagementObjectSearcher(@"root\WMI", @"select * from MSAcpi_ThermalZoneTemperature");

            foreach (ManagementObject managementObject in vManagementObjectSearcher.Get())
            {
                str += managementObject.Properties["CurrentTemperature"].Value.ToString();
            }

            //这就是CPU的温度了
            double temp = (double.Parse(str) - 2732) / 10;
            return Math.Round(temp, 2);
        }

        #endregion

        #region WMI接口获取CPU使用率

        /// <summary>
        /// WMI接口获取CPU使用率
        /// </summary>
        /// <returns></returns>
        public static string GetProcessorData()
        {
            double d = GetCounterValue(CpuCounter, "Processor", "% Processor Time", "_Total");
            return CompactFormat ? (int)d + "%" : d.ToString("F") + "%";
        }

        #endregion

        #region 获取虚拟内存使用率详情

        /// <summary>
        /// 获取虚拟内存使用率详情
        /// </summary>
        /// <returns></returns>
        public static string GetMemoryVData()
        {
            string str;
            double d = GetCounterValue(MemoryCounter, "Memory", "% Committed Bytes In Use", null);
            str = d.ToString("F") + "% (";

            d = GetCounterValue(MemoryCounter, "Memory", "Committed Bytes", null);
            str += FormatBytes(d) + " / ";

            d = GetCounterValue(MemoryCounter, "Memory", "Commit Limit", null);
            return str + FormatBytes(d) + ") ";
        }

        /// <summary>
        /// 获取虚拟内存使用率
        /// </summary>
        /// <returns></returns>
        public static double GetUsageVirtualMemory()
        {
            return GetCounterValue(MemoryCounter, "Memory", "% Committed Bytes In Use", null);
        }

        /// <summary>
        /// 获取虚拟内存已用大小
        /// </summary>
        /// <returns></returns>
        public static double GetUsedVirtualMemory()
        {
            return GetCounterValue(MemoryCounter, "Memory", "Committed Bytes", null);
        }
        /// <summary>
        /// 获取虚拟内存总大小
        /// </summary>
        /// <returns></returns>
        public static double GetTotalVirtualMemory()
        {
            return GetCounterValue(MemoryCounter, "Memory", "Commit Limit", null);
        }

        #endregion

        #region 获取物理内存使用率详情

        /// <summary>
        /// 获取物理内存使用率详情描述
        /// </summary>
        /// <returns></returns>
        public static string GetMemoryPData()
        {
            string s = QueryComputerSystem("totalphysicalmemory");
            double totalphysicalmemory = Convert.ToDouble(s);

            double d = GetCounterValue(MemoryCounter, "Memory", "Available Bytes", null);
            d = totalphysicalmemory - d;

            s = CompactFormat ? "%" : "% (" + FormatBytes(d) + " / " + FormatBytes(totalphysicalmemory) + ")";
            d /= totalphysicalmemory;
            d *= 100;
            return CompactFormat ? (int)d + s : d.ToString("F") + s;
        }

        /// <summary>
        /// 获取物理内存总数，单位B
        /// </summary>
        /// <returns></returns>
        public static double GetTotalPhysicalMemory()
        {
            string s = QueryComputerSystem("totalphysicalmemory");
            return Convert.ToDouble(s);
        }

        /// <summary>
        /// 获取空闲的物理内存数，单位B
        /// </summary>
        /// <returns></returns>
        public static double GetFreePhysicalMemory()
        {
            return GetCounterValue(MemoryCounter, "Memory", "Available Bytes", null);
        }

        /// <summary>
        /// 获取已经使用了的物理内存数，单位B
        /// </summary>
        /// <returns></returns>
        public static double GetUsedPhysicalMemory()
        {
            return GetTotalPhysicalMemory() - GetFreePhysicalMemory();
        }

        #endregion

        #region 获取硬盘的读写速率

        /// <summary>
        /// 获取硬盘的读写速率
        /// </summary>
        /// <param name="dd">读或写</param>
        /// <returns></returns>
        public static double GetDiskData(DiskData dd) => dd == DiskData.Read ? GetCounterValue(DiskReadCounter, "PhysicalDisk", "Disk Read Bytes/sec", "_Total") : dd == DiskData.Write ? GetCounterValue(DiskWriteCounter, "PhysicalDisk", "Disk Write Bytes/sec", "_Total") : dd == DiskData.ReadAndWrite ? GetCounterValue(DiskReadCounter, "PhysicalDisk", "Disk Read Bytes/sec", "_Total") + GetCounterValue(DiskWriteCounter, "PhysicalDisk", "Disk Write Bytes/sec", "_Total") : 0;

        #endregion

        #region 获取网络的传输速率

        /// <summary>
        /// 获取网络的传输速率
        /// </summary>
        /// <param name="nd">上传或下载</param>
        /// <returns></returns>
        public static double GetNetData(NetData nd)
        {
            if (InstanceNames.Length == 0) return 0;

            double d = 0;
            for (int i = 0; i < InstanceNames.Length; i++) d += nd == NetData.Received ? GetCounterValue(NetRecvCounters[i], "Network Interface", "Bytes Received/sec", InstanceNames[i]) : nd == NetData.Sent ? GetCounterValue(NetSentCounters[i], "Network Interface", "Bytes Sent/sec", InstanceNames[i]) : nd == NetData.ReceivedAndSent ? GetCounterValue(NetRecvCounters[i], "Network Interface", "Bytes Received/sec", InstanceNames[i]) + GetCounterValue(NetSentCounters[i], "Network Interface", "Bytes Sent/sec", InstanceNames[i]) : 0;

            return d;
        }

        #endregion

        /// <summary>
        /// 获取网卡硬件地址
        /// </summary>
        /// <returns></returns>
        public static IList<string> GetMacAddress()
        {
            //获取网卡硬件地址       
            IList<string> list = new List<string>();
            ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
            using (mc)
            {
                ManagementObjectCollection moc = mc.GetInstances();
                using (moc)
                {
                    foreach (ManagementObject mo in moc)
                    {
                        if ((bool)mo["IPEnabled"])
                        {
                            list.Add(mo["MacAddress"].ToString());
                        }
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 获取IP地址 
        /// </summary>
        /// <returns></returns>
        public static IList<string> GetIPAddress()
        {
            //获取IP地址        
            IList<string> list = new List<string>();
            var st = "";
            ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
            using (mc)
            {
                ManagementObjectCollection moc = mc.GetInstances();
                using (moc)
                {
                    foreach (ManagementObject mo in moc)
                    {
                        if ((bool)mo["IPEnabled"])
                        {
                            st = mo["IpAddress"].ToString();
                            var ar = (Array)(mo.Properties["IpAddress"].Value);
                            st = ar.GetValue(0).ToString();
                            list.Add(st);
                        }
                    }
                    return list;
                }
            }
        }
        /// <summary>
        /// 获取操作系统版本
        /// </summary>
        /// <returns></returns>
        public static string GetOsVersion()
        {
            return Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")?.GetValue("ProductName").ToString();
        }
        #region 将速度值格式化成字节单位

        /// <summary>
        /// 将速度值格式化成字节单位
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string FormatBytes(this double bytes)
        {
            int unit = 0;
            while (bytes > 1024)
            {
                bytes /= 1024;
                ++unit;
            }
            string s = CompactFormat ? ((int)bytes).ToString() : bytes.ToString("F") + " ";
            return s + (Unit)unit;
        }

        #endregion

        #region 查询计算机系统信息

        /// <summary>
        /// 查询计算机系统信息
        /// </summary>
        /// <param name="type">类型名</param>
        /// <returns></returns>
        public static string QueryComputerSystem(string type)
        {
            string str = null;
            ManagementObjectSearcher objCS = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (ManagementObject objMgmt in objCS.Get()) str = objMgmt[type].ToString();
            return str;
        }

        #endregion

        #region 获取环境变量

        /// <summary>
        /// 获取环境变量
        /// </summary>
        /// <param name="type">环境变量名</param>
        /// <returns></returns>
        public static string QueryEnvironment(string type) => Environment.ExpandEnvironmentVariables(type);

        #endregion

        #region 获取磁盘空间

        /// <summary>
        /// 获取磁盘可用空间
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> DiskFree()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            ManagementObjectSearcher objCS = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk");
            foreach (ManagementObject objMgmt in objCS.Get())
            {
                var device = objMgmt["DeviceID"];
                if (null != device)
                {
                    var space = objMgmt["FreeSpace"];
                    if (null != space)
                    {
                        dic.Add(device.ToString(), FormatBytes(double.Parse(space.ToString())));
                    }
                }
            }
            return dic;
        }

        /// <summary>
        /// 获取磁盘总空间
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> DiskTotalSpace()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            ManagementObjectSearcher objCS = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk");
            foreach (ManagementObject objMgmt in objCS.Get())
            {
                var device = objMgmt["DeviceID"];
                if (null != device)
                {
                    var space = objMgmt["Size"];
                    if (null != space)
                    {
                        dic.Add(device.ToString(), FormatBytes(double.Parse(space.ToString())));
                    }
                }
            }
            return dic;
        }


        /// <summary>
        /// 获取磁盘已用空间
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> DiskUsedSpace()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            ManagementObjectSearcher objCS = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk");
            foreach (ManagementObject objMgmt in objCS.Get())
            {
                var device = objMgmt["DeviceID"];
                if (null != device)
                {
                    var free = objMgmt["FreeSpace"];
                    var total = objMgmt["Size"];
                    if (null != total)
                    {
                        dic.Add(device.ToString(), FormatBytes(double.Parse(total.ToString()) - free.ToString().ToDouble()));
                    }
                }
            }
            return dic;
        }

        /// <summary>
        /// 获取磁盘使用率
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, double> DiskUsage()
        {
            Dictionary<string, double> dic = new Dictionary<string, double>();
            ManagementObjectSearcher objCS = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk");
            foreach (ManagementObject objMgmt in objCS.Get())
            {
                var device = objMgmt["DeviceID"];
                if (null != device)
                {
                    var free = objMgmt["FreeSpace"];
                    var total = objMgmt["Size"];
                    if (null != total && total.ToString().ToDouble() > 0)
                    {
                        dic.Add(device.ToString(), 1 - free.ToString().ToDouble() / total.ToString().ToDouble());
                    }
                }
            }
            return dic;
        }

        #endregion

        private static double GetCounterValue(PerformanceCounter pc, string categoryName, string counterName, string instanceName)
        {
            pc.CategoryName = categoryName;
            pc.CounterName = counterName;
            pc.InstanceName = instanceName;
            return pc.NextValue();
        }

        #region Win32API声明 

#pragma warning disable 1591
        [DllImport("kernel32")]
        public static extern void GetWindowsDirectory(StringBuilder winDir, int count);

        [DllImport("kernel32")]
        public static extern void GetSystemDirectory(StringBuilder sysDir, int count);

        [DllImport("kernel32")]
        public static extern void GetSystemInfo(ref CPU_INFO cpuinfo);

        [DllImport("kernel32")]
        public static extern void GlobalMemoryStatus(ref MemoryInfo meminfo);

        [DllImport("kernel32")]
        public static extern void GetSystemTime(ref SystemtimeInfo stinfo);

        [DllImport("IpHlpApi.dll")]
        public static extern uint GetIfTable(byte[] pIfTable, ref uint pdwSize, bool bOrder);

        [DllImport("User32")]
        // ReSharper disable once MissingXmlDoc
        public static extern int GetWindow(int hWnd, int wCmd);

        [DllImport("User32")]
        public static extern int GetWindowLongA(int hWnd, int wIndx);

        [DllImport("user32.dll")]
        public static extern bool GetWindowText(int hWnd, StringBuilder title, int maxBufSize);

        [DllImport("user32", CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(IntPtr hWnd);
#pragma warning restore 1591

        #endregion
    }
}