﻿using FlatRedBall.Glue.Plugins;
using FlatRedBall.IO;
using OfficialPlugins.ContentPreview.ViewModels;
using OfficialPlugins.ContentPreview.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OfficialPlugins.ContentPreview.Managers
{
    internal static class PngManager
    {
        static PngPreviewView View;

        static PluginBase Plugin;
        static PluginTab Tab;

        public static PngViewModel ViewModel { get; private set; }

        public static void Initialize(PluginBase plugin)
        {
            Plugin = plugin;
        }

        public static PngPreviewView GetView(FilePath filePath)
        {
            CreateViewIfNecessary();

            return View;
        }

        private static void CreateViewIfNecessary()
        {
            if(View == null)
            {
                View = new PngPreviewView();
                ViewModel = new PngViewModel();
                View.DataContext = ViewModel;
                View.Initialize(new SpritePlugin.Managers.CameraLogic());
            }
        }

        public static void HideTab() => Tab?.Hide();

        internal static void ShowTab(FilePath filePath)
        {
            var view = PngManager.GetView(filePath);
            view.TextureFilePath = filePath;
            var vm = PngManager.ViewModel;
            vm.ResolutionWidth = view.Texture?.Width ?? 0;
            vm.ResolutionHeight = view.Texture?.Height ?? 0;
            view.ResetCamera();

            if (Tab == null)
            {
                Tab = Plugin.CreateTab(view, "PNG Preview", TabLocation.Center);
            }

            Tab.Show();
            view.GumCanvas.InvalidateVisual();
        }
    }
}