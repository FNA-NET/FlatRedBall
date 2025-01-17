﻿using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.WebRequestMethods;

namespace Npc.Data
{
    static class EmptyTemplates
    {
        public static List<PlatformProjectInfo> Projects { get; private set; } = new List<PlatformProjectInfo>();

        static EmptyTemplates()
        {
            Add("Desktop GL .NET 6 (Windows, Mac, Linux)", "FlatRedBallDesktopGlNet6Template", "FlatRedBallDesktopGlNet6Template.zip", "http://files.flatredball.com/content/FrbXnaTemplates/DailyBuild/ZippedTemplates/FlatRedBallDesktopGlNet6Template.zip", true);
            Add("Android .NET (Phone, Tablet, Fire TV)", "FlatRedBallAndroidMonoGameTemplate", "FlatRedBallAndroidMonoGameTemplate.zip", "http://files.flatredball.com/content/FrbXnaTemplates/DailyBuild/ZippedTemplates/FlatRedBallAndroidMonoGameTemplate.zip", true);
            Add("iOS .NET (iPhone, iPad, iPod Touch)", "FlatRedBalliOSMonoGameTemplate", "FlatRedBalliOSMonoGameTemplate.zip", "http://files.flatredball.com/content/FrbXnaTemplates/DailyBuild/ZippedTemplates/FlatRedBalliOSMonoGameTemplate.zip", true);

            Add("[Experimental] FNA .NET 7 (Windows, Mac, Linux)", "FlatRedBallDesktopFnaTemplate", "FlatRedBallDesktopFnaTemplate.zip", "http://files.flatredball.com/content/FrbXnaTemplates/DailyBuild/ZippedTemplates/FlatRedBallDesktopFnaTemplate.zip", true);

            Add("[deprecated, use Desktop GL .NET 6] Desktop GL .NET Framework 4.7.1 (Windows, Mac, Linux)", "FlatRedBallDesktopGlTemplate", "FlatRedBallDesktopGL.zip", "http://files.flatredball.com/content/FrbXnaTemplates/DailyBuild/ZippedTemplates/FlatRedBallDesktopGlTemplate.zip", true);
            Add("[deprecated, use Desktop GL .NET 6] Desktop XNA (Windows, requires XNA install)", "FlatRedBallXna4Template", "FlatRedBallXna4Template.zip" , "http://files.flatredball.com/content/FrbXnaTemplates/DailyBuild/ZippedTemplates/FlatRedBallXna4Template.zip", true);
            Projects.Add(new AddNewLocalProjectOption());
        }

        static void Add(string friendlyName, string namespaceName, string zipName, string url, bool supportedInGlue)
        {
            var newItem = new PlatformProjectInfo();

            newItem.FriendlyName = friendlyName;
            newItem.Namespace = namespaceName;
            newItem.ZipName = zipName;
            newItem.Url = url;
            newItem.SupportedInGlue = supportedInGlue;

            Projects.Add(newItem);
        }
    }
}
