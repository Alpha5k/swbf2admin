﻿/*
 * This file is part of SWBF2Admin (https://github.com/jweigelt/swbf2admin). 
 * Copyright(C) 2017, 2018  Jan Weigelt <jan@lekeks.de>
 *
 * SWBF2Admin is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.

 * SWBF2Admin is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
 * GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
 * along with SWBF2Admin. If not, see<http://www.gnu.org/licenses/>.
 */
using System;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;

using SWBF2Admin.Utility;
using SWBF2Admin.Config;
using System.Diagnostics;

namespace SWBF2Admin.Gameserver
{
    public class IngameServerController : ComponentBase
    {
        private const int OFFSET_MAP_STATUS = (0x01EAFCA0 - 0x00401000 + 0x1000);
        private const int OFFSET_MAP_FREEZE = (0x01E64EFF - 0x00401000 + 0x1000);
        private const int OFFSET_NORENDER = (0x01EAD47B - 0x00401000 + 0x1000);

        private const byte NET_COMMAND_RDP_OPEN = 0x01;
        private const byte NET_COMMAND_RDP_CLOSE = 0x02;

        [Flags]
        private enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }

        private bool isLoading = false;     //map load in progress
        private bool steamMode;             //steam mode enabled?
        private int notRespondingCount = 0; //times the server process didn't espond
        private int mapHangTime = 0;        //time since game ended
        private int freezeCount = 0;        //times we tried to freeze-unfreeze

        private IngameServerControllerConfiguration config;

        private IntPtr moduleBase;
        private IntPtr procHandle = IntPtr.Zero;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        public IngameServerController(AdminCore core) : base(core) { }

        public override void OnInit()
        {
            if (steamMode)
            {
                UpdateInterval = config.ReadTimeout;
                Core.Server.SteamServerStarting += Server_SteamServerStarting;
                Core.Scheduler.PushRepeatingTask(() => CheckResponding(), config.NotRespondingCheckInterval);
            }
        }
        public override void OnServerStart(EventArgs e)
        {
            if (steamMode)
            {
                if (((StartEventArgs)e).Attached) EnableUpdates();

                try
                {
                    MemoryInit();
                }
                catch
                {
                    MemoryClose();
                    Logger.Log(LogLevel.Warning, "IngameServerController failed to attach. Server won't be supported.");
                }
            }
        }
        public override void OnServerStop()
        {
            if (steamMode)
            {
                DisableUpdates();
                MemoryClose();
            }
        }
        protected override void OnUpdate()
        {
            try
            {
                CheckMapStatus();
            }
            catch
            {
                //TODO
            }
        }

        public override void Configure(CoreConfiguration config)
        {
            steamMode = config.EnableSteamMode;
            this.config = Core.Files.ReadConfig<IngameServerControllerConfiguration>();

            //TODO: clean that up:
            //calling getter once so any format errors are thrown now (during config) and not during runtime
            IPEndPoint ipep = this.config.ServerIPEP;
        }

        public void Server_SteamServerStarting(object sender, EventArgs e)
        {
            //request RD session for startup
            DisableUpdates();
            isLoading = true;
            SendCommand(NET_COMMAND_RDP_OPEN);
            Core.Scheduler.PushDelayedTask(() => EnableUpdates(), config.StartupTime);
        }

        private void SetNoRender(bool norender)
        {
            WriteByte(OFFSET_NORENDER, (byte)(norender ? 1 : 0));
        }

        private void SetFreeze(bool freeze)
        {
            WriteByte(OFFSET_MAP_FREEZE, (byte)(freeze ? 0 : 1));
        }
        private byte ReadMapStatus()
        {
            return ReadByte(OFFSET_MAP_STATUS);
        }
        private byte ReadByte(int offset)
        {
            IntPtr bytesRead;
            byte[] buf = new byte[1];
            if (!ReadProcessMemory(procHandle, IntPtr.Add(moduleBase, offset), buf, 1, out bytesRead) || bytesRead == IntPtr.Zero)
                throw new Exception("ReadProcessMemory() failed");
            return buf[0];
        }
        private void WriteByte(int offset, byte value)
        {
            UIntPtr bytesWritten;
            byte[] buf = new byte[] { value };
            if (!WriteProcessMemory(procHandle, IntPtr.Add(moduleBase, offset), buf, 1, out bytesWritten) || bytesWritten == UIntPtr.Zero)
                throw new Exception("WriteProcessMemory() failed");
        }
        private void MemoryInit()
        {
            Process p = Core.Server.ServerProcess;
            if (p != null)
            {
                Logger.Log(LogLevel.Verbose, "Attaching controller to GoG listenserver");
                procHandle = OpenProcess(ProcessAccessFlags.All, false, p.Id);
                if (procHandle == IntPtr.Zero)
                {
                    Logger.Log(LogLevel.Error, "OpenProcess() failed on serverprocess (id: {0})", p.Id.ToString());
                    throw new Exception("OpenProcess() failed.");
                }

                moduleBase = p.MainModule.BaseAddress;
            }
        }
        private void MemoryClose()
        {
            if (procHandle != IntPtr.Zero)
            {
                Logger.Log(LogLevel.Verbose, "Closing handle {0}", procHandle.ToString());
                CloseHandle(procHandle);
            }
        }

        private void SendCommand(byte command)
        {
            Logger.Log(LogLevel.Verbose, "Sending controller command: {0}", command.ToString());
            try
            {
                //just using a very primitive single-threaded client which re-connects every time
                //as events requiring net interaction are rather rare, there's no point in keeping a connection alive
                using (TcpClient client = new TcpClient())
                {
                    //use strict timeouts so we don't block the main thread for too long if something goes wrong
                    client.ReceiveTimeout = config.TcpTimeout;
                    client.SendTimeout = config.TcpTimeout;

                    client.Connect(config.ServerIPEP);
                    using (BinaryWriter writer = new BinaryWriter(client.GetStream()))
                    {
                        writer.Write(command);
                        writer.Flush();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Warning, "SendCommand() failed ({0})", e.Message);
            }

        }

        private void CheckResponding()
        {
            //check if the process is stuck and "crash" it manually if necessary
            Process p = Core.Server.ServerProcess;
            if (p != null && !p.HasExited)
            {
                if (!p.Responding)
                {
                    if (notRespondingCount++ >= config.NotRespondingMaxCount)
                        p.Kill();
                }
                else notRespondingCount = 0;
            }
        }
        private void CheckMapStatus()
        {
            if (ReadMapStatus() != 0)
            {
                if (!isLoading)
                {
                    SetNoRender(false);
                    SendCommand(NET_COMMAND_RDP_OPEN);
                }
                isLoading = true;
                mapHangTime += UpdateInterval;
            }
            else
            {
                mapHangTime = 0;
                freezeCount = 0;
                if (isLoading)
                {
                    SetNoRender(true);
                    isLoading = false;
                    SendCommand(NET_COMMAND_RDP_CLOSE);
                }
            }

            if (mapHangTime > config.MapHangTimeout && freezeCount < config.FreezesBeforeKill)
            {
                Logger.Log(LogLevel.Info, "Server seems to be stuck - trying to freeze-unfreeze it...");
                freezeCount++;
                mapHangTime = 0;
                TryFreezeUnfreeze();
            }
            else if (freezeCount >= config.FreezesBeforeKill)
            {
                Logger.Log(LogLevel.Info, "Server doesn't seem to resume. Shutting it down.");
                Core.Server.ServerProcess.Kill(); //"crash" the server so ServerManager will restart it
                freezeCount = 0;
            }
        }
        private void TryFreezeUnfreeze()
        {

            SetFreeze(true);
            Core.Scheduler.PushDelayedTask(() => SetFreeze(false), config.FreezeTime);
        }

        ~IngameServerController()
        {
            OnServerStop(); //make sure we close any open handle
        }
    }
}