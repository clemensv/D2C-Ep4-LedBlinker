D2C-Ep3-LedBlinker
==================

Device to Cloud - Subscribe Episode 3 - LedBlinker Example

This sample is a very simple cloud gateway for my little Arduino "Hello World" device that can do 
nothing more than switching an LED. The cloud gateway, implemented in the LedSwitcherServer project (in 
C#), has a plain TCP endpoint for the device(s) to connect to from within the local network they're in. 
The server will register and hold on to the connection and maintaining it with a ping sent every 55s
to entertain NATs and the Windows Azure load-balancer on the route so that they don't cut the connection.
We will explore this aspect and its impact on energy consumption in more detai in a coming episode. 

To the service consumer, the service exposes a very simple WebAPI endpoint (SwitchController) that allows 
turning the device on or off. The WebAPI implementation looks up whether the requested device is available
and either performs the switch by sending a command to the device or it reports a 404.

IMPORTANT: Mind that this sample is a step on a longer journey. It's not a best-practice example. There is
no implementation of authorization or any other secueity features as of yet. The composition is also not 
representative of how you'd put together a robust service. The goal here is to show the basic principle in 
a fairly minimal fashion. We'll make this more complicated early enough :)

Projects:

* LedBlinkService - WorkerRole host for Windows Azure Worker Role
* LedBlinker - Windows Azure Deployment Project
* LedBlinkerArduino - Arduino Client Project for the Gateway variant
* LedBlinkerArduinoDirect - Arduino Client from Episode 2
* LedBlinkerServer - Server Implementation
* LedBlinkerTestHost - Local test host to run the server outside of the WA Emulator

The project assumes you have Visual Studio 2012 with the latest (2.1) Windows Azure Tools 
(http://www.windowsazure.com/en-en/downloads/) and the Arduino plug-in from 
http://www.visualmicro.com/ 

If you don't have a Windows Azure account, yet, we offer a free trial at 

http://www.windowsazure.com/en-us/pricing/free-trial/

(Oh, and, yes, I did roll the storage key that's embedded in the configuration, so you will have to use 
your own storage account for diagnostics and obviously also publish to your own cloud service deployment.
Guidelines are here: http://msdn.microsoft.com/en-us/library/windowsazure/ee460772.aspx) 
