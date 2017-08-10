﻿using FlatRedBall.Glue.Plugins.ExportedInterfaces.CommandInterfaces;
using FlatRedBall.Glue.SaveClasses;
using FlatRedBall.IO;
using System;
using System.Windows.Forms;
using FlatRedBall.Glue.FormHelpers;
using System.IO;
using FlatRedBall.Glue.VSHelpers.Projects;
using FlatRedBall.Glue.Elements;
using System.Collections.Generic;
using FlatRedBall.Glue.Managers;
using FlatRedBall.Glue.ContentPipeline;
using FlatRedBall.Glue.Projects;
using EditorObjects.Parsing;
using FlatRedBall.Glue.Errors;

namespace FlatRedBall.Glue.Plugins.ExportedImplementations.CommandInterfaces
{
    class ProjectCommands : IProjectCommands
    {
        public void SaveProjects()
        {
            ProjectManager.SaveProjects();
        }

        public void CreateAndAddPartialFile(IElement element, string partialName, string code)
        {
            var fileName = element.Name + ".Generated." + partialName + ".cs";
            var fullFileName = ProjectManager.ProjectBase.Directory + fileName;

            var save = false; // we'll be doing manual saving after it's created
            ProjectManager.CodeProjectHelper.CreateAndAddPartialCodeFile(fileName, save);
            
            // Now we can save it:
            FileManager.SaveText(code, fullFileName);
        }

        public void AddContentFileToProject(string absoluteFileName, bool saveProjects = true)
        {
            string relativeFileName = FileManager.MakeRelative(absoluteFileName, ProjectManager.ProjectBase.ContentProject.Directory);
            GlueCommands.Self.ProjectCommands.UpdateFileMembershipInProject(ProjectManager.ProjectBase, relativeFileName, false, false, null);
            if (saveProjects)
            {
                ProjectManager.SaveProjects();
            }
        }

        /// <summary>
        /// Updates the presence of the RFS in the main project.  If the RFS has project specific files, then those
        /// files are updated in the appropriate synced project.  
        /// </summary>
        /// <remarks>
        /// This method does not update synced projects if the synced projects use the same file.  The reason is because
        /// this is taken care of when the projects are saved later on.
        /// </remarks>
        /// <param name="referencedFileSave">The RFS representing the file to update membership on.</param>
        /// <returns>Whether anything was added to any projects.</returns>
        public bool UpdateFileMembershipInProject(ReferencedFileSave referencedFileSave)
        {
            var assetTypeInfo = referencedFileSave.GetAssetTypeInfo();

            bool shouldSkip = assetTypeInfo != null && assetTypeInfo.ExcludeFromContentProject;

            bool wasAnythingAdded = false;

            if (!shouldSkip)
            {

                bool useContentPipeline = referencedFileSave.UseContentPipeline || (assetTypeInfo != null && assetTypeInfo.MustBeAddedToContentPipeline);

                wasAnythingAdded = UpdateFileMembershipInProject(GlueState.Self.CurrentMainProject, referencedFileSave.GetRelativePath(), useContentPipeline, false);

                foreach (ProjectSpecificFile projectSpecificFile in referencedFileSave.ProjectSpecificFiles)
                {
                    wasAnythingAdded |= UpdateFileMembershipInProject(ProjectManager.GetProjectByName(projectSpecificFile.ProjectName), projectSpecificFile.FilePath, useContentPipeline, true);
                }
                if (wasAnythingAdded)
                {
                    int m = 3;
                }
            }
            return wasAnythingAdded;
        }

        /// <summary>
        /// Adds the argument fileRelativeToProject to the argument project if it's not already part of the project.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="fileName"></param>
        /// <param name="useContentPipeline">Whether this file must be part of the content pipeline. See internal notes on this variable.</param>
        /// <param name="shouldLink"></param>
        /// <param name="parentFile"></param>
        /// <returns>Whether the project was modified.</returns>
        public bool UpdateFileMembershipInProject(ProjectBase project, string fileName, bool useContentPipeline, bool shouldLink, string parentFile = null, bool recursive = true)
        {
            bool wasProjectModified = false;
            ///////////////////Early Out/////////////////////
            if (project == null) return wasProjectModified;

            /////////////////End Early Out//////////////////

            string fileToAddAbsolute = GlueCommands.Self.GetAbsoluteFileName(fileName, isContent:true);

            fileToAddAbsolute = fileToAddAbsolute.Replace("/", "\\");

            bool isFileAlreadyPartOfProject = false;

            bool needsToBeInContentProject = ShouldFileBeInContentProject(fileToAddAbsolute);

            BuildItemMembershipType bimt = BuildItemMembershipType.CopyIfNewer;

            // useContentPipeline can come from the parent file, if it uses content pipeline. But there may be other cases where we want to force content pepeline
            
            if (!useContentPipeline)
            {
                useContentPipeline = GetIfShouldUseContentPipeline(fileToAddAbsolute);
            }

            if (useContentPipeline)
            {
                bimt = BuildItemMembershipType.CompileOrContentPipeline;
            }
            else if (!project.ContentProject.ContentCopiedToOutput)
            {
                bimt = BuildItemMembershipType.Content;
            }

            if (!needsToBeInContentProject)
            {
                isFileAlreadyPartOfProject = project.IsFilePartOfProject(fileName, BuildItemMembershipType.CompileOrContentPipeline);
            }

            string fileRelativeToContent = FileManager.MakeRelative(
                fileToAddAbsolute,
                FileManager.GetDirectory(project.ContentProject.FullFileName));
            fileRelativeToContent = fileRelativeToContent.Replace("/", "\\");

            if (!isFileAlreadyPartOfProject && needsToBeInContentProject)
            {
                // Here we're going to get the absolute file name.
                // We want to get the file name 

                isFileAlreadyPartOfProject = project.ContentProject.IsFilePartOfProject(fileRelativeToContent, bimt);

                if (!isFileAlreadyPartOfProject)
                {
                    var buildItem = project.ContentProject.GetItem(fileRelativeToContent);
                    if (buildItem != null)
                    {
                        // The item is here but it's using the wrong build types.  Let's
                        // remove it and readd it so that it gets added with the right options.
                        // Let's remove it and say it's not part of the project so it gets removed and readded
                        project.ContentProject.RemoveItem(fileRelativeToContent);
                    }
                }
            }
            

            bool shouldSkipAdd = useContentPipeline &&
                project.ContentProject is VisualStudioProject &&
                !((VisualStudioProject)project.ContentProject).AllowContentCompile;

            bool shouldRemoveFile = shouldSkipAdd && 
                project.ContentProject.IsFilePartOfProject(fileRelativeToContent, bimt);

            if (shouldRemoveFile)
            {
                // It's using content pipeline, so we use XNBs not PNGs
                var buildItem = project.ContentProject.GetItem(fileRelativeToContent);
                if (buildItem != null)
                {
                    // The item is here but it's using the wrong build types.  Let's
                    // remove it and readd it so that it gets added with the right options.
                    // Let's remove it and say it's not part of the project so it gets removed and readded
                    project.ContentProject.RemoveItem(fileRelativeToContent);
                }
            }


            if (!isFileAlreadyPartOfProject && !shouldSkipAdd)
            {
                wasProjectModified = true;

                if (needsToBeInContentProject)
                {
                    AddFileToContentProject(project, useContentPipeline, shouldLink, fileToAddAbsolute);
                }
                else
                {
                    ProjectManager.CodeProjectHelper.AddFileToCodeProject(project, fileToAddAbsolute);
                }
            }

            var listOfReferencedFiles = new List<string>();

            bool didErrorOccur = false;
            try
            {
                // Glue is going to assume .cs files can't reference content:
                if (!fileToAddAbsolute.EndsWith(".cs"))
                {
                    listOfReferencedFiles = FileReferenceManager.Self.GetFilesReferencedBy(fileToAddAbsolute, TopLevelOrRecursive.TopLevel);
                }
            }
            catch (FileNotFoundException fnfe)
            {
                string errorMessage = "Could not track dependencies because of a missing file:\n" + fnfe.FileName;

                if (parentFile != null)
                {
                    errorMessage += "\n\nwhich is referenced by:\n" + parentFile;
                }

                didErrorOccur = true;
                ErrorReporter.ReportError(fnfe.FileName, errorMessage, false);

            }
            catch (InvalidOperationException ioe)
            {
                string errorMessage = "Problem trying to track dependencies because of a problem parsing the XML file :\n" + fileToAddAbsolute;
                didErrorOccur = true;
                ErrorReporter.ReportError(fileToAddAbsolute, errorMessage, false);
            }

            // We should tell the user that a file is missing here if it can't be found.

            if (!didErrorOccur && System.IO.File.Exists(fileToAddAbsolute) == false)
            {
                string message =
                    "The file\n\n" + fileToAddAbsolute + "\n\nis missing and is needed in this project";

                if (!string.IsNullOrEmpty(parentFile))
                {
                    message += " by\n\n" + parentFile;
                }
                ErrorReporter.ReportError(fileToAddAbsolute, message, false);
            }

            bool shouldAddChildren = true;


            if (fileName.EndsWith(".x") || useContentPipeline)
            {
                shouldAddChildren = false;
            }


            if (shouldAddChildren && listOfReferencedFiles != null && recursive)
            {
                for (int i = 0; i < listOfReferencedFiles.Count; i++)
                {
                    string file = listOfReferencedFiles[i];

                    if (file.Contains(@"../"))
                    {
                        string message = "The file\n\n" + fileToAddAbsolute + "\n\nincludes the file\n\n" + file + "\n\n" +
                            "This file should not contain ../ in the path.  This likely happened if you saved the file " +
                            "in a FRBDK tool and didn't select the \"Copy to relative\" option.\n\nYou should probably shut " +
                            "down Glue, fix this problem, then re-open your project.";

                        System.Windows.Forms.MessageBox.Show(message);
                    }
                    else
                    {
                        wasProjectModified |= UpdateFileMembershipInProject(project, file, useContentPipeline, shouldLink, fileToAddAbsolute);
                    }
                }
            }

            return wasProjectModified;
        }

        private static bool GetIfShouldUseContentPipeline(string fileAbsolute)
        {
            // grab the RFS and see if the rfs forces it
            var rfs = ObjectFinder.Self.GetReferencedFileSaveFromFile(fileAbsolute);
            bool useContentPipeline = false;
            if (rfs != null && rfs.UseContentPipeline)
            {
                useContentPipeline = true;
            }

            if(!useContentPipeline)
            {
                // let plugins decide:
                var returnedValue = PluginManager.GetIfUsesContentPipeline(fileAbsolute);
                if(returnedValue != null)
                {
                    useContentPipeline = returnedValue.Value;
                }
            }

            return useContentPipeline;
        }

        private static bool ShouldFileBeInContentProject(string fileToAddAbsolute)
        {
            bool toReturn = FileManager.IsRelativeTo(fileToAddAbsolute, GlueState.Self.CurrentMainContentProject.GetAbsoluteContentFolder());

            // If this is a .cs file and the content project is the same project as the main project, then it's actually a code file
            if (toReturn && GlueState.Self.CurrentMainContentProject.FullFileName == GlueState.Self.CurrentMainProject.FullFileName && FileManager.GetExtension(fileToAddAbsolute) == "cs")
            {
                toReturn = false;
            }

            return toReturn;
        }

        private static void AddFileToContentProject(ProjectBase project, bool useContentPipeline, bool shouldLink, string fileToAddAbsolute)
        {
            string relativeFileName = FileManager.MakeRelative(
                fileToAddAbsolute,
                FileManager.GetDirectory(project.ContentProject.FullFileName) + project.ContentProject.ContentDirectory);

            if (relativeFileName.StartsWith(ProjectManager.ContentDirectoryRelative))
            {
                relativeFileName = relativeFileName.Substring(ProjectManager.ContentDirectoryRelative.Length);
            }

            if (!useContentPipeline && project.ContentProject.IsFilePartOfProject(fileToAddAbsolute, BuildItemMembershipType.CompileOrContentPipeline))
            {
                ReferencedFileSave rfs = ObjectFinder.Self.GetReferencedFileSaveFromFile(fileToAddAbsolute);

                if (rfs != null)
                {
                    rfs.UseContentPipeline = false;
                    ContentPipelineHelper.ReactToUseContentPipelineChange(rfs);
                }

            }
            else if (useContentPipeline && project.ContentProject.IsFilePartOfProject(fileToAddAbsolute, BuildItemMembershipType.CopyIfNewer))
            {
                ReferencedFileSave rfs = ObjectFinder.Self.GetReferencedFileSaveFromFile(fileToAddAbsolute);

                if (rfs != null)
                {
                    rfs.UseContentPipeline = true;
                    ContentPipelineHelper.ReactToUseContentPipelineChange(rfs);
                }
            }
            else
            {
                project.ContentProject.AddContentBuildItem(
                    fileToAddAbsolute,
                    shouldLink ? SyncedProjectRelativeType.Linked : SyncedProjectRelativeType.Contained,
                    useContentPipeline);

            }

            PluginManager.ReceiveOutput("Added " + relativeFileName + $" to {project.Name} as content");
        }

        public void CreateAndAddCodeFile(string relativeFileName)
        {
            // see if the file exists. If not, create it:
            var absoluteFileName = GlueState.Self.CurrentGlueProjectDirectory + relativeFileName;

            if(System.IO.File.Exists(absoluteFileName) == false)
            {
                // will get back in later
                System.IO.File.WriteAllText(absoluteFileName, "");
            }

            var mainProject = GlueState.Self.CurrentMainProject;
            if(mainProject.IsFilePartOfProject(absoluteFileName) == false)
            {
                mainProject.AddCodeBuildItem(absoluteFileName);
                mainProject.Save();
                // do we need to project sync?
            }
        }


        public void CopyToBuildFolder(ReferencedFileSave rfs)
        {
            string source = ProjectManager.ContentDirectory + rfs.Name;

            CopyToBuildFolder(rfs);
        }

        public void CopyToBuildFolder(string absoluteSource)
        {
            string buildFolder = FileManager.GetDirectory(GlueState.Self.CurrentGlueProjectFileName) + "bin/x86/debug/Content/";
            string destination = buildFolder +  FileManager.MakeRelative(absoluteSource, ProjectManager.ContentDirectory);

            string destinationFolder = FileManager.GetDirectory(destination);

            // We used to only check the bin folder, but we want to check the specific
            // destination folder. If this is a new entity or a new folder in an entity, 
            // there's no reason to copy this over yet - it means the game hasn't been built
            // with this file:
            if(System.IO.Directory.Exists(destinationFolder))
            {
                string projectName = FileManager.RemovePath(FileManager.RemoveExtension(GlueState.Self.CurrentGlueProjectFileName));

                try
                {
                    System.IO.File.Copy(absoluteSource, destination, true);

                    PluginManager.ReceiveOutput("Copied " + absoluteSource + " ==> " + destination);
                }
                catch (Exception e)
                {
                    // this could really overwhelm the user with popups, so let's just show output:
                    PluginManager.ReceiveOutput("Error copying file:\n\n" + e.ToString());
                }
            }
        }

        public void AddDirectory(string folderName, TreeNode treeNodeToAddTo)
        {

            if (treeNodeToAddTo.IsGlobalContentContainerNode())
            {
                string rootDirectory = FileManager.RelativeDirectory;
                if (ProjectManager.ContentProject != null)
                {
                    rootDirectory = ProjectManager.ContentProject.GetAbsoluteContentFolder();
                }

                string directory = rootDirectory + "GlobalContent/" + folderName;

                Directory.CreateDirectory(directory);
            }
            else if (treeNodeToAddTo.IsRootEntityNode())
            {
                string directory = FileManager.RelativeDirectory + "Entities/" +
                    folderName;

                Directory.CreateDirectory(directory);
            }
            else if (treeNodeToAddTo.IsDirectoryNode())
            {
                // This used to use RelativeDirectory, but
                // I think we want this to be content, so not
                // sure why it uses RelativeDirectory...
                //string directory = FileManager.RelativeDirectory +
                //    currentTreeNode.GetRelativePath() +
                //    tiw.Result;
                // Update October 16, 2011
                // An Enity has both folders
                // in the code folder (represented
                // by RelativeDirectory) as well as
                // in the Content project.  An Entity
                // may not have files in the Content folder,
                // but it must have code files.  Therefore, we
                // create folders in the code directory tree and
                // we worry about content when NamedObjectSaves are
                // added to a given Entity later.
                //string directory = currentTreeNode.GetRelativePath() +
                //    tiw.Result;
                // Update February 17, 2012
                // But...when we add a new folder
                // to an Entity, we want that folder
                // to show up in the tree view in Glue.
                // Glue only scans the content folder, so
                // we want to make sure this folder exists
                // so it shows up okay.

                string directory = FileManager.RelativeDirectory +
                        treeNodeToAddTo.GetRelativePath() +
                        folderName;
                directory = ProjectManager.MakeAbsolute(directory, true);

                Directory.CreateDirectory(directory);

                directory = ProjectManager.ContentDirectory +
                        treeNodeToAddTo.GetRelativePath() +
                        folderName;
                directory = ProjectManager.MakeAbsolute(directory, true);

                Directory.CreateDirectory(directory);

            }
            else if (treeNodeToAddTo.IsFilesContainerNode() || treeNodeToAddTo.IsFolderInFilesContainerNode())
            {
                string directory =
                    treeNodeToAddTo.GetRelativePath() + folderName;

                Directory.CreateDirectory(ProjectManager.MakeAbsolute(directory, true));

                if (EditorLogic.CurrentEntityTreeNode != null)
                {
                    EditorLogic.CurrentEntityTreeNode.UpdateReferencedTreeNodes();
                }
                else if (EditorLogic.CurrentScreenTreeNode != null)
                {
                    EditorLogic.CurrentScreenTreeNode.UpdateReferencedTreeNodes();
                }
            }
            else if (treeNodeToAddTo.IsFolderInFilesContainerNode())
            {

                throw new NotImplementedException();
            }

            var containingElementNode = treeNodeToAddTo.GetContainingElementTreeNode();

            IElement element = null;
            if (containingElementNode != null)
            {
                element = containingElementNode.Tag as IElement;
            }

            if (containingElementNode == null)
            {
                GlueCommands.Self.RefreshCommands.RefreshGlobalContent();
            }
            else
            {
                GlueCommands.Self.RefreshCommands.RefreshUi(element);
            }

            GlueCommands.Self.RefreshCommands.RefreshDirectoryTreeNodes();
        }

        public string MakeAbsolute(string relativeFileName, bool forceAsContent = false)
        {
            return ProjectManager.MakeAbsolute(relativeFileName, forceAsContent);

        }

        public void RemoveFromProjects(string absoluteFileName)
        {
            ProjectManager.RemoveItemFromAllProjects(absoluteFileName);
        }
    }
}
