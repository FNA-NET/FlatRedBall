﻿using FlatRedBall.Glue.Controls;
using FlatRedBall.Glue.Managers;
using FlatRedBall.Glue.MVVM;
using FlatRedBall.Glue.Plugins.ExportedImplementations;
using FlatRedBall.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using TiledPluginCore.ViewModels;
using TiledPluginCore.Views;
using FlatRedBall.Glue.SaveClasses;
using FlatRedBall.Glue.Elements;

namespace TiledPluginCore.Controllers
{
    class LevelScreenController : Singleton<LevelScreenController>
    {
        #region Fields/Properties

        LevelScreenView view;
        LevelScreenViewModel viewModel;

        const string IsTmxLevel = nameof(IsTmxLevel);

        #endregion

        public bool GetIfShouldShow()
        {
            var screen = GlueState.Self.CurrentScreenSave;

            return screen?.Name == "Screens\\GameScreen";
        }

        internal LevelScreenView GetView()
        {
            if (view == null)
            {
                view = new LevelScreenView();
                viewModel = new ViewModels.LevelScreenViewModel();
                viewModel.PropertyChanged += HandleViewModelPropertyChanged;
                view.DataContext = viewModel;
            }
            return view;
        }

        private void HandleViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case nameof(viewModel.AutoCreateTmxScreens):
                    if(viewModel.AutoCreateTmxScreens)
                    {
                        GenerateScreensForAllTmxFiles();
                    }
                    else
                    {
                        RemoveScreensForAllTmxFiles();
                    }
                    break;
                case nameof(viewModel.ShowLevelScreensInTreeView):
                    var isHidden = !viewModel.ShowLevelScreensInTreeView;
                    var tmxLevelScreens =
                        GlueState.Self.CurrentGlueProject.Screens.Where(item => item.Properties.GetValue<bool>(IsTmxLevel)).ToArray();
                    foreach (var screen in tmxLevelScreens)
                    {
                        screen.IsHiddenInTreeView = isHidden;
                        GlueCommands.Self.RefreshCommands.RefreshTreeNodeFor(screen);
                    }
                    break;
            }
        }

        internal void RefreshViewModelTo(FlatRedBall.Glue.SaveClasses.ScreenSave currentScreenSave)
        {
            viewModel.GlueObject = currentScreenSave;

            RefreshViewModelTmxFileList();

            viewModel.UpdateFromGlueObject();
        }

        private void RefreshViewModelTmxFileList()
        {
            viewModel.TmxFiles.Clear();

            var allTmxFiles = GetAllLevelTmxFiles();

            var contentDirectory = GlueState.Self.ContentDirectory;
            foreach (var tmxFile in allTmxFiles)
            {
                viewModel.TmxFiles.Add(FileManager.MakeRelative(tmxFile.FullPath, contentDirectory));
            }
        }

        private static List<FilePath> GetAllLevelTmxFiles()
        {
            // This returns all TMX files that are in the content folder
            // and not referenced by a non-level element.
            // They are considered level files if they are files unreferenced by
            // Glue, or if they are referenced only by a level screen.

            var contentDirectory = GlueState.Self.ContentDirectory;
            var files = FileManager.GetAllFilesInDirectory(contentDirectory, "tmx").Select(item => new FilePath(item)).ToList();

            void RemoveRfsFromTmx(FlatRedBall.Glue.SaveClasses.ReferencedFileSave tmxRfs)
            {
                var filePath = GlueCommands.Self.FileCommands.GetFilePath(tmxRfs);

                if (files.Contains(filePath))
                {
                    files.Remove(filePath);
                }
            }

            foreach(var screen in GlueState.Self.CurrentGlueProject.Screens)
            {
                var isLevel = screen.Properties.GetValue<bool>(IsTmxLevel);
                if(!isLevel)
                {
                    foreach(var rfs in screen.ReferencedFiles)
                    {
                        RemoveRfsFromTmx(rfs);
                    }
                }
            }
            foreach(var entity in GlueState.Self.CurrentGlueProject.Entities)
            {
                foreach (var rfs in entity.ReferencedFiles)
                {
                    RemoveRfsFromTmx(rfs);
                }
            }

            foreach (var rfs in GlueState.Self.CurrentGlueProject.GlobalFiles)
            {
                RemoveRfsFromTmx(rfs);
            }

            return files;

        }

        private void GenerateScreensForAllTmxFiles()
        {
            var tmxFiles = GetAllLevelTmxFiles();

            var shouldSave = false;

            foreach(var tmxFile in tmxFiles)
            {
                var expectedScreenName = GetLevelScreenNameFor(tmxFile);

                var existingScreen = ObjectFinder.Self.GetScreenSave(expectedScreenName);

                if(existingScreen == null)
                {
                    var newScreen = new ScreenSave();
                    newScreen.Name = expectedScreenName;
                    newScreen.Properties.SetValue(IsTmxLevel, true);
                    newScreen.IsHiddenInTreeView = true;
                    newScreen.BaseScreen = "Screens\\GameScreen";

                    GlueCommands.Self.GluxCommands.ScreenCommands.AddScreen(newScreen, suppressAlreadyExistingFileMessage: true);
                    newScreen.UpdateFromBaseType();


                    // add the TMX file:
                    var rfs = GlueCommands.Self.GluxCommands.AddSingleFileTo(tmxFile.FullPath, tmxFile.NoPathNoExtension, null, null, false, null, newScreen, null, false);
                    var mapRfs = newScreen.GetNamedObject("Map");
                    mapRfs.SourceType = SourceType.File;
                    mapRfs.SourceFile = rfs.Name;
                    mapRfs.SourceName = "Entire File (LayeredTileMap)";

                    shouldSave = true;
                    //GlueCommands.Self.GluxCommands.ScreenCommands.

                    GlueCommands.Self.GenerateCodeCommands.GenerateElementCode(newScreen);

                }
            }

            if(shouldSave)
            {
                GlueCommands.Self.GluxCommands.SaveGlux();
                GlueCommands.Self.ProjectCommands.SaveProjects();
            }
        }

        private string GetLevelScreenNameFor(FilePath tmxFile)
        {
            var stripped = tmxFile.NoPathNoExtension
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace("(", " ")
                .Replace(")", " ");

            stripped = char.ToUpper(stripped[0]) + stripped.Substring(1) + "Level";



            return "Screens\\" + stripped;
        }

        private List<ScreenSave> GetLevelScreens()
        {
            List<ScreenSave> screens = new List<ScreenSave>();

            var tmxFiles = GetAllLevelTmxFiles();

            foreach (var tmxFile in tmxFiles)
            {
                var screenName = GetLevelScreenNameFor(tmxFile);

                var screen = ObjectFinder.Self.GetScreenSave(screenName);

                if (screen != null)
                {
                    screens.Add(screen);
                }
            }
            return screens;
        }

        private void RemoveScreensForAllTmxFiles()
        {
            foreach(var screen in GetLevelScreens())
            {
                TaskManager.Self.AddOrRunIfTasked(() =>
                {
                    // don't delete the files, in case the user wants to reference
                    // them or re-add.
                    GlueCommands.Self.GluxCommands.RemoveScreen(screen);
                }, $"Removing {screen}");
            }
        }

        internal void HandleTabShown()
        {
            if(viewModel.AutoCreateTmxScreens)
            {
                GenerateScreensForAllTmxFiles();
            }
        }
    }
}
