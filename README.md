# SACGUI - Steam Auto Cracker GUI

A Windows Forms application for automating Steam game cracking operations. Built with .NET 10 and modern C# 14 features.

## Features

- 🎮 Auto-detect and crack Steam games with 2 clicks
- 📦 Batch processing for multiple games
- 🔒 Archive creation with optional password protection
- ☁️ Upload to file sharing services
- 🎨 Modern dark-themed UI with acrylic effects
- ⚡ Fast and efficient cracking workflow

## Requirements

- Windows 10/11 (64-bit)
- .NET 10 Desktop Runtime

## Building from Source

### Prerequisites
- .NET 10 SDK
- Visual Studio 2022 or JetBrains Rider (recommended for development)

### Build Steps

```bash
# Restore dependencies
dotnet restore "HFP's APPID Finder.csproj"

# Build Debug
dotnet build "HFP's APPID Finder.csproj" -c Debug

# Build Release
dotnet build "HFP's APPID Finder.csproj" -c Release

# Publish single-file executable
dotnet publish "HFP's APPID Finder.csproj" -c Release -r win-x64 --self-contained true
```

## Project Structure

```
├── Form1.cs                    # Main application form
├── Program.cs                  # Application entry point
├── AppSettings.cs              # Settings management
├── LogHelper.cs                # Logging utilities
├── Utilities/                  # Helper classes
│   ├── DataTableGeneration.cs
│   ├── Updater.cs
│   └── StringTools.cs
├── EnhancedShareWindow.cs      # Share/upload functionality
├── BatchGameSelectionForm.cs   # Batch processing UI
└── _bin/                       # External tools (7-Zip, Steamless, etc.)
```

## Code Quality

This project follows modern C# coding standards:
- ✅ C# 12 language features
- ✅ Nullable reference types enabled
- ✅ Code analysis and formatting via EditorConfig
- ✅ XML documentation for public APIs
- ✅ Async/await patterns for I/O operations
- ✅ Proper exception handling and logging

## Contributing

Contributions welcome! Please ensure:
1. Code follows the project's EditorConfig settings
2. All builds pass without warnings
3. New features include appropriate error handling
4. Public APIs have XML documentation comments

## License

See LICENSE file for details.

## Disclaimer

This tool is for educational purposes only. Respect intellectual property rights and use responsibly.

