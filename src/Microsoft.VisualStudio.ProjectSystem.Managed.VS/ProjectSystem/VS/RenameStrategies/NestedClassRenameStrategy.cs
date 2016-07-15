using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using System.IO;

namespace Microsoft.VisualStudio.ProjectSystem.VS.RenameStrategies
{
    internal class NestedClassRenameStrategy : AbstractRenameStrategy
    {
        private static readonly char[] s_separatorChars = new[] { '+', '.' };

        private readonly IRoslynServices _roslynServices;

        public NestedClassRenameStrategy(IProjectThreadingService threadingService, IUserNotificationServices userNotificationService, IOptionsSettings optionsSettings, IRoslynServices roslynServices)
            : base(threadingService, userNotificationService, optionsSettings)
        {
            _roslynServices = roslynServices;
        }

        public override bool CanHandleRename(string oldFilePath, string newFilePath)
        {
            // The nested class strategy applies if the separator chars supported (+ and .) are in either the old
            // path or the new path. For example, Foo.cs -> Foo.Baz.cs should rename the inner class of Foo, if one
            // exists
            var oldPathBase = Path.GetFileNameWithoutExtension(oldFilePath);
            var newPathBase = Path.GetFileNameWithoutExtension(newFilePath);
            return s_separatorChars.Any(c => oldPathBase.Contains(c) || newPathBase.Contains(c));
        }

        public override Task RenameAsync(Project newProject, string oldFilePath, string newFilePath)
        {
            var oldPathBase = Path.GetFileNameWithoutExtension(oldFilePath);
            var newPathBase = Path.GetFileNameWithoutExtension(newFilePath);

            var oldSplitNames = oldPathBase.Split
            throw new NotImplementedException();
        }
    }
}
