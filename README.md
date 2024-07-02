# ScreenSaver

This is a C# Screensaver that displays album covers from your computer as a mozaique, changing every second.

## Introduction

Ever wanted to have a cool screensaver like the default iTunes screensaver on Mac, except on Windows? Well, there you go.

It looks like this: [[YouTube] Screensaver Demo](https://www.youtube.com/watch?v=YUvwzdiCS0g)

## Requirements

* OS: Windows.
* C# .NET
  * .dll libraries:
    * ID3Lib
    * MP3Lib

## How does it work?

1. A companion application `AlbumCoverFinder` parses a folder for MP3s and extracts album covers into a local database.
2. This database is then used by the Screensaver to display covers.
