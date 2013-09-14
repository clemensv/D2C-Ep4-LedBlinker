#include <SPI.h>
#include <Ethernet.h>

#define DEBUG

// Enter a MAC address and IP address for your controller below.
// The IP address will be dependent on your local network:
static uint8_t mac[6] = { 0x90, 0xA2, 0xDA, 0x0D, 0xBC, 0xAE };
static const byte deviceId[] = { 0x00, 0x00, 0x00, 0x01 };
static const uint8_t ACK = 0x01;
static const int LED_PIN = 9;
int connected = 0;
EthernetClient client;


void setup() 
{
#ifdef DEBUG
	Serial.begin(115200);
#endif
	Ethernet.begin(mac);
	pinMode(LED_PIN, OUTPUT);

	// flash the LED to show that the program started
	turnLedOn();
	delay(500);
	turnLedOff();
}

void turnLedOn()
{
	digitalWrite(LED_PIN, HIGH);
}

void turnLedOff()
{
	digitalWrite(LED_PIN, LOW);
}

void loop() 
{
	if ( !connected || !client.connected() )
	{
		//char* host = "cv-ledblinker.cloudapp.net";
		char* host = "192.168.2.156";

		client.setTimeout(10000);
		connected = client.connect(host, 10100);
		if ( connected ) 
		{
#ifdef DEBUG
			Serial.println("connected");
#endif
			client.write(deviceId, sizeof(deviceId));
		}
	}

	if ( connected )
	{
		byte buf[16];
		int readResult = client.read(buf, 1);
		if ( readResult == 0 ) 
		{
			connected = false;
		}
		else if ( readResult == 1 )
		{
#ifdef DEBUG
			Serial.print("read ");
			Serial.println(buf[0]);
#endif
			switch ( buf[0] )
			{
			case 0 :
				// ignore, this is a ping 
				break;
			case 1:
				// turn LED on
				turnLedOn();
				client.write(ACK);
				break;
			case 2: 
				// turn LED off 
				turnLedOff();
				client.write(ACK);
				break;
			}
		}
	}
}