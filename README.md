# ScreenSaver

This is a C# Screensaver that displays album covers from your computer as a mozaique, changing every second.

## Introduction

Ever wanted to have a cool screensaver like the default iTunes screensaver on Mac, except on Windows? Well, there you go.

It looks like this: [[YouTube] Screensaver Demo](https://www.youtube.com/watch?v=YUvwzdiCS0g)

To simply use the application:

* uncompress `release.zip`.
  * Copy `ScreenSaver.scr` into `C:\Windows\System32`.
* Right-click Windows desktop background.
  * Click on **Properties**
  * Select the **Screensaver** tab.
  * Select the screensaver.

## Requirements

Here are the requirements for developers:

* OS: Windows 10.
* Visual Studio 2022. Here are dependencies, listed here for convenience, but please check the `.csproj` files and Visual Studio for updated information: 
  * .NET Framework 4.7.2
  * .NET Framework 3.5 SP1
  * .dll libraries:
    * ID3Lib
    * MP3Lib
  * NuGet packages:
    * TagLibSharp, Version=2.3.0.0
    * Google.Apis, Version=1.62.0.0
    * Google.Apis.Auth, Version=1.62.0.0
    * Google.Apis.Auth.PlatformServices, Version=1.62.0.0
    * Google.Apis.Core, Version=1.62.0.0
    * Google.Apis.PlatformServices, Version=1.62.0.0
    * Microsoft.Bcl.AsyncInterfaces, Version=7.0.0.0
    * Newtonsoft.Json, Version=13.0.0.0

## How does it work?

1. A companion application `AlbumCoverFinder` parses a folder for MP3s and extracts album covers into a local database.
2. This database is then used by the Screensaver to display covers.
