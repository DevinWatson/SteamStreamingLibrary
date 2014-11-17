Steam Streaming Library
=====================
A library to control Steam in-home streaming.

Usage: SteamStreamingLibraryTester.exe IPAddress Port PSK AppID

PSK can be found in \Program Files (x86)\Steam\userdata\\{ID}\config\localconfig.vdf under SharedAuth:AuthData

### Credits 

Steam client discovery / streaming code: https://github.com/zhuowei/Varodahn

Steam client discovery: http://codingrange.com/blog/steam-in-home-streaming-discovery-protocol

Google protocol buffers library: https://developers.google.com/protocol-buffers/docs/overview

Protocol buffer descriptors extracted from Steam by the SteamKit project: https://github.com/SteamRE/SteamKit

Bouncy Castle C#: https://github.com/bcgit/bc-csharp

### License
[The MIT License (MIT)](http://opensource.org/licenses/MIT)


This application is not affiliated with Valve, Steam, or any of their partners. All copyrights reserved to their respective owners.

### Please Note
This library is built against a ever changing, internal only Steam interface and is likely to break on any Steam client update.
