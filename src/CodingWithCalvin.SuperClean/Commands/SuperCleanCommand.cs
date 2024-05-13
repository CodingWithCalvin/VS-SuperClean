using System;
using System.ComponentModel.Design;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
            try
            {
                _ = OpenPathAsync(sender, e);
            }
            catch (Exception ex)
            {
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
            var activeItem = await VS.Solutions.GetActiveItemAsync();

            if (activeItem == null)
            {
                return;
            }

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
                    }
                    catch (Exception ex)
                    {
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
                    }
                    catch (Exception ex)
                    {
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
                var success = true;
                var errors = new StringBuilder();

                foreach (var project in await VS.Solutions.GetAllProjectsAsync())
                {
                    try
                    {
                        SuperCleanProject(project);
                    }
                    catch (Exception ex)
                    {
                        errors.AppendLine(ex.Message);
                        success = false;
                    }
                }

                return (success, errors.ToString());
            }

            void SuperCleanProject(SolutionItem project)
            {
                var projectPath =
                    Path.GetDirectoryName(project.FullPath)
                    ?? throw new InvalidOperationException();

                var binPath = Path.Combine(projectPath, "bin");
                var objPath = Path.Combine(projectPath, "obj");

                if (Directory.Exists(binPath))
                {
                    Directory.Delete(binPath, true);
                }

                if (Directory.Exists(objPath))
                {
                    Directory.Delete(objPath, true);
                }
            }
        }
    }
}
