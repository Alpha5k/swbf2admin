﻿using System.Net;
using SWBF2Admin.Gameserver;
namespace SWBF2Admin.Web.Pages
{
    class DefaultPage : AjaxPage
    {
        class DefaultApiResponse
        {
            public bool Online { get; set; }
            public DefaultApiResponse(bool online) { Online = online; }
        }

        public DefaultPage(AdminCore core) : base(core, "/", "frame.htm") { }

        public override void HandleGet(HttpListenerContext ctx, WebUser user)
        {
            ReturnTemplate(ctx,
                "{account:username}", user.Username,
                "{account:lastvisit}", user.LastVisit.ToShortDateString());
        }

        public override void HandlePost(HttpListenerContext ctx, WebUser user, string postData)
        {
            ApiRequestParams p = null;
            if ((p = TryJsonParse<ApiRequestParams>(ctx, postData)) == null) return;

            if (p.Action.Equals("status_get"))
            {
                WebAdmin.SendHtml(ctx, ToJson(new DefaultApiResponse(Core.Server.Status == ServerStatus.Online)));
            }
        }

    }
}