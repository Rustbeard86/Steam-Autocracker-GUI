# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] - Modernization & Cleanup

### Added
- EditorConfig file for consistent code formatting across IDEs
- Directory.Build.props for centralized build configuration
- GlobalUsings.cs for implicit namespace imports
- Comprehensive XML documentation comments for public APIs
- Enhanced README with build instructions and project structure
- This CHANGELOG file to track project changes

### Changed
- Updated to C# 12 language features
- Enabled nullable reference types for better null safety
- Modernized LogHelper.cs with:
  - File-scoped namespaces
  - XML documentation comments
  - Better error handling with logging
  - Modern C# patterns (target-typed new, range operators)
- Modernized AppSettings.cs with:
  - File-scoped namespaces
  - XML documentation comments
  - Sealed class to prevent inheritance
  - Improved null handling
  - Better error logging
- Modernized StringTools.cs with:
  - File-scoped namespaces
  - XML documentation comments
  - Range operators (C# 8) instead of Substring
  - Better null handling
  - More efficient KeepOnlyNumbers implementation using LINQ
- Improved .gitignore to exclude application-specific log files

### Fixed
- Empty catch blocks now include diagnostic output
- Consistent error handling across utility classes

### Code Quality
- Enabled .NET code analyzers
- Configured code analysis to run on build
- Set up treat warnings as errors for Release builds
- Standardized code formatting rules
