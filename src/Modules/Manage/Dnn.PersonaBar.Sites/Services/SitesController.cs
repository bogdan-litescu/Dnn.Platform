﻿#region Copyright
// DotNetNuke® - http://www.dotnetnuke.com
// Copyright (c) 2002-2016
// by DotNetNuke Corporation
// All Rights Reserved
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using Dnn.PersonaBar.Library;
using Dnn.PersonaBar.Library.Attributes;
using Dnn.PersonaBar.Sites.Services.Dto;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security.Membership;
using DotNetNuke.Services.Localization;
using DotNetNuke.Services.Log.EventLog;
using DotNetNuke.Web.Api;

namespace Dnn.PersonaBar.Sites.Services
{
    [ServiceScope(Scope = ServiceScope.Host)]
    public class SitesController : PersonaBarApiController
    {
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof (SitesController));
        private readonly Components.SitesController _controller = new Components.SitesController();

        /// <summary>
        /// Gets list of portals
        /// </summary>
        /// <param name="portalGroupId"></param>
        /// <param name="filter"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns>List of portals</returns>
        /// <example>
        /// GET /api/personabar/host/evoqsites/GetPortals?portalGroupId=-1&amp;filter=mysite&amp;pageIndex=0&amp;pageSize=10
        /// </example>
        [HttpGet]
        public HttpResponseMessage GetPortals(int portalGroupId, string filter, int pageIndex, int pageSize)
        {
            try
            {
                var totalRecords = 0;
                IEnumerable<PortalInfo> portals;

                if (portalGroupId > Null.NullInteger)
                {
                    portals = PortalGroupController.Instance.GetPortalsByGroup(portalGroupId)
                                .Where(p => string.IsNullOrEmpty(filter) 
                                                || p.PortalName.ToLowerInvariant().Contains(filter.ToLowerInvariant()))
                                .ToList();
                    totalRecords = portals.Count();
                    portals = portals.Skip(pageIndex*pageSize).Take(pageSize);
                }
                else
                {
                    portals = PortalController.GetPortalsByName($"%{filter}%", pageIndex, pageSize,
                                ref totalRecords).Cast<PortalInfo>();
                }
                var response = new
                {
                    Success = true,
                    Results = portals.Select(GetPortalDto).ToList(),
                    TotalResults = totalRecords
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (Exception exc)
            {
                Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// POST: api/Sites/CreatePortal
        /// <summary>
        /// Adds a portal
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireHost]
        public HttpResponseMessage CreatePortal(CreatePortalRequest request)
        {
            try
            {
                var errors = new ArrayList();
                var portalId = _controller.CreatePortal(ref errors, GetDomainName(), GetAbsoluteServerPath(),
                    request.SiteTemplate, request.SiteName,
                    request.SiteAlias, request.SiteDescription, request.SiteKeywords,
                    request.IsChildSite, request.HomeDirectory, request.SiteGroupId, request.UseCurrentUserAsAdmin,
                    request.Firstname, request.Lastname, request.Username, request.Email, request.Password,
                    request.PasswordConfirm, request.Question, request.Answer);
                if (portalId < 0)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Success = false,
                        ErrorMessage = errors
                    });
                }
                var portal = PortalController.Instance.GetPortal(portalId);
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Success = true,
                    Portal = GetPortalDto(portal),
                    ErrorMessage = errors
                });
            }
            catch (Exception exc)
            {
                Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// POST: api/Sites/DeletePortal
        /// <summary>
        /// Deletes a portal
        /// </summary>
        /// <param name="portalId"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage DeletePortal(int portalId)
        {
            try
            {
                var portal = PortalController.Instance.GetPortal(portalId);
                if (portal != null)
                {
                    if (portal.PortalID != PortalSettings.PortalId &&
                        !PortalController.IsMemberOfPortalGroup(portal.PortalID))
                    {
                        var strMessage = PortalController.DeletePortal(portal, GetAbsoluteServerPath());
                        if (string.IsNullOrEmpty(strMessage))
                        {
                            EventLogController.Instance.AddLog("PortalName", portal.PortalName, PortalSettings,
                                UserInfo.UserID, EventLogController.EventLogType.PORTAL_DELETED);
                            return Request.CreateResponse(HttpStatusCode.OK, new {Success = true});
                        }
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Success = false,
                            ErrorMessage = strMessage
                        });
                    }
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Success = false,
                        ErrorMessage = "Portal deletion not allowed."
                    });
                }
                return Request.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    Success = false,
                    ErrorMessage = "Portal not found"
                });
            }
            catch (Exception exc)
            {
                Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// POST: api/Sites/ExportPortalTemplate
        /// <summary>
        /// Exports portal template
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage ExportPortalTemplate(ExportTemplateRequest request)
        {
            try
            {
                bool success;
                var message = _controller.ExportPortalTemplate(request, out success);

                if (!success)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Success = false,
                        Message = message
                    });
                }
                var template = _controller.GetPortalTemplates().First(x => x.Name == request.FileName);
                var templateItem = _controller.CreateListItem(template);
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Success = true,
                    Message = message,
                    Template = new
                    {
                        Name = templateItem.Text,
                        templateItem.Value
                    }
                });
            }
            catch (Exception exc)
            {
                Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// GET: api/Sites/GetPortalLocales
        /// <summary>
        /// Gets list of portal locales
        /// </summary>
        /// <param name="portalId"></param>
        /// <returns>List of portal locales</returns>
        [HttpGet]
        public HttpResponseMessage GetPortalLocales(int portalId)
        {
            try
            {
                var locales = LocaleController.Instance.GetLocales(portalId).Values;
                var response = new
                {
                    Success = true,
                    Results = locales.Select(l => new
                    {
                        l.Code,
                        l.EnglishName,
                        l.LanguageId
                    }),
                    TotalResults = locales.Count
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (Exception exc)
            {
                Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// POST: api/Sites/DeleteExpiredPortals
        /// <summary>
        /// Deletes expired portals
        /// </summary>
        /// <param></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage DeleteExpiredPortals()
        {
            try
            {
                PortalController.DeleteExpiredPortals(GetAbsoluteServerPath());
                return Request.CreateResponse(HttpStatusCode.OK, new {Success = true});
            }
            catch (Exception exc)
            {
                Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// GET: api/Sites/GetPortalTemplates
        /// <summary>
        /// Gets list of portal templates
        /// </summary>
        /// <param></param>
        /// <returns>List of portal templates</returns>
        [HttpGet]
        public HttpResponseMessage GetPortalTemplates()
        {
            try
            {
                Dictionary<string, string> temps = new Dictionary<string, string>();
                var templates = _controller.GetPortalTemplates();
                foreach (var template in templates)
                {
                    var item = _controller.CreateListItem(template);
                    temps.Add(item.Text, item.Value);
                }

                var response = new
                {
                    Success = true,
                    Results = new
                    {
                        Templates = temps.Select(t => new
                        {
                            Name = t.Key,
                            t.Value
                        }),
                        DefaultTemplate = _controller.GetDefaultTemplate()
                    },
                    TotalResults = 1
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (Exception exc)
            {
                Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// GET: api/Sites/RequiresQuestionAndAnswer
        /// <summary>
        /// Gets whether a Question/Answer is required for Password retrieval
        /// </summary>
        /// <param></param>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage RequiresQuestionAndAnswer()
        {
            try
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    MembershipProviderConfig.RequiresQuestionAndAnswer
                });
            }
            catch (Exception exc)
            {
                Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        private string GetAbsoluteServerPath()
        {
            var httpContext = Request.Properties["MS_HttpContext"] as HttpContextWrapper;
            var strServerPath = string.Empty;
            if (httpContext != null)
                strServerPath = httpContext.Request.MapPath(httpContext.Request.ApplicationPath);
            if (!strServerPath.EndsWith("\\"))
            {
                strServerPath += "\\";
            }
            return strServerPath;
        }

        private string GetDomainName()
        {
            var httpContext = Request.Properties["MS_HttpContext"] as HttpContextWrapper;
            return httpContext != null ? Globals.GetDomainName(httpContext.Request, true) : string.Empty;
        }

        private object GetPortalDto(PortalInfo portal)
        {
            string contentLocalizable;
            var portalDto = new
            {
                portal.PortalID,
                portal.PortalName,
                PortalAliases = _controller.FormatPortalAliases(portal.PortalID),
                portal.Users,
                portal.Pages,
                portal.HostSpace,
                portal.HostFee,
                portal.DefaultLanguage,
                ExpiryDate = _controller.FormatExpiryDate(portal.ExpiryDate),
                portal.LastModifiedOnDate,
                contentLocalizable =
                    PortalController.Instance.GetPortalSettings(portal.PortalID)
                        .TryGetValue("ContentLocalizationEnabled", out contentLocalizable) &&
                    Convert.ToBoolean(contentLocalizable),
                allowDelete =
                    (portal.PortalID != PortalSettings.PortalId &&
                     !PortalController.IsMemberOfPortalGroup(portal.PortalID))
            };
            return portalDto;
        }
    }
}