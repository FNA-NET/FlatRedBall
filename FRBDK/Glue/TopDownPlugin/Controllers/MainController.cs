﻿using FlatRedBall.Glue.IO;
using FlatRedBall.Glue.Managers;
using FlatRedBall.Glue.Plugins;
using FlatRedBall.Glue.Plugins.ExportedImplementations;
using FlatRedBall.Glue.SaveClasses;
using FlatRedBall.IO;
using FlatRedBall.IO.Csv;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TopDownPlugin.CodeGenerators;
using TopDownPlugin.Data;
using TopDownPlugin.DataGenerators;
using TopDownPlugin.Logic;
using TopDownPlugin.Models;
using TopDownPlugin.ViewModels;
using TopDownPlugin.Views;

namespace TopDownPlugin.Controllers
{
    public class MainController : Singleton<MainController>
    {
        #region Fields

        TopDownEntityViewModel viewModel;
        TopDownAnimationData topDownAnimationData;
        MainEntityView mainControl;

        bool ignoresPropertyChanges = false;

        public PluginBase MainPlugin { get; set; }



        CsvHeader[] lastHeaders;

        const string baseAnimationsName = "Base Animations";


        #endregion

        public MainController()
        {
        }

        public MainEntityView GetExistingOrNewControl()
        {
            if (mainControl == null)
            {
                viewModel = new TopDownEntityViewModel();
                viewModel.PropertyChanged += HandleViewModelPropertyChange;
                mainControl = new MainEntityView();

                mainControl.DataContext = viewModel;
            }

            return mainControl;
        }

        internal void MakeCurrentEntityTopDown()
        {
            if(viewModel != null)
            {
                viewModel.IsTopDown = true;
            }
        }

        private void HandleViewModelPropertyChange(object sender, PropertyChangedEventArgs e)
        {
            /////////// early out ///////////
            if (ignoresPropertyChanges)
            {
                return;
            }
            ///////////// end early out ///////////

            var entity = GlueState.Self.CurrentEntitySave;
            var viewModel = sender as TopDownEntityViewModel;
            bool shouldGenerateCsv, shouldGenerateEntity, shouldAddTopDownVariables;

            DetermineWhatToGenerate(e.PropertyName, viewModel,
                out shouldGenerateCsv, out shouldGenerateEntity, out shouldAddTopDownVariables);

            switch(e.PropertyName)
            {
                case nameof(TopDownEntityViewModel.IsTopDown):
                    HandleIsTopDownPropertyChanged(viewModel);
                    break;
                    // already handled in a dedicated method
                case nameof(TopDownEntityViewModel.TopDownValues):
                    RefreshAnimationValues(entity);
                    break;
            }

            if (shouldGenerateCsv)
            {
                if(viewModel.IsTopDown && viewModel.TopDownValues.Count == 0)
                {
                    var newValues = PredefinedTopDownValues.GetValues("Default");
                    viewModel.TopDownValues.Add(newValues);
                }

                GenerateCsv(entity, viewModel);
            }

            if (shouldAddTopDownVariables)
            {
                AddTopDownGlueVariables(entity);
            }

            if (shouldGenerateEntity)
            {
                TaskManager.Self.AddSync(
                    () => GlueCommands.Self.GenerateCodeCommands.GenerateElementCode(entity),
                    "Generating " + entity.Name);
            }

            if (shouldAddTopDownVariables)
            {
                TaskManager.Self.AddSync(() =>
                {
                    GlueCommands.Self.DoOnUiThread(() =>
                    {
                        GlueCommands.Self.RefreshCommands.RefreshPropertyGrid();
                        GlueCommands.Self.RefreshCommands.RefreshUiForSelectedElement();
                    });

                }, "Refreshing UI after top down plugin values changed");
            }

            if (shouldGenerateCsv || shouldGenerateEntity || shouldAddTopDownVariables)
            {
                TaskManager.Self.AddAsyncTask(
                    () =>
                    {
                        GlueCommands.Self.GluxCommands.SaveGlux();
                        EnumFileGenerator.Self.GenerateAndSave();
                        InterfacesFileGenerator.Self.GenerateAndSave();
                        if(shouldGenerateCsv || shouldAddTopDownVariables)
                        {
                            AiCodeGenerator.Self.GenerateAndSave();
                            AnimationCodeGenerator.Self.GenerateAndSave();
                        }
                    },"Saving Glue Project");
            }

        }

        internal void HandleElementRenamed(IElement renamedElement, string oldName)
        {
            if(topDownAnimationData != null)
            {
                SaveCurrentEntitySaveAnimationDataTask();
            }
        }

        private void HandleIsTopDownPropertyChanged(TopDownEntityViewModel viewModel)
        {
            if (viewModel.IsTopDown &&
                                GlueCommands.Self.GluxCommands.GetPluginRequirement(MainPlugin) == false)
            {
                GlueCommands.Self.GluxCommands.SetPluginRequirement(MainPlugin, true);
                GlueCommands.Self.PrintOutput("Added Top Down Plugin as a required plugin because the entity was marked as a top down entity");
                GlueCommands.Self.GluxCommands.SaveGluxTask();
            }

            if(viewModel.IsTopDown == false)
            {
                CheckForNoTopDownEntities();
            }
        }

        public void CheckForNoTopDownEntities()
        {
            var areAnyEntitiesTopDown = GlueState.Self.CurrentGlueProject.Entities
                .Any(item => TopDownEntityPropertyLogic.GetIfIsTopDown(item));

            if (!areAnyEntitiesTopDown)
            {
                FilePath absoluteFile =
                    GlueState.Self.CurrentGlueProjectDirectory +
                    AiCodeGenerator.Self.RelativeFile;

                TaskManager.Self.Add(() => GlueCommands.Self.ProjectCommands.RemoveFromProjects(absoluteFile),
                    "Removing " + AiCodeGenerator.Self.RelativeFile);

                // todo - probably need to remove all the other files that are created for top down
            }
        }

        private void DetermineWhatToGenerate(string propertyName, TopDownEntityViewModel viewModel, out bool shouldGenerateCsv, out bool shouldGenerateEntity, out bool shouldAddTopDownVariables)
        {
            var entity = GlueState.Self.CurrentEntitySave;
            shouldGenerateCsv = false;
            shouldGenerateEntity = false;
            shouldAddTopDownVariables = false;
            if (entity != null)
            {
                switch (propertyName)
                {
                    case nameof(TopDownEntityViewModel.IsTopDown):
                        entity.Properties.SetValue(propertyName, viewModel.IsTopDown);
                        // Don't generate a CSV if it's not a top down
                        shouldGenerateCsv = viewModel.IsTopDown;
                        shouldAddTopDownVariables = viewModel.IsTopDown;
                        shouldGenerateEntity = true;
                        break;
                    case nameof(TopDownEntityViewModel.TopDownValues):
                        shouldGenerateCsv = true;
                        // I don't think we need this...yet
                        shouldGenerateEntity = false;
                        shouldAddTopDownVariables = false;
                        break;
                }
            }
        }

        private void GenerateCsv(EntitySave entity, TopDownEntityViewModel viewModel)
        {
            TaskManager.Self.Add(
                                () => CsvGenerator.Self.GenerateFor(entity, viewModel, lastHeaders),
                                "Generating Top Down CSV for " + entity.Name);


            TaskManager.Self.Add(() =>
            {
                string rfsName = entity.Name.Replace("\\", "/") + "/" + CsvGenerator.RelativeCsvFile;
                bool isAlreadyAdded = entity.ReferencedFiles.FirstOrDefault(item => item.Name == rfsName) != null;

                if (!isAlreadyAdded)
                {
                    GlueCommands.Self.GluxCommands.AddSingleFileTo(
                        CsvGenerator.Self.CsvFileFor(entity).FullPath,
                        CsvGenerator.RelativeCsvFile,
                        "",
                        null,
                        false,
                        null,
                        entity,
                        null
                        );
                }

                var rfs = entity.ReferencedFiles.FirstOrDefault(item => item.Name == rfsName);

                if (rfs != null && rfs.CreatesDictionary == false)
                {
                    rfs.CreatesDictionary = true;
                    GlueCommands.Self.GluxCommands.SaveGlux();
                    GlueCommands.Self.GenerateCodeCommands.GenerateElementCode(entity);
                }

                const string customClassName = "TopDownValues";
                if (GlueState.Self.CurrentGlueProject.CustomClasses.Any(item => item.Name == customClassName) == false)
                {
                    CustomClassSave throwaway;
                    GlueCommands.Self.GluxCommands.AddNewCustomClass(customClassName, out throwaway);
                }

                var customClass = GlueState.Self.CurrentGlueProject.CustomClasses
                    .FirstOrDefault(item => item.Name == customClassName);

                if (rfs != null)
                {
                    if (customClass != null && customClass.CsvFilesUsingThis.Contains(rfs.Name) == false)
                    {
                        FlatRedBall. Glue.CreatedClass.CustomClassController.Self.SetCsvRfsToUseCustomClass(rfs, customClass, force: true);

                        GlueCommands.Self.GluxCommands.SaveGlux();
                    }
                }
            },
            "Adding csv to top down entity"
            );
        }

        private void AddTopDownGlueVariables(EntitySave entity)
        {
            // We don't make any variables because currently there's no concept of
            // different movement types that the plugin can switch between, the way
            // the platformer switches between ground/air/double-jump

            // Actually even though there's not air, ground, double jump, there is a CurrentMovementValues
            // property. But we'll just codegen that for now.
        }

        #region Update To / Refresh From Model

        internal void UpdateTo(EntitySave currentEntitySave)
        {
            ignoresPropertyChanges = true;

            viewModel.IsTopDown = currentEntitySave.Properties.GetValue<bool>(nameof(viewModel.IsTopDown));

            RefreshTopDownValues(currentEntitySave);

            // must be called after refreshing the top down values
            RefreshAnimationValues(currentEntitySave);

            ignoresPropertyChanges = false;
        }

        private void RefreshAnimationValues(EntitySave currentEntitySave)
        {
            LoadAnimationData(currentEntitySave);

            AddNecessaryAnimationMovementValuesFor(currentEntitySave, viewModel.TopDownValues);
            RemoveUnneededAnimationMovementValues(currentEntitySave, viewModel.TopDownValues);

            viewModel.AnimationRows.Clear();

            foreach(var animationValues in topDownAnimationData.Animations)
            {
                var row = new AnimationRowViewModel();
                row.AnimationRowName = animationValues.MovementValuesName;
                foreach(var setModel in animationValues.AnimationSets)
                {
                    var setViewModel = new AnimationSetViewModel();
                    setViewModel.AnimationSetName = setModel.AnimationSetName;

                    setViewModel.UpLeftAnimation = setModel.UpLeftAnimation;
                    setViewModel.UpAnimation = setModel.UpAnimation;
                    setViewModel.UpRightAnimation = setModel.UpRightAnimation;

                    setViewModel.LeftAnimation = setModel.LeftAnimation;
                    setViewModel.RightAnimation = setModel.RightAnimation;

                    setViewModel.DownLeftAnimation = setModel.DownLeftAnimation;
                    setViewModel.DownAnimation = setModel.DownAnimation;
                    setViewModel.DownRightAnimation = setModel.DownRightAnimation;

                    setViewModel.PropertyChanged += HandleSetViewModelPropertyChanged;

                    setViewModel.BackingData = setModel;

                    row.Animations.Add(setViewModel);
                }
                viewModel.AnimationRows.Add(row);
            }
        }

        private void RemoveUnneededAnimationMovementValues(EntitySave currentEntitySave, 
            ObservableCollection<TopDownValuesViewModel> topDownValues)
        {
            for(int i = topDownAnimationData.Animations.Count - 1; i > -1; i--)
            {
                var movementValueAnimations = topDownAnimationData.Animations[i];

                var isReferencedByTopDownValues =
                    movementValueAnimations.MovementValuesName == baseAnimationsName ||
                    topDownValues
                        .Any(topDownValue => topDownValue.Name == movementValueAnimations.MovementValuesName);

                if(!isReferencedByTopDownValues)
                {
                    topDownAnimationData.Animations.RemoveAt(i);
                }
            }
        }

        private void HandleSetViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var changedVm = sender as AnimationSetViewModel;

            changedVm.SetValuesOnBackingData();

            SaveCurrentEntitySaveAnimationDataTask();

            GlueCommands.Self.GenerateCodeCommands.GenerateCurrentElementCodeTask();
        }

        private void SaveCurrentEntitySaveAnimationDataTask()
        {
            var filePath = GetAnimationFilePathFor(GlueState.Self.CurrentEntitySave);

            TaskManager.Self.Add(() =>
            {
                var contents = JsonConvert.SerializeObject(topDownAnimationData);

                GlueCommands.Self.TryMultipleTimes(() =>
                {
                    System.IO.Directory.CreateDirectory(
                        filePath.GetDirectoryContainingThis().FullPath);
                    System.IO.File.WriteAllText(filePath.FullPath, contents);
                    GlueCommands.Self.PrintOutput($"Saved animation file {filePath.FullPath}");
                });

            }, $"Saving animation file for {GlueState.Self.CurrentEntitySave}");
        }

        private void AddNecessaryAnimationMovementValuesFor(EntitySave currentEntitySave, ObservableCollection<TopDownValuesViewModel> topDownValues)
        {

            if(topDownAnimationData.Animations.Any(item => item.MovementValuesName == baseAnimationsName) == false)
            {
                var newAnimation = new MovementValueAnimations();
                newAnimation.MovementValuesName = baseAnimationsName;
                newAnimation.AnimationSets.Add(new AnimationSetModel()
                {
                    AnimationSetName = "Idle"
                });
                newAnimation.AnimationSets.Add(new AnimationSetModel()
                {
                    AnimationSetName = "Move"
                });
                topDownAnimationData.Animations.Add(newAnimation);

                // temporary:
                
            }

            foreach (var topDownValue in viewModel.TopDownValues)
            {
                if(topDownAnimationData.Animations.Any(item => item.MovementValuesName == topDownValue.Name) == false)
                {
                    var newAnimation = new MovementValueAnimations();
                    newAnimation.MovementValuesName = topDownValue.Name;
                    newAnimation.AnimationSets.Add(new AnimationSetModel()
                    {
                        AnimationSetName = "Idle"
                    });
                    newAnimation.AnimationSets.Add(new AnimationSetModel()
                    {
                        AnimationSetName = "Move"
                    });
                    topDownAnimationData.Animations.Add(newAnimation);
                }
            }


        }

        private void RefreshTopDownValues(EntitySave currentEntitySave)
        {
            TopDownValuesCreationLogic.GetCsvValues(currentEntitySave,
                out Dictionary<string, TopDownValues> csvValues,
                out List<Type> additionalValueTypes,
                out CsvHeader[] csvHeaders);

            // Here we read the headers from the CSV. It's possible that the CSV
            // somehow got malformed, or is an old CSV, and is missing some of the
            // headers. We want to make sure we include required properties from the type
            // These are (as of July 7, 2020)
            //string Name

            //bool UsesAcceleration { get; set; } = true;

            //float MaxSpeed { get; set; }
            //float AccelerationTime { get; set; }
            //float DecelerationTime { get; set; }
            //bool UpdateDirectionFromVelocity { get; set; } = true;

            List<CsvHeader> tempList = csvHeaders.ToList();
            bool ContainsHeader(string name)
            {
                return tempList.Any(item => item.Name == name);
            }

            if(!ContainsHeader(nameof(TopDownValues.Name)))
            {
                tempList.Add(new CsvHeader
                {
                    OriginalText = nameof(TopDownValues.Name) + " (string, required)",
                    IsRequired = true,
                    Name = nameof(TopDownValues.Name),
                    MemberTypes = System.Reflection.MemberTypes.Property
                });

            }
            if (!ContainsHeader(nameof(TopDownValues.UsesAcceleration)))
            {
                tempList.Add(new CsvHeader
                {
                    OriginalText = nameof(TopDownValues.UsesAcceleration) + " (bool)",
                    Name = nameof(TopDownValues.UsesAcceleration),
                    MemberTypes = System.Reflection.MemberTypes.Property
                });
            }
            if (!ContainsHeader(nameof(TopDownValues.MaxSpeed)))
            {
                tempList.Add(new CsvHeader
                {
                    OriginalText = nameof(TopDownValues.MaxSpeed) + " (float)",
                    Name = nameof(TopDownValues.MaxSpeed),
                    MemberTypes = System.Reflection.MemberTypes.Property
                });
            }
            if (!ContainsHeader(nameof(TopDownValues.AccelerationTime)))
            {
                tempList.Add(new CsvHeader
                {
                    OriginalText = nameof(TopDownValues.AccelerationTime) + " (float)",
                    Name = nameof(TopDownValues.AccelerationTime),
                    MemberTypes = System.Reflection.MemberTypes.Property
                });
            }
            if (!ContainsHeader(nameof(TopDownValues.DecelerationTime)))
            {
                tempList.Add(new CsvHeader
                {
                    OriginalText = nameof(TopDownValues.DecelerationTime) + " (float)",
                    Name = nameof(TopDownValues.DecelerationTime),
                    MemberTypes = System.Reflection.MemberTypes.Property
                });
            }
            if (!ContainsHeader(nameof(TopDownValues.UpdateDirectionFromVelocity)))
            {
                tempList.Add(new CsvHeader
                {
                    OriginalText = nameof(TopDownValues.UpdateDirectionFromVelocity) + " (bool)",
                    Name = nameof(TopDownValues.UpdateDirectionFromVelocity),
                    MemberTypes = System.Reflection.MemberTypes.Property
                });
            }

            lastHeaders = tempList.ToArray();

            viewModel.TopDownValues.Clear();

            foreach (var value in csvValues.Values)
            {
                var topDownValuesViewModel = new TopDownValuesViewModel();

                // setfrom before property changed so that this doesn't raise an event
                topDownValuesViewModel.SetFrom(value, additionalValueTypes);
                topDownValuesViewModel.PropertyChanged += HandleTopDownValuesViewModelChanged;

                viewModel.TopDownValues.Add(topDownValuesViewModel);
            }
        }

        private void LoadAnimationData(EntitySave currentEntitySave)
        {
            FilePath fileToLoad = GetAnimationFilePathFor(currentEntitySave);

            topDownAnimationData = null;

            if (fileToLoad.Exists())
            {
                try
                {
                    var fileContents = System.IO.File.ReadAllText(fileToLoad.FullPath);

                    topDownAnimationData = JsonConvert.DeserializeObject<TopDownAnimationData>(fileContents);
                }
                catch
                {
                    // do nothing
                }
            }

            if (topDownAnimationData == null)
            {
                // if it's null then the file wasn't there or there was some unrecoverable failure, so just make a new one:
                topDownAnimationData = new TopDownAnimationData();
            }
        }

        public static FilePath GetAnimationFilePathFor(EntitySave currentEntitySave)
        {
            return $"{GlueState.Self.CurrentGlueProjectDirectory}AnimationSets/{currentEntitySave.Name}.json";
        }

        #endregion

        private void HandleTopDownValuesViewModelChanged(object sender, PropertyChangedEventArgs e)
        {
            var senderAsViewModel = (TopDownValuesViewModel)sender;


            switch(e.PropertyName)
            {
                case nameof(TopDownValuesViewModel.Name):
                    var backingData = senderAsViewModel.BackingData;

                    var animationToRename = topDownAnimationData
                        .Animations
                        .FirstOrDefault(item => item.MovementValuesName == backingData.Name);

                    animationToRename.MovementValuesName = senderAsViewModel.Name;

                    SaveCurrentEntitySaveAnimationDataTask();
                    
                    // todo - regenerate

                    break;
            }
        }

    }
}
