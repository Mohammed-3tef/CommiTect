# Commit Intent Detector - Visual Studio Extension

Automatically analyzes your code changes on file save and suggests a clear, professional commit name (Bug Fix, Feature, Refactor, Docs, Tests) using an external backend API.

## Features

**Automatic Intent Detection**
- Analyzes git diffs in real-time as you save files
- Predicts commit type: Bug Fix, Feature, Refactor, Risky Commit, or Documentation

**Smart & Fast**
- Debounced saves prevent excessive API calls
- Automatically skips binary files and ignored directories
- Works only in git repositories with tracked files

## Requirements

- Visual Studio 2022 (version 17.0 or higher)
- .NET Framework 4.7.2 or higher
- Git repository (extension only works in git repos)
- Backend API endpoint running (configurable)

## Installation

### From Source
1. Clone this repository
2. Open `CommitIntentDetector.sln` in Visual Studio 2022
3. Build the solution (Ctrl+Shift+B)
4. The VSIX file will be generated in `bin\Debug` or `bin\Release`
5. Double-click the `.vsix` file to install

### From VSIX Package
1. Download the `.vsix` file
2. Double-click to install
3. Restart Visual Studio

## Configuration

Configure the extension via **Tools > Options > Commit Intent Detector > General**:

- **Enabled** - Enable/disable the extension (default: `true`)
- **API URL** - Backend API endpoint (default: `http://commitintentdetector.runasp.net/api/Commit/analyze`)
- **Timeout (ms)** - API request timeout (default: `30000`)
- **Debounce Delay (ms)** - Delay before processing saves (default: `1000`)
- **Show Status Bar** - Show status bar indicator (default: `true`)
- **Allow Insecure SSL** - Allow self-signed certificates for development (default: `false`)

## How to Use

1. Open a solution in a git repository
2. Make changes to a tracked file
3. Save the file (Ctrl+S)
4. See the detected commit intent in a notification dialog
5. Choose to copy the intent to clipboard

The extension works automatically - no commands needed!

## Backend API Setup

The extension requires a running backend API. Your API should accept this format:

**Request:**
```json
POST /api/Commit/analyze
Content-Type: application/json

{
  "diff": "+ // Added a new feature\n+ function subtract(a, b) {\n+   return a - b;\n+ }"
}
```

**Response:**
```json
{
    "intent": "Intent: Feature\nMessage: Add subtraction support to the calculator"
}
```

Supported intents: `Bug Fix`, `Feature`, `Refactor`, `Risky Commit`, `Documentation`, `Test`

## Project Structure

```
CommitIntentDetector/
├── CommitIntentDetectorPackage.cs    # Main extension package
├── OptionPageGrid.cs                 # Configuration options page
├── DocumentSaveListener.cs           # Listens for file save events
├── GitService.cs                     # Git operations (diff, repo check)
├── ApiClient.cs                      # HTTP client for backend API
├── FileFilter.cs                     # Filters binary/ignored files
├── IntentProcessor.cs                # Processes and displays results
├── StatusBarService.cs               # Visual Studio status bar integration
├── source.extension.vsixmanifest     # VSIX manifest
└── CommitIntentDetector.csproj       # Project file
```

## Building the Extension

### Prerequisites
- Visual Studio 2022 with "Visual Studio extension development" workload
- .NET Framework 4.7.2 SDK

### Build Steps
1. Open `CommitIntentDetector.sln`
2. Restore NuGet packages
3. Build solution (F6 or Ctrl+Shift+B)
4. The VSIX will be in `bin\Debug\` or `bin\Release\`

### Debug the Extension
1. Press F5 to start debugging
2. A new Visual Studio instance (Experimental Instance) will launch
3. Open a git repository and test the extension

## Troubleshooting

**No notifications appearing?**
- Make sure you're in a git repository
- Check that the file has tracked changes
- Verify extension is enabled in Tools > Options

**SSL certificate errors?**
- For development: Enable "Allow Insecure SSL" in options
- For production: Use a valid SSL certificate

**API connection failed?**
- Verify the backend service is running
- Check API URL in Tools > Options
- Test the endpoint manually

**Extension not loading?**
- Check Visual Studio output window for errors
- Verify Visual Studio version is 2022 (17.0+)
- Try resetting the experimental instance: `devenv /ResetSettings Exp`

## Known Limitations

- Only works in git repositories
- Requires a running backend API
- Binary files are excluded
- Large diffs (>5MB) are skipped
- Untracked files are ignored

## Development

### Key Components

**CommitIntentDetectorPackage**
- Main entry point
- Initializes services and event listeners

**DocumentSaveListener**
- Implements `IVsRunningDocTableEvents3`
- Monitors file save events
- Triggers intent detection

**GitService**
- Executes git commands
- Retrieves diffs and repository information

**ApiClient**
- Sends HTTP requests to backend
- Handles SSL configuration

### Adding Features

To add new features:
1. Create new service class
2. Initialize in `CommitIntentDetectorPackage.InitializeAsync()`
3. Add configuration in `OptionPageGrid` if needed

## Privacy

This extension sends git diff content to your configured backend API for analysis. No data is sent to third parties.

## Support

- Report issues: [GitHub Issues](https://github.com/Mohammed-3tef/Commit_Intent_Detector/issues)
- Source code: [GitHub Repository](https://github.com/Mohammed-3tef/Commit_Intent_Detector)

## License

MIT License - see LICENSE file for details

---

**Enjoy better commit awareness in Visual Studio!** 🚀