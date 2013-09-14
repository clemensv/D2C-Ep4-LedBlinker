D2C-Ep4-LedBlinker
==================

Device to Cloud - Subscribe Episode 4 - LedBlinker Example

This sample is the evolution of the one from Episode 3, which still resides in its own, parallel repo at
https://github.com/clemensv/D2C-Ep3-LedBlinker. I'm choosing not to use branches for these snapshots to
preserve the state that matches the video episodes.  

The solution does, still, just switch an LED. 

The device gateway, which is implemented in the LedSwitcherServer project (in 
C#), has a plain TCP endpoint for the device(s) to connect to from within the local network they're in. 
The server will register and hold on to the connection and maintaining it with a ping sent every 235s 
(just under 4 minutes) to entertain NATs and the Windows Azure load-balancer on the route so that they 
don't cut the connection. We will explore this aspect and its impact on energy consumption in more detail 
in a coming episode. 

To the service consumer, the service exposes a very simple WebAPI endpoint (SwitchController) that allows 
turning the device on or off. 

New in this episode is that the WebAPI controller representing the device's "cloud-side" API and the device 
gateway are intermediated by a pair of queues. The device gateway translates the native device protocol to
to/from messages that flow on the queues. 

The "device queue" exists once for each device and holds all messages destined for that device. We will 
explore a number of pattern variations for this "mailbox" in coming episodes and the queue-per-device is 
just the simplest one. The upside of using this approach is that the device now has a stable address to drop 
messages into and that address is available even if the device is temporarily offline, which can easily 
occur when devices are connected by some form of wireless radio. In coming episodes we will also explore 
how to make these queues or other entities as devices get onboarded into the system, during "device provisioning".

The other immediate gain is that the device gateway can now scale out across multiple nodes in a load balanced
environment as connections drop and get reestablished, which we couldn't do with Episode 3's in-memory model.

The "ingress" queue is shared across devices multiple devices and uses sessions to multiplex any replies from 
the devices. The API implementation puts messages to the device onto the device queue and then waits on the 
"ingress" queue supplying a correlation key (the session-id) for the reply. The per-device processing loop 
pulls the message off the queue, sends the fitting protocol command to the device and enqueues the correlation key.
As the device replies, the correlation key is dequeued and then used as the session-id for the reply sent to the
ingress queue, which is how the pending API call gets the confirmation that the command succeeded.

IMPORTANT: Mind that this sample is (still) a step on a longer journey. It's not a best-practice example. There is
no implementation of authorization or any other secueity features as of yet. The composition is also not 
(yet) representative of how you'd put together a robust service. The goal here is to show the basic principle in 
a fairly minimal fashion.

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
