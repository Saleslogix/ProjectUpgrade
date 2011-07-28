using System;

namespace Sage.Platform.Upgrade
{
    public class InvalidProjectException : ApplicationException
    {
        public string ProjectPath { get; private set; }

        public InvalidProjectException(string message, string projectPath)
            : base(message)
        {
            ProjectPath = projectPath;
        }

        public InvalidProjectException(string message)
            : base(message)
        {
        }
    }
}