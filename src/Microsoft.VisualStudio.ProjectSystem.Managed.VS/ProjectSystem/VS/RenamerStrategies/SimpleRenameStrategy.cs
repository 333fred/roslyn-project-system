// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using System.Globalization;
using System.IO;

namespace Microsoft.VisualStudio.ProjectSystem.VS.RenameStrategies
{
    internal sealed class SimpleRenameStrategy : IRenameStrategy
    {
        private readonly IProjectThreadingService _threadingService;
        private readonly IUserNotificationServices _userNotificationServices;
        private readonly IOptionsSettings _optionsSettings;
        private readonly IRoslynServices _roslynServices;
        private bool _userPromptedOnce = false;
        private bool _userConfirmedRename = true;

        public SimpleRenameStrategy(IProjectThreadingService threadingService, IUserNotificationServices userNotificationService, IOptionsSettings optionsSettings, IRoslynServices roslynServices)
        {
            _threadingService = threadingService;
            _userNotificationServices = userNotificationService;
            _optionsSettings = optionsSettings;
            _roslynServices = roslynServices;
        }

        public async Task RenameAsync(Project myNewProject, string oldFileName, string newFileName)
        {
            Solution renamedSolution = await GetRenamedSolutionAsync(myNewProject, oldFileName, newFileName).ConfigureAwait(false);
            if (renamedSolution == null)
                return;

            await _threadingService.SwitchToUIThread();
            var renamedSolutionApplied = _roslynServices.ApplyChangesToSolution(myNewProject.Solution.Workspace, renamedSolution);

            if (!renamedSolutionApplied)
            {
                string failureMessage = string.Format(CultureInfo.CurrentCulture, Resources.RenameSymbolFailed, oldFileName);
                await _threadingService.SwitchToUIThread();
                _userNotificationServices.NotifyFailure(failureMessage);
            }
        }

        private async Task<Solution> GetRenamedSolutionAsync(Project myNewProject, string oldFileName, string newFileName)
        {
            var project = myNewProject;
            Solution renamedSolution = null;
            string oldNameBase = Path.GetFileNameWithoutExtension(oldFileName);

            while (project != null)
            {
                var newDocument = GetDocument(project, newFileName);
                if (newDocument == null)
                    return renamedSolution;

                var root = await GetRootNode(newDocument).ConfigureAwait(false);
                if (root == null)
                    return renamedSolution;

                var declarations = root.DescendantNodes().Where(n => HasMatchingSyntaxNode(newDocument, n, oldNameBase));
                var declaration = declarations.FirstOrDefault();
                if (declaration == null)
                    return renamedSolution;

                var semanticModel = await newDocument.GetSemanticModelAsync().ConfigureAwait(false);
                if (semanticModel == null)
                    return renamedSolution;

                var symbol = semanticModel.GetDeclaredSymbol(declaration);
                if (symbol == null)
                    return renamedSolution;

                bool userConfirmed = await CheckUserConfirmation(oldFileName).ConfigureAwait(false);
                if (!userConfirmed)
                    return renamedSolution;

                string newName = Path.GetFileNameWithoutExtension(newDocument.FilePath);

                // Note that RenameSymbolAsync will return a new snapshot of solution.
                renamedSolution = await _roslynServices.RenameSymbolAsync(newDocument.Project.Solution, symbol, newName).ConfigureAwait(false);
                project = renamedSolution.Projects.Where(p => StringComparers.Paths.Equals(p.FilePath, myNewProject.FilePath)).FirstOrDefault();
            }
            return null;
        }

        private Document GetDocument(Project project, string filePath) =>
            (from d in project.Documents where StringComparers.Paths.Equals(d.FilePath, filePath) select d).FirstOrDefault();

        private async Task<SyntaxNode> GetRootNode(Document newDocument) =>
            await newDocument.GetSyntaxRootAsync().ConfigureAwait(false);

        private async Task<bool> CheckUserConfirmation(string oldFileName)
        {
            if (_userPromptedOnce)
            {
                return _userConfirmedRename;
            }

            await _threadingService.SwitchToUIThread();
            var userNeedPrompt = _optionsSettings.GetPropertiesValue("Environment", "ProjectsAndSolution", "PromptForRenameSymbol", false);
            if (userNeedPrompt)
            {
                string renamePromptMessage = string.Format(CultureInfo.CurrentCulture, Resources.RenameSymbolPrompt, oldFileName);

                await _threadingService.SwitchToUIThread();
                _userConfirmedRename = _userNotificationServices.Confirm(renamePromptMessage);
            }

            _userPromptedOnce = true;
            return _userConfirmedRename;
        }

        private bool HasMatchingSyntaxNode(Document document, SyntaxNode syntaxNode, string name)
        {
            var generator = SyntaxGenerator.GetGenerator(document);
            var kind = generator.GetDeclarationKind(syntaxNode);

            if (kind == DeclarationKind.Class ||
                kind == DeclarationKind.Interface ||
                kind == DeclarationKind.Delegate ||
                kind == DeclarationKind.Enum ||
                kind == DeclarationKind.Struct)
            {
                return generator.GetName(syntaxNode) == name;
            }
            return false;
        }
    }
}
