# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AutomationCore is a Windows desktop automation framework built with .NET 8, designed for image-based UI automation, screen capture, and input simulation. The project is currently undergoing architectural refactoring from a monolithic structure to a clean, modular architecture following SOLID principles.

## Build Commands

```bash
# Build the project
dotnet build AutomationCore.csproj

# Build in Release mode
dotnet build AutomationCore.csproj -c Release

# Run the application
dotnet run

# Clean build artifacts
dotnet clean
```

## Architecture Overview

The codebase follows a layered architecture pattern with clear separation of concerns:

### Core Architecture Layers

1. **Public/** - Simple facade API for end users
   - `AutomationClient` - Main entry point for all automation operations
   - `AutomationOptions` - Configuration classes with predefined profiles (HighPerformance, HighQuality)

2. **Features/** - High-level business features
   - `ImageSearch/` - Image template matching and search operations
   - `WindowAutomation/` - Window management and automation
   - `Workflows/` - Sequential automation workflow builder

3. **Services/** - Business logic layer
   - `Capture/` - Screen capture orchestration
   - `Input/` - Input simulation orchestration
   - `Matching/` - Template matching logic
   - `Windows/` - Window management logic

4. **Infrastructure/** - Platform-specific implementations
   - `Capture/` - Windows Graphics Capture (WGC) implementation
   - `Input/` - Windows input simulation
   - `Platform/` - P/Invoke wrappers and Windows API calls
   - `Storage/` - File-based template storage

5. **Core/** - Domain models and abstractions
   - `Models/` - Data structures and value objects
   - `Abstractions/` - Interface definitions
   - `Exceptions/` - Custom exception hierarchy

### Key Design Patterns

- **Dependency Injection**: Uses Microsoft.Extensions.DependencyInjection throughout
- **Options Pattern**: Configuration through strongly-typed options classes
- **Result Pattern**: Operations return Result<T> objects instead of throwing exceptions
- **Builder Pattern**: Workflow construction through fluent interface
- **Factory Pattern**: WgcCaptureFactory for capture device creation

## Development Guidelines

### API Usage Patterns

The framework provides three API levels:

1. **Simple API** (recommended): `AutomationClient.Create().ClickOnImageAsync("button")`
2. **Advanced API**: Direct service access through `AutomationClient.Images`, `AutomationClient.Input`
3. **Workflow API**: Fluent builder for complex scenarios

### Migration Status

The project is in active refactoring. See `MIGRATION_PLAN.md` for detailed migration status:

- ✅ New architecture implemented (Public, Features, Services, Infrastructure, Core)
- ❌ Legacy code still exists (EnhancedScreenCapture.cs, Assets/, Input/)
- 🔄 Currently migrating old monolithic classes to new modular structure

### Template System

Image templates are stored in file system with these key components:
- `ITemplateStorage` - Abstract storage interface
- Template keys reference image files for matching operations
- Templates support multiple formats and are cached for performance

### Capture Technology

Uses Windows Graphics Capture (WGC) for screen capture:
- Hardware-accelerated D3D11 capture
- Per-window and full-screen capture support
- Frame pooling for memory efficiency
- Async/await throughout capture pipeline

### Input Simulation

Provides humanized input simulation:
- Configurable human-like mouse movements with trajectories
- Variable typing speeds with optional typos
- Hardware-level input injection through Windows APIs

## Key Files to Understand

- `Public/Facades/AutomationClient.cs` - Main entry point, demonstrates all API patterns
- `Public/Configuration/AutomationOptions.cs` - Configuration system with presets
- `EXAMPLES.md` - Comprehensive usage examples for all API levels
- `MIGRATION_PLAN.md` - Current refactoring status and migration strategy

## Dependencies

Key external dependencies:
- **OpenCvSharp4.Windows** - Image processing and template matching
- **SharpDX** - DirectX integration for graphics capture
- **Microsoft.Windows.CsWinRT** - Windows Runtime projections
- **Microsoft.Extensions.*** - Dependency injection, logging, configuration

## Testing

No formal test framework is currently configured. Tests should be added during the refactoring process. The project includes:
- Example usage patterns in `EXAMPLES.md`
- Diagnostic methods in `AutomationClient` for system health checks
- Performance metrics collection capabilities

## Important Notes

- This is a Windows-only framework (requires Windows 10 v1903+ for WGC)
- Uses unsafe code blocks for high-performance graphics operations
- Currently supports both legacy and new API for backward compatibility
- All image operations use OpenCV for computer vision processing
- Hardware acceleration is enabled by default for capture operations