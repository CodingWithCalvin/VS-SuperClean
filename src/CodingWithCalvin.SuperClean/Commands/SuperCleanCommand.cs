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
                VSCommandTableVsct.CommandSetGuid.Guid,
                VSCommandTableVsct.CommandSetGuid.SuperCleanCommandId
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
                        var (success, errors, skippedWebsites) = await SuperCleanSolution();

                        if (!success)
                        {
                            throw new ApplicationException(errors);
                        }

                        VsixTelemetry.LogInformation("Solution super cleaned successfully");

                        if (skippedWebsites.Count > 0)
                        {
                            MessageBox.Show(
                                $"Super Clean skipped the following Web Site project(s) because their bin folder is required at runtime:{Environment.NewLine}{Environment.NewLine}  • {string.Join($"{Environment.NewLine}  • ", skippedWebsites)}",
                                "Super Clean",
                                System.Windows.Forms.MessageBoxButtons.OK,
                                System.Windows.Forms.MessageBoxIcon.Information
                            );
                        }
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
                        var cleaned = await SuperCleanProjectAsync(activeItem);

                        if (cleaned)
                        {
                            VsixTelemetry.LogInformation("Project super cleaned successfully");
                        }
                        else
                        {
                            VsixTelemetry.LogInformation($"Project skipped: {activeItem.Name}");

                            MessageBox.Show(
                                $"Super Clean skipped \"{activeItem.Name}\" because Web Site projects rely on their bin folder at runtime.",
                                "Super Clean",
                                System.Windows.Forms.MessageBoxButtons.OK,
                                System.Windows.Forms.MessageBoxIcon.Information
                            );
                        }
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

            async Task<(bool, string, List<string>)> SuperCleanSolution()
            {
                using var solutionActivity = VsixTelemetry.StartCommandActivity("SuperClean.SuperCleanSolution");

                var success = true;
                var errors = new StringBuilder();
                var projectCount = 0;
                var skippedWebsites = new List<string>();

                foreach (var project in await VS.Solutions.GetAllProjectsAsync())
                {
                    try
                    {
                        if (await SuperCleanProjectAsync(project))
                        {
                            projectCount++;
                        }
                        else
                        {
                            skippedWebsites.Add(project.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.AppendLine(ex.Message);
                        success = false;
                        solutionActivity?.RecordError(ex);
                    }
                }

                solutionActivity?.SetTag("projects.cleaned", projectCount);
                solutionActivity?.SetTag("projects.skipped", skippedWebsites.Count);
                solutionActivity?.SetTag("success", success);

                return (success, errors.ToString(), skippedWebsites);
            }

            async Task<bool> SuperCleanProjectAsync(SolutionItem project)
            {
                using var projectActivity = VsixTelemetry.StartCommandActivity("SuperClean.SuperCleanProject");

                if (project is Project typedProject && await typedProject.IsKindAsync(ProjectTypes.WEBSITE))
                {
                    projectActivity?.SetTag("skipped", true);
                    projectActivity?.SetTag("skip.reason", "website-project");
                    VsixTelemetry.LogInformation($"Skipped Web Site project: {project.Name}");
                    return false;
                }

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

                return true;
            }
        }
    }
}
