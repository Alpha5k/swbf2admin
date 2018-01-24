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
using SWBF2Admin.Utility;
using System;

namespace SWBF2Admin.Scheduler
{
    public class SchedulerTask
    {
        public delegate void TaskDelegate();
        private TaskDelegate task;

        public SchedulerTask(TaskDelegate task)
        {
            this.task = task;
        }

        public void Run()
        {
            try
            {
                task.Invoke();
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Verbose, "Failed to run task {0}::{1} ({2})", task.Target.ToString(), task.Method.Name, e.Message);
            }
        }
    }
}