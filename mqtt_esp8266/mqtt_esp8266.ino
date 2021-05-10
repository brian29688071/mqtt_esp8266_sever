#include <ESP8266WiFi.h>
#include <PubSubClient.h>
#include <Arduino.h>
#include "AudioFileSourceHTTPStream.h"
#include "AudioFileSourceSPIRAMBuffer.h"
#include "AudioGeneratorMP3.h"
#include "AudioOutputI2SNoDAC.h"
#include <NTPClient.h>
#include <WiFiUdp.h>
#include "string.h"
#include "stdlib.h"
const char *ssid = "OPPO R17 Pro";
const char *password = "29688071";
const char *mqtt_server = "192.168.43.44";
const char *myname = "myname";       //topic
const char *mynowplay = "mynowplay"; //topic
const char *URL = "http://192.168.43.44/mp3file/OP.mp3";
String checkchar = ",";
WiFiUDP ntpudp;
NTPClient time_nClient(ntpudp, "tw.pool.ntp.org");
WiFiClient espClient;
PubSubClient client(espClient);
boolean type = true;
AudioGeneratorMP3 *mp3;
AudioFileSourceHTTPStream *file;
AudioFileSourceSPIRAMBuffer *buff;
AudioOutputI2SNoDAC *out;
String filename;
String filename_now;
String knowplayfile = "";
long lastMsg = 0;
char msg[50];
short int value = 0;
long int PPOS = 0;
int time_n, time_sp;
boolean trigger = true;
void setup_wifi()
{
  delay(10);
  // We start by connecting to a WiFi network
  Serial.println();
  Serial.print("Connecting to ");
  Serial.println(ssid);
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);
    Serial.print(".");
  }
  randomSeed(micros());
  Serial.println("");
  Serial.println("WiFi connected");
  Serial.println("IP address: ");
  Serial.println(WiFi.localIP());
}
void callback(char *topic, byte *payload, unsigned int length) //接收回傳
{
  Serial.println(length);
  String temp = "";
  for (int i = 0; i < length; i++)
  {
    temp.concat((String)(char)payload[i]);
    if (i == 3)
    {
      if (temp == "seek")
      {
        temp = "";
        for (int i = 4; i < length; i++)
        {
          temp.concat((String)(char)payload[i]);
        }
        PPOS = atoi(temp.c_str());
        Serial.println(PPOS);
        PPOS = PPOS * 16384;
        buff->seek(PPOS, 0);
        break;
      }
      else
      {
        String filename_next = "http://192.168.43.44/mp3file/"; //file位置的頭
        String mynow_play = ",mynowplay,";
        knowplayfile = "";
        mynow_play.concat(temp);
        knowplayfile.concat(temp);
        filename_next.concat(temp);
        for (int i = 4; i < length; i++)
        {
          mynow_play.concat((String)(char)payload[i]);
          knowplayfile.concat((String)(char)payload[i]);
          filename_next.concat((String)(char)payload[i]);
        }
        client.publish(mynowplay, mynow_play.c_str());
        filename = filename_next; //指向全域變數
      }
    }
  }
}
void reconnect()
{
  // Loop until  reconnected
  while (!client.connected())
  {
    Serial.print("Attempting MQTT connection...");
    String clientId = "ESP8266Clinet-";
    clientId += String(random(0xffff), HEX);
    if (client.connect(clientId.c_str()))
    {
      Serial.println("connected");
      client.subscribe(myname);
    }
    else
    {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println(" try again in 5 seconds");
      delay(5000);
    }
  }
}
void setup()
{
  Serial.begin(115200);
  setup_wifi();
  client.setServer(mqtt_server, 1883);
  client.setCallback(callback);
  audioLogger = &Serial;
  out = new AudioOutputI2SNoDAC();
  mp3 = new AudioGeneratorMP3();
}
void loop()
{
  if (!client.connected())
  {
    reconnect();
  }
  while (!time_nClient.update())
  {
    time_nClient.forceUpdate();
  }
  client.loop();
  if (filename_now != filename) //切換部分
  {
    filename_now = filename;
    if (mp3->isRunning())
    {
      (*mp3).stop();
      (*out).stop();
      delay(1000);
      URL = filename_now.c_str();
      delete file;
      delete buff;
      file = new AudioFileSourceHTTPStream(URL);
      buff = new AudioFileSourceSPIRAMBuffer(file, 4, 131072);
      (*mp3).begin(buff, out);
      (*out).stop();
      time_n = time_nClient.getSeconds();
      time_sp = (time_n + 2);
      if (time_sp > 60)
        time_sp = time_sp % 60;
      while (trigger)
      {
        time_n = time_nClient.getSeconds();
        if (time_sp == time_n)
          trigger = false;
      }
      (*out).begin();
      printf("switchsong....");
    }
    else
    {
      URL = filename_now.c_str();
      file = new AudioFileSourceHTTPStream(URL);
      buff = new AudioFileSourceSPIRAMBuffer(file, 4, 131072);
      (*mp3).begin(buff, out);
      out->stop();
      time_n = time_nClient.getSeconds();
      Serial.println("現在:");
      Serial.print(time_n);
      time_sp = (time_n + 2);
      if (time_sp > 60)
        time_sp = time_sp % 60;
      Serial.println("開始:");
      Serial.print(time_sp);
      while (trigger)
      {
        time_n = time_nClient.getSeconds();
        Serial.println("現在:");
        Serial.print(time_n);
        if (time_sp == time_n)
          trigger = false;
        delay(200);
      }
      Serial.println("開始");
      (*out).begin();
    }
  }
  else //播放狀態
  {
    if (mp3->isRunning())
    {
      if (!mp3->loop())
        mp3->stop();
      else
      {
        if ((value % 1000) == 0)
        { //撥放狀態監測
          Serial.print("Knowplay:");
          Serial.println(knowplayfile);
          int pos = buff->getPos();
          Serial.println(pos);
          value = 0;
        }
      }
      value++;
    }
    else
    {
      //Serial.printf("MP3 done\n");
      //client.publish(mynowplay,",, no song playing know");
      delay(1000);
      value = 0;
    }
  }
}
