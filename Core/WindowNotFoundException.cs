// AutomationCore/Core/WindowNotFoundException.cs
using System;

namespace AutomationCore.Core
{
    public class WindowNotFoundException : Exception
    {
        public WindowNotFoundException(string message) : base(message) { }
    }
}
