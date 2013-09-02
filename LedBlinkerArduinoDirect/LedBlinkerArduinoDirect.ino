#include "SPI.h"
#include "Ethernet.h"
#include "WebServer.h"

#define DEBUG

static uint8_t mac[6] = { 0x90, 0xA2, 0xDA, 0x0D, 0xBC, 0xAE };
static uint8_t ip[4] = { 192, 168, 2, 107 };

static const int LED_PIN = 9;
#define PREFIX "/switch"
WebServer webserver(PREFIX, 8088);



void turnLedOn()
{
	digitalWrite(LED_PIN, HIGH);
}

void turnLedOff()
{
	digitalWrite(LED_PIN, LOW);
}

void switchCmd(WebServer &server, WebServer::ConnectionType type, char * tail, bool tailComplete)
{
#ifdef DEBUG
	Serial.println("incoming request");
#endif
	if (type == WebServer::PUT)
	{
#ifdef DEBUG
		Serial.println("PUT");
#endif
		bool parseResult;
		char name[16], value[16];
		do
		{
			parseResult = server.nextURLparam(&tail, name, sizeof(name), value, sizeof(value));
			if ( parseResult != URLPARAM_OK )
			{
				break;
			}
#ifdef DEBUG
			Serial.print(name);
			Serial.print(":");
			Serial.println(value);
#endif
			if (strcmp(name, "state") == 0 )
			{

				if (strcmp(value, "true") == 0 || 
					strcmp(value, "1") == 0)
				{
					turnLedOn();
					server.httpSuccess();
					return;
				}
				else
				{
					turnLedOff();
					server.httpSuccess();
					return;
				}
			}
		}
		while ( true );
		server.httpFail();
	}
	else
	{
		server.httpFail();
	}
}

void setup()
{
#ifdef DEBUG
	Serial.begin(115200);
	Serial.println("Start");
#endif

	Ethernet.begin(mac, ip);
	pinMode(LED_PIN, OUTPUT);

	// flash the LED to show that the program started
	turnLedOn();
	delay(500);
	turnLedOff();

	webserver.setDefaultCommand(&switchCmd); 

	/* start the server to wait for connections */
	webserver.begin();
}

void loop()
{
	// process incoming connections one at a time forever
	webserver.processConnection();

}
