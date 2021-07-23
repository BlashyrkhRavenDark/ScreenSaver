# ScreenSaver
This is a C# Screensaver that displays album covers from your computer as a mozaique, changing every second.

Ever wanted to have a cool screensaver like the default iTunes screensaver on Mac, except on Windows? Well, there you go.

It looks like this: https://www.youtube.com/watch?v=YUvwzdiCS0g

It needs ID3Lib and MP3Lib libraries.

How does it work?
A companion application (AlbumCoverFinder) parses a folder for MP3s and extracts album covers into a local database.
This database is then used by the Screensaver to display covers.

Installation instructions: 

1) Download ScreenSaver.scr and AlbumCoverFinder.exe
2) This could brick your computer, don't download executables from the internet, I'm not responsible for your recklessness.
3) Right click ScreenSaver.scr and select "Install". 
4) In the Windows Screensaver menu, click Settings to launch the parser that will scan your drives for MP3s. Alternatively, you can run AlbumCoverFinder to the same effect.
5) That's it. It's a screensaver. Keep your fingers off the keyboard.

