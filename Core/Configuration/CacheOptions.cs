// AutomationCore/Core/Configuration/CacheOptions.cs
using System;

namespace AutomationCore.Core.Configuration
{
    public class CacheOptions
    {
        public int Capacity { get; set; } = 256;
        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromSeconds(5);
    }
}
