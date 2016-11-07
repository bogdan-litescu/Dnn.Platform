﻿#region Copyright
// DotNetNuke® - http://www.dotnetnuke.com
// Copyright (c) 2002-2016
// by DotNetNuke Corporation
// All Rights Reserved
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Web.Http;
using System.Xml;
using Dnn.PersonaBar.Library;
using Dnn.PersonaBar.Library.Attributes;
using Dnn.PersonaBar.Themes.Components;
using Dnn.PersonaBar.Themes.Components.DTO;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Host;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Instrumentation;
using DotNetNuke.Services.Localization;
using DotNetNuke.UI.Skins;
using DotNetNuke.UI.Skins.Controls;
using DotNetNuke.Web.Api;

namespace Dnn.PersonaBar.Themes.Services
{
    [ServiceScope(Scope = ServiceScope.Admin)]
    public class ThemesController : PersonaBarApiController
    {
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(ThemesController));
        private IThemesController _controller = Components.ThemesController.Instance;

        #region Public API

        [HttpGet]
        public HttpResponseMessage GetCurrentTheme()
        {
            try
            {


                return Request.CreateResponse(HttpStatusCode.OK, GetCurrentThemeObject());
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetThemes(ThemeLevel level)
        {
            try
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Layouts = _controller.GetLayouts(PortalSettings, level),
                    Containers = _controller.GetContainers(PortalSettings, level)
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetThemeFiles(string themeName, ThemeType type, ThemeLevel level)
        {
            try
            {
                var theme = (type == ThemeType.Skin ? _controller.GetLayouts(PortalSettings, level)
                                                    : _controller.GetContainers(PortalSettings, level)
                            ).FirstOrDefault(t => t.PackageName.Equals(themeName, StringComparison.InvariantCultureIgnoreCase));

                if (theme == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "ThemeNotFound");
                }

                return Request.CreateResponse(HttpStatusCode.OK, _controller.GetThemeFiles(PortalSettings, theme));
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage ApplyTheme(ApplyThemeInfo applyTheme)
        {
            try
            {
                _controller.ApplyTheme(PortalId, applyTheme.ThemeFile, applyTheme.Scope);
                return Request.CreateResponse(HttpStatusCode.OK, GetCurrentThemeObject());
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage ApplyDefaultTheme(ApplyDefaultThemeInfo defaultTheme)
        {
            try
            {
                var themeInfo = _controller.GetLayouts(PortalSettings, ThemeLevel.Global | ThemeLevel.Site)
                                    .FirstOrDefault(
                                        t => t.PackageName.Equals(defaultTheme.ThemeName, StringComparison.InvariantCultureIgnoreCase));

                if (themeInfo == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "ThemeNotFound");
                }

                var themeFiles = _controller.GetThemeFiles(PortalSettings, themeInfo);
                if (themeFiles.Count == 0)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "NoThemeFile");
                }

                _controller.ApplyDefaultTheme(PortalSettings, defaultTheme.ThemeName);
                return Request.CreateResponse(HttpStatusCode.OK, GetCurrentThemeObject());
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage DeleteThemePackage(ThemeInfo theme)
        {
            try
            {
                if (theme.Level == ThemeLevel.Global && !UserInfo.IsSuperUser)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "NoPermission");
                }

                _controller.DeleteThemePackage(PortalSettings, theme);
                return Request.CreateResponse(HttpStatusCode.OK, new { });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetEditableTokens()
        {
            try
            {
                var tokens = SkinControlController.GetSkinControls().Values
                    .Where(c => !string.IsNullOrEmpty(c.ControlKey) && !string.IsNullOrEmpty(c.ControlSrc))
                    .Select(c => new ListItemInfo { Text = c.ControlKey, Value = c.ControlSrc });

                return Request.CreateResponse(HttpStatusCode.OK, tokens);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetEditableSettings(string token)
        {
            try
            {
                var strFile = Globals.ApplicationMapPath + "\\" + token.ToLowerInvariant().Replace("/", "\\").Replace(".ascx", ".xml");
                var settings = new List<ListItemInfo>();
                if (File.Exists(strFile))
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(strFile);
                    foreach (XmlNode xmlSetting in xmlDoc.SelectNodes("//Settings/Setting"))
                    {
                        settings.Add(new ListItemInfo(xmlSetting.SelectSingleNode("Name").InnerText, xmlSetting.SelectSingleNode("Name").InnerText));
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, settings);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetEditableValues(string token, string setting)
        {
            try
            {
                var strFile = Globals.ApplicationMapPath + "\\" + token.ToLowerInvariant().Replace("/", "\\").Replace(".ascx", ".xml");
                var value = string.Empty;
                if (File.Exists(strFile))
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(strFile);
                    foreach (XmlNode xmlSetting in xmlDoc.SelectNodes("//Settings/Setting"))
                    {
                        if (xmlSetting.SelectSingleNode("Name").InnerText == setting)
                        {
                            string strValue = xmlSetting.SelectSingleNode("Value").InnerText;
                            switch (strValue)
                            {
                                case "":
                                    break;
                                case "[TABID]":
                                    value = "Pages";
                                    break;
                                default:
                                    value = strValue;
                                    break;
                            }
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new { Value = value });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage UpdateTheme(UpdateThemeInfo updateTheme)
        {
            try
            {
                var token = SkinControlController.GetSkinControls().Values.FirstOrDefault(t => t.ControlSrc == updateTheme.Token);
                if (token == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "InvalidParameter");
                }

                var themeFilePath = updateTheme.Path.ToLowerInvariant();
                if ((!themeFilePath.StartsWith("[g]") && !themeFilePath.StartsWith("[l]") && !themeFilePath.StartsWith("[s]"))
                    || (themeFilePath.StartsWith("[g]") && !UserInfo.IsSuperUser))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "InvalidPermission");
                }

                updateTheme.Token = token.ControlKey;
                _controller.UpdateTheme(PortalSettings, updateTheme);
                return Request.CreateResponse(HttpStatusCode.OK, new { });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireHost]
        public HttpResponseMessage ParseTheme(ParseThemeInfo parseTheme)
        {
            try
            {
                var themeName = parseTheme.ThemeName;

                var layout = _controller.GetLayouts(PortalSettings, ThemeLevel.Global | ThemeLevel.Site)
                                .FirstOrDefault(t => t.PackageName.Equals(themeName, StringComparison.InvariantCultureIgnoreCase));

                if (layout != null)
                {
                    _controller.ParseTheme(PortalSettings, layout, parseTheme.ParseType);
                }

                var container = _controller.GetContainers(PortalSettings, ThemeLevel.Global | ThemeLevel.Site)
                                .FirstOrDefault(t => t.PackageName.Equals(themeName, StringComparison.InvariantCultureIgnoreCase));

                if (container != null)
                {
                    _controller.ParseTheme(PortalSettings, container, parseTheme.ParseType);
                }

                return Request.CreateResponse(HttpStatusCode.OK, new { });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

         [HttpPost]
         [ValidateAntiForgeryToken]
         public HttpResponseMessage RestoreTheme()
         {
             try
             {
                 SkinController.SetSkin(SkinController.RootSkin, PortalId, SkinType.Portal, "");
                 SkinController.SetSkin(SkinController.RootContainer, PortalId, SkinType.Portal, "");
                 SkinController.SetSkin(SkinController.RootSkin, PortalId, SkinType.Admin, "");
                 SkinController.SetSkin(SkinController.RootContainer, PortalId, SkinType.Admin, "");
                 DataCache.ClearPortalCache(PortalId, true);
 
                 return Request.CreateResponse(HttpStatusCode.OK, GetCurrentThemeObject());
             }
             catch (Exception ex)
             {
                 Logger.Error(ex);
                 return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
             }
         }
 
        #endregion

        #region Private Methods

        private object GetCurrentThemeObject()
        {
            var cultureCode = LocaleController.Instance.GetCurrentLocale(PortalId).Code;
            var siteLayout = PortalController.GetPortalSetting("DefaultPortalSkin", PortalId, Host.DefaultPortalSkin, cultureCode);
            var siteContainer = PortalController.GetPortalSetting("DefaultPortalContainer", PortalId, Host.DefaultPortalContainer, cultureCode);
            var editLayout = PortalController.GetPortalSetting("DefaultAdminSkin", PortalId, Host.DefaultAdminSkin, cultureCode);
            var editContainer = PortalController.GetPortalSetting("DefaultAdminContainer", PortalId, Host.DefaultAdminContainer, cultureCode);

            var currentTheme = new
            {
                SiteLayout = _controller.GetThemeFile(PortalSettings, siteLayout, ThemeType.Skin),
                SiteContainer = _controller.GetThemeFile(PortalSettings, siteContainer, ThemeType.Container),
                EditLayout = _controller.GetThemeFile(PortalSettings, editLayout, ThemeType.Skin),
                EditContainer = _controller.GetThemeFile(PortalSettings, editContainer, ThemeType.Container)

            };

            return currentTheme;
        }

        #endregion
    }
}
