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
using System.Net;
using System.Collections.Generic;

using SWBF2Admin.Structures;
using System.Threading;
using System;
using SWBF2Admin.Gameserver;

namespace SWBF2Admin.Web.Pages
{
    class MapSettingsPage : AjaxPage
    {

        Mutex sRMtx = new Mutex();
        class MapApiParams : ApiRequestParams
        {
            public List<string> Maps { get; set; }
            public bool Randomize { get; set; }
        }

        class MapSaveResponse
        {
            public bool Ok { get; set; }
            public string Error { get; set; }
            public MapSaveResponse(Exception e)
            {
                Ok = false;
                Error = e.Message;
            }
            public MapSaveResponse()
            {
                Ok = true;
            }
        }

        class MapRotResponse
        {
            public bool Ok { get; set; }
            public List<string> Maps { get; set; }
            public bool Randomize { get; set; }
            public string Error { get; set; }

            public MapRotResponse(List<string> maps, bool randomize)
            {
                Ok = true;
                Maps = maps;
                Randomize = randomize;
                Error = string.Empty;
            }

            public MapRotResponse(Exception e)
            {
                Ok = false;
                Maps = null;
                Error = e.Message;
            }
        }

        public MapSettingsPage(AdminCore core) : base(core, "/settings/maps", "maps.htm") { }

        public override void HandleGet(HttpListenerContext ctx, WebUser user)
        {
            ReturnTemplate(ctx);
        }

        public override void HandlePost(HttpListenerContext ctx, WebUser user, string postData)
        {
            MapApiParams p = null;
            if ((p = TryJsonParse<MapApiParams>(ctx, postData)) == null) return;

            switch (p.Action)
            {
                case "maps_installed":
                    List<ServerMap> mapList = Core.Database.GetMaps();
                    WebAdmin.SendHtml(ctx, ToJson(mapList));
                    break;

                case "maps_save":
                    WebServer.LogAudit(user, "modified the map rotation");
                    WebAdmin.SendHtml(ctx, ToJson(SaveMapRot(p)));
                    break;

                case "maps_rotation":
                    WebAdmin.SendHtml(ctx, ToJson(GetMapRotation()));
                    break;
            }
        }

        private MapSaveResponse SaveMapRot(MapApiParams p)
        {
            List<string> mapRot = p.Maps;
            MapSaveResponse r;
            sRMtx.WaitOne();
            try
            {
                ServerMap.SaveMapRotation(Core, mapRot);

                if (Core.Config.EnableRuntime && Core.Server.Status == ServerStatus.Online)
                {
                    Core.Scheduler.PushTask(() => Core.Rcon.UpdateMapList(mapRot));
                }

                //there's a good chance that only maps were updated
                //in this case we dont want to re-write ServerSettings.cfg -> check if Randomize changed
                if (Core.Server.Settings.Randomize != p.Randomize)
                {
                    Core.Server.Settings.Randomize = p.Randomize;
                    Core.Server.Settings.WriteToFile(Core);
                    if (Core.Config.EnableRuntime && Core.Server.Status == ServerStatus.Online)
                    {
                        Core.Scheduler.PushTask(() => Core.Rcon.SendCommand("randomize", p.Randomize ? "1" : "0")); 
                    }
                }
                r = new MapSaveResponse();
            }
            catch (Exception e)
            {
                r = new MapSaveResponse(e);
            }
            finally
            {
                sRMtx.ReleaseMutex();
            }
            return r;
        }

        private MapRotResponse GetMapRotation()
        {
            MapRotResponse r;
            sRMtx.WaitOne();
            try
            {
                r = new MapRotResponse(ServerMap.ReadMapRotation(Core), Core.Server.Settings.Randomize);
            }
            catch (Exception e)
            {
                r = new MapRotResponse(e);
            }
            finally
            {
                sRMtx.ReleaseMutex();
            }

            return r;
        }

    }
}