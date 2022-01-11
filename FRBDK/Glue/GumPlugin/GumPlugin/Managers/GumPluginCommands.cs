﻿using FlatRedBall.Glue.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gum.DataTypes;
using Gum.DataTypes.Behaviors;
using FlatRedBall.IO;
using FlatRedBall.Glue.Plugins.ExportedImplementations;
using FlatRedBall.Glue.IO;
using Gum.Managers;
using Polenter.Serialization;

namespace GumPlugin.Managers
{
    public class GumPluginCommands : Singleton<GumPluginCommands>
    {
        #region File Commands

        public async Task SaveGumxAsync(bool saveAllElements = false)
        {
            await TaskManager.Self.AddAsync(() =>
            {
                string gumProjectFileName = GumProjectManager.Self.GetGumProjectFileName();

                GlueCommands.Self.FileCommands.IgnoreNextChangeOnFile(gumProjectFileName);
                GlueCommands.Self.TryMultipleTimes(() => AppState.Self.GumProjectSave.Save(gumProjectFileName, saveAllElements));
            }, "Saving gum projects");
        }

        #endregion

        #region Component

        internal void AddComponent(FilePath filePath)
        {
            var component = FileManager.XmlDeserialize<ComponentSave>(filePath.Standardized);

            AddComponentToGumProject(component);

            component.Initialize(StandardElementsManager.Self.GetDefaultStateFor("Component"));
        }

        internal void AddComponentToGumProject(ComponentSave gumComponent)
        {
            AppState.Self.GumProjectSave.Components.Add(gumComponent);
            var elementReference = new ElementReference
            {
                ElementType = ElementType.Component,
                Name = gumComponent.Name
            };
            AppState.Self.GumProjectSave.ComponentReferences.Add(elementReference);
            AppState.Self.GumProjectSave.ComponentReferences.Sort((first, second) => first.Name.CompareTo(second.Name));
            FlatRedBall.Glue.Plugins.PluginManager.ReceiveOutput("Added Gum component " + gumComponent.Name);

        }

        internal void SaveComponent(ComponentSave gumComponent)
        {
            string gumProjectFileName = GumProjectManager.Self.GetGumProjectFileName();

            var directory = FileManager.GetDirectory(gumProjectFileName) + ElementReference.ComponentSubfolder + "/";
            string componentFileName =
                directory + gumComponent.Name + "." + GumProjectSave.ComponentExtension;


            gumComponent.Save(componentFileName);

        }

        internal bool IsComponentFileReferenced(FilePath componentFilePath)
        {
            var gumDirectory = FileManager.GetDirectory(AppState.Self.GumProjectSave.FullFileName);

            var componentDirectory = new FilePath(gumDirectory + "Components/");

            var isInComponentsDirectory = componentDirectory.IsRootOf(componentFilePath);

            var found = false;
            if (isInComponentsDirectory)
            {
                var componentName = FileManager.RemoveExtension(FileManager.MakeRelative(
                    componentFilePath.Standardized, componentDirectory.Standardized)).Replace("\\", "/").ToLowerInvariant() ;

                found = AppState.Self.GumProjectSave.Components
                    .Any(item => item.Name.Replace("\\", "/").ToLowerInvariant() == componentName);
            }

            return found;
        }

        #endregion

        #region Screen

        internal void AddScreenToGumProject(ScreenSave gumScreen)
        {
            AppState.Self.GumProjectSave.Screens.Add(gumScreen);
            var elementReference = new ElementReference
            {
                ElementType = ElementType.Screen,
                Name = gumScreen.Name
            };
            AppState.Self.GumProjectSave.ScreenReferences.Add(elementReference);
            AppState.Self.GumProjectSave.ScreenReferences.Sort((first, second) => first.Name.CompareTo(second.Name));
            FlatRedBall.Glue.Plugins.PluginManager.ReceiveOutput("Added Gum screen " + gumScreen.Name);

        }

        internal bool IsScreenFileReferenced(FilePath screenFilePath)
        {
            var gumDirectory = FileManager.GetDirectory(AppState.Self.GumProjectSave.FullFileName);

            var screenDirectory = new FilePath( gumDirectory + "Screens/");

            var isInScreensDirectory = screenDirectory.IsRootOf(screenFilePath);

            var found = false;
            if(isInScreensDirectory)
            {
                var screenName = FileManager.MakeRelative(
                    screenFilePath.Standardized, screenDirectory.Standardized);

                found = AppState.Self.GumProjectSave.Screens
                    .Any(item => item.Name.ToLowerInvariant() == screenName.ToLowerInvariant());

            }

            return found;
        }

        internal void SaveScreen(ScreenSave gumScreen)
        {
            string gumProjectFileName = GumProjectManager.Self.GetGumProjectFileName();

            var directory = FileManager.GetDirectory(gumProjectFileName) + ElementReference.ScreenSubfolder + "/";
            string screenFileName =
                directory + gumScreen.Name + "." + GumProjectSave.ScreenExtension;


            gumScreen.Save(screenFileName);

        }

        public string GetGumScreenNameFor(FlatRedBall.Glue.SaveClasses.ScreenSave glueScreen) => FileManager.RemovePath(glueScreen.Name) + "Gum";

        internal void AddScreenForGlueScreen(FlatRedBall.Glue.SaveClasses.ScreenSave glueScreen)
        {
            string gumScreenName = GetGumScreenNameFor(glueScreen);

            bool exists = AppState.Self.GumProjectSave.Screens.Any(item => item.Name == gumScreenName);
            if (!exists)
            {
                Gum.DataTypes.ScreenSave gumScreen = new Gum.DataTypes.ScreenSave();
                gumScreen.Initialize(StandardElementsManager.Self.GetDefaultStateFor("Screen"));
                gumScreen.Name = gumScreenName;

                string gumProjectFileName = GumProjectManager.Self.GetGumProjectFileName();

                AddScreenToGumProject(gumScreen);

                SaveGumxAsync(saveAllElements: false);

                SaveScreen(gumScreen);

            }
            // Select the screen to add the file to this
            GlueState.Self.CurrentScreenSave = glueScreen;

            RightClickManager.Self.AddScreenByName(gumScreenName, glueScreen);
        }

        internal void RemoveScreen(ScreenSave gumScreen, bool save = true)
        {
            AppState.Self.GumProjectSave.Screens.Remove(gumScreen);
            AppState.Self.GumProjectSave.ScreenReferences.RemoveAll(item => item.Name == gumScreen.Name);

            if(save)
            {
                SaveGumxAsync();
            }

        }

        #endregion

        #region Behavior

        internal void AddBehavior(BehaviorSave behavior)
        {
            AppState.Self.GumProjectSave.Behaviors.Add(behavior);
            var behaviorReference = new BehaviorReference
            {
                Name = behavior.Name
            };
            AppState.Self.GumProjectSave.BehaviorReferences.Add(behaviorReference);
            AppState.Self.GumProjectSave.BehaviorReferences.Sort((first, second) =>
            {
                if (first.Name == null)
                {
                    return 0;
                }
                else
                {
                    return first.Name.CompareTo(second.Name);
                }
            });
            FlatRedBall.Glue.Plugins.PluginManager.ReceiveOutput("Added Gum behavior " + behavior.Name);

        }

        internal void SaveBehavior(BehaviorSave behavior)
        {
            string gumProjectFileName = GumProjectManager.Self.GetGumProjectFileName();
            var directory = FileManager.GetDirectory(gumProjectFileName) + BehaviorReference.Subfolder + "/";
            string behaviorFileName =
                directory + behavior.Name + "." + BehaviorReference.Extension;

            behavior.Save(behaviorFileName);

        }

        #endregion

        internal void SaveStandardElement(StandardElementSave gumStandardElement)
        {
            string gumProjectFileName = GumProjectManager.Self.GetGumProjectFileName();

            var directory = FileManager.GetDirectory(gumProjectFileName) + ElementReference.StandardSubfolder + "/";
            string standardsFileName =
                directory + gumStandardElement.Name + "." + GumProjectSave.StandardExtension;

            gumStandardElement.Save(standardsFileName);
            //var serializer = new SharpSerializer();
            //serializer.Serialize(gumStandardElement, standardsFileName);

        }

        internal void UpdateGumToGlueResolution()
        {
            TaskManager.Self.AddOrRunIfTasked(() =>
            {
                var displaySettings = GlueState.Self.CurrentGlueProject?.DisplaySettings;

                if (displaySettings != null && AppState.Self.GumProjectSave != null)
                {
                    var gumProject = AppState.Self.GumProjectSave;

                    if(gumProject.DefaultCanvasWidth != displaySettings.ResolutionWidth ||
                        gumProject.DefaultCanvasHeight != displaySettings.ResolutionHeight)
                    {
                        gumProject.DefaultCanvasWidth = displaySettings.ResolutionWidth;
                        gumProject.DefaultCanvasHeight = displaySettings.ResolutionHeight;

                        GumPluginCommands.Self.SaveGumxAsync();
                    }

                }
            }, "Setting Gum Resolution");


        }

    }
}