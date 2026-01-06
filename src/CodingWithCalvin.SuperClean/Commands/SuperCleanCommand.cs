using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CodingWithCalvin.Otel4Vsix;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using MessageBox = System.Windows.Forms.MessageBox;

namespace CodingWithCalvin.SuperClean.Commands
{
    internal class SuperCleanCommand
    {
        private readonly Package _package;

        private SuperCleanCommand(Package package)
        {
            _package = package;

            var commandService = (OleMenuCommandService)
                ServiceProvider.GetService(typeof(IMenuCommandService));

            if (commandService == null)
            {
                return;
            }

            var menuCommandId = new CommandID(
                PackageGuids.CommandSetGuid,
                PackageIds.SuperCleanCommandId
            );
            var menuItem = new MenuCommand(OpenPathWrapper, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        private IServiceProvider ServiceProvider => _package;

        public static void Initialize(Package package)
        {
            _ = new SuperCleanCommand(package);
        }

        private static void OpenPathWrapper(object sender, EventArgs e)
        {
            using var activity = VsixTelemetry.StartCommandActivity("SuperClean.OpenPathWrapper");

            try
            {
                _ = OpenPathAsync(sender, e);
            }
            catch (Exception ex)
            {
                activity?.RecordError(ex);
                VsixTelemetry.TrackException(ex, new Dictionary<string, object>
                {
                    { "operation.name", "OpenPathWrapper" }
                });

                MessageBox.Show(
                    $@"
                                Fatal Error! Unable to invoke Super Clean!
                                {Environment.NewLine}
                                {Environment.NewLine}
                                Exception: {ex.Message}"
                );
            }
        }

        private static async Task OpenPathAsync(object sender, EventArgs e)
        {
            using var activity = VsixTelemetry.StartCommandActivity("SuperClean.OpenPathAsync");

            var activeItem = await VS.Solutions.GetActiveItemAsync();

            if (activeItem == null)
            {
                VsixTelemetry.LogInformation("No active item found");
                return;
            }

            activity?.SetTag("item.type", activeItem.Type.ToString());
                        switch (activeItem.Type)
            {
                case SolutionItemType.Solution:
                    try
                    {
                        var (success, errors) = await SuperCleanSolution();

                        if (!success)
                        {
                            throw new ApplicationException(errors);
                        }

                        VsixTelemetry.LogInformation("Solution super cleaned successfully");
                    }
                    catch (Exception ex)
                    {
                        activity?.RecordError(ex);
                        VsixTelemetry.TrackException(ex, new Dictionary<string, object>
                        {
                            { "operation.name", "SuperCleanSolution" }
                        });

                        MessageBox.Show(
                            $@"
                                Unable to Super Clean solution
                                {Environment.NewLine}
                                {Environment.NewLine}
                                Exception: {ex.Message}"
                        );
                    }

                    break;
                case SolutionItemType.Project:
                    try
                    {
                        SuperCleanProject(activeItem);
                        VsixTelemetry.LogInformation("Project super cleaned successfully");
                    }
                    catch (Exception ex)
                    {
                        activity?.RecordError(ex);
                        VsixTelemetry.TrackException(ex, new Dictionary<string, object>
                        {
                            { "operation.name", "SuperCleanProject" },
                            });

                        MessageBox.Show(
                            $@"
                                Unable to Super Clean project ${activeItem.Name}
                                {Environment.NewLine}
                                {Environment.NewLine}
                                Exception: {ex.Message}"
                        );
                    }

                    break;
            }

            async Task<(bool, string)> SuperCleanSolution()
            {
                using var solutionActivity = VsixTelemetry.StartCommandActivity("SuperClean.SuperCleanSolution");

                var success = true;
                var errors = new StringBuilder();
                var projectCount = 0;

                foreach (var project in await VS.Solutions.GetAllProjectsAsync())
                {
                    try
                    {
                        SuperCleanProject(project);
                        projectCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.AppendLine(ex.Message);
                        success = false;
                        solutionActivity?.RecordError(ex);
                    }
                }

                solutionActivity?.SetTag("projects.cleaned", projectCount);
                solutionActivity?.SetTag("success", success);

                return (success, errors.ToString());
            }

            void SuperCleanProject(SolutionItem project)
            {
                using var projectActivity = VsixTelemetry.StartCommandActivity("SuperClean.SuperCleanProject");

                                var projectPath =
                    Path.GetDirectoryName(project.FullPath)
                    ?? throw new InvalidOperationException();

                var binPath = Path.Combine(projectPath, "bin");
                var objPath = Path.Combine(projectPath, "obj");

                if (Directory.Exists(binPath))
                {
                    Directory.Delete(binPath, true);
                    projectActivity?.SetTag("bin.deleted", true);
                }

                if (Directory.Exists(objPath))
                {
                    Directory.Delete(objPath, true);
                    projectActivity?.SetTag("obj.deleted", true);
                }
            }
        }
    }
}
