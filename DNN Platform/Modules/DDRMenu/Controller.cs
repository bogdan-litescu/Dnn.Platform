﻿using System;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Modules.Definitions;

namespace DotNetNuke.Web.DDRMenu
{
    using DotNetNuke.Entities.Portals;

    public class Controller : IUpgradeable, IPortable
	{
		private const string ddrMenuModuleName = "DDRMenu";
		private const string ddrMenuMmoduleDefinitionName = "DDR Menu";

		public string UpgradeModule(string version)
		{
			UpdateWebConfig();

			TidyModuleDefinitions();

			CleanOldAssemblies();

			CheckSkinReferences();

			return "UpgradeModule completed OK";
		}

		public string ExportModule(int moduleId)
		{
            var module = ModuleController.Instance.GetModule(moduleId, Null.NullInteger, true);
			var moduleSettings = module.ModuleSettings;

			var settings = new Settings
			               {
			               	MenuStyle = moduleSettings["MenuStyle"].ToString(),
			               	NodeXmlPath = moduleSettings["NodeXmlPath"].ToString(),
			               	NodeSelector = moduleSettings["NodeSelector"].ToString(),
			               	IncludeNodes = moduleSettings["IncludeNodes"].ToString(),
			               	ExcludeNodes = moduleSettings["ExcludeNodes"].ToString(),
			               	NodeManipulator = moduleSettings["NodeManipulator"].ToString(),
			               	IncludeContext = Convert.ToBoolean(moduleSettings["IncludeContext"]),
			               	IncludeHidden = Convert.ToBoolean(moduleSettings["IncludeHidden"]),
			               	ClientOptions = Settings.ClientOptionsFromSettingString(moduleSettings["ClientOptions"].ToString()),
			               	TemplateArguments =
			               		Settings.TemplateArgumentsFromSettingString(moduleSettings["TemplateArguments"].ToString())
			               };
			return settings.ToXml();
		}

		public void ImportModule(int moduleId, string content, string version, int userId)
		{
			var settings = Settings.FromXml(content);

			ModuleController.Instance.UpdateModuleSetting(moduleId, "MenuStyle", (settings.MenuStyle ?? ""));
			ModuleController.Instance.UpdateModuleSetting(moduleId, "NodeXmlPath", (settings.NodeXmlPath ?? ""));
			ModuleController.Instance.UpdateModuleSetting(moduleId, "NodeSelector", (settings.NodeSelector ?? ""));
			ModuleController.Instance.UpdateModuleSetting(moduleId, "IncludeNodes", (settings.IncludeNodes ?? ""));
			ModuleController.Instance.UpdateModuleSetting(moduleId, "ExcludeNodes", (settings.ExcludeNodes ?? ""));
			ModuleController.Instance.UpdateModuleSetting(moduleId, "NodeManipulator", (settings.NodeManipulator ?? ""));
			ModuleController.Instance.UpdateModuleSetting(moduleId, "IncludeContext", settings.IncludeContext.ToString());
			ModuleController.Instance.UpdateModuleSetting(moduleId, "IncludeHidden", settings.IncludeHidden.ToString());
			ModuleController.Instance.UpdateModuleSetting(moduleId, "TemplateArguments", Settings.ToSettingString(settings.TemplateArguments));
			ModuleController.Instance.UpdateModuleSetting(moduleId, "ClientOptions", Settings.ToSettingString(settings.ClientOptions));
		}

		private static void UpdateWebConfig()
		{
			const string navName = "DDRMenuNavigationProvider";
			const string navType = "DotNetNuke.Web.DDRMenu.DDRMenuNavigationProvider, DotNetNuke.Web.DDRMenu";

			var server = HttpContext.Current.Server;
			var webConfig = server.MapPath("~/web.config");

			var configXml = new XmlDocument();
			configXml.Load(webConfig);
			var navProviders = configXml.SelectSingleNode("/configuration/dotnetnuke/navigationControl/providers") as XmlElement;
// ReSharper disable PossibleNullReferenceException
			var addProvider = navProviders.SelectSingleNode("add[@name='" + navName + "']") as XmlElement;
// ReSharper restore PossibleNullReferenceException

			var needsUpdate = true;
			if (addProvider == null)
			{
				addProvider = configXml.CreateElement("add");
				addProvider.SetAttribute("name", navName);
				navProviders.AppendChild(addProvider);
			}
			else
			{
				needsUpdate = (addProvider.GetAttribute("type") != navType);
			}

			if (needsUpdate)
			{
				addProvider.SetAttribute("type", navType);
				configXml.Save(webConfig);
			}
		}

		private static void TidyModuleDefinitions()
		{
			RemoveLegacyModuleDefinitions(ddrMenuModuleName, ddrMenuMmoduleDefinitionName);
			RemoveLegacyModuleDefinitions("DDRMenuAdmin", "N/A");
		}

		private static void RemoveLegacyModuleDefinitions(string moduleName, string currentModuleDefinitionName)
		{
		    var mdc = new ModuleDefinitionController();

            var desktopModule = DesktopModuleController.GetDesktopModuleByModuleName(moduleName, Null.NullInteger);
			if (desktopModule == null)
			{
				return;
			}

			var desktopModuleId = desktopModule.DesktopModuleID;
            var modDefs = ModuleDefinitionController.GetModuleDefinitionsByDesktopModuleID(desktopModuleId);

			var currentModDefId = 0;
			foreach (var modDefKeyPair in modDefs)
			{
                if (modDefKeyPair.Value.FriendlyName.Equals(currentModuleDefinitionName, StringComparison.InvariantCultureIgnoreCase))
				{
                    currentModDefId = modDefKeyPair.Value.ModuleDefID;
				}
			}

			foreach (var modDefKeyPair in modDefs)
			{
                var oldModDefId = modDefKeyPair.Value.ModuleDefID;
				if (oldModDefId != currentModDefId)
				{
					foreach (ModuleInfo mod in ModuleController.Instance.GetAllModules())
					{
						if (mod.ModuleDefID == oldModDefId)
						{
							mod.ModuleDefID = currentModDefId;
                            ModuleController.Instance.UpdateModule(mod);
						}
					}

					mdc.DeleteModuleDefinition(oldModDefId);
				}
			}

            modDefs = ModuleDefinitionController.GetModuleDefinitionsByDesktopModuleID(desktopModuleId);
			if (modDefs.Count == 0)
			{
				new DesktopModuleController().DeleteDesktopModule(desktopModuleId);
			}
		}

		private static void CleanOldAssemblies()
		{
			var assembliesToRemove = new[] {"DNNDoneRight.DDRMenu.dll", "DNNGarden.DDRMenu.dll"};

			var server = HttpContext.Current.Server;
			var assemblyPath = server.MapPath("~/bin/");
			foreach (var assembly in assembliesToRemove)
			{
				File.Delete(Path.Combine(assemblyPath, assembly));
			}
		}

		private static void CheckSkinReferences()
		{
			var server = HttpContext.Current.Server;
			var portalsRoot = server.MapPath("~/Portals/");
			foreach (var portal in Directory.GetDirectories(portalsRoot))
			{
				foreach (var skinControl in Directory.GetFiles(portal, "*.ascx", SearchOption.AllDirectories))
				{
					try
					{
						var ascxText = File.ReadAllText(skinControl);
						var originalText = ascxText;
						ascxText = Regex.Replace(
							ascxText,
							Regex.Escape(@"Namespace=""DNNDoneRight.DDRMenu"""),
							@"Namespace=""DotNetNuke.Web.DDRMenu.TemplateEngine""",
							RegexOptions.IgnoreCase);
						ascxText = Regex.Replace(
							ascxText,
							Regex.Escape(@"Namespace=""DNNGarden.TemplateEngine"""),
							@"Namespace=""DotNetNuke.Web.DDRMenu.TemplateEngine""",
							RegexOptions.IgnoreCase);
						ascxText = Regex.Replace(
							ascxText,
							Regex.Escape(@"Assembly=""DNNDoneRight.DDRMenu"""),
							@"Assembly=""DotNetNuke.Web.DDRMenu""",
							RegexOptions.IgnoreCase);
						ascxText = Regex.Replace(
							ascxText,
							Regex.Escape(@"Assembly=""DNNGarden.DDRMenu"""),
							@"Assembly=""DotNetNuke.Web.DDRMenu""",
							RegexOptions.IgnoreCase);
						if (!ascxText.Equals(originalText))
						{
							File.WriteAllText(skinControl, ascxText);
						}
					}
					catch (IOException)
					{
					}
					catch (UnauthorizedAccessException)
					{
					}
					catch (SecurityException)
					{
					}
				}
			}
		}
	}
}