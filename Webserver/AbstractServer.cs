using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Webserver.Modules;

namespace Webserver
{
    abstract class AbstractServer
    {
        private LogModule _logModule;

        public AbstractServer(LogModule logModule)
        {
            _logModule = logModule;
        }
        public void SendHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, string sStatusCode, Stream networkStream)
        {
            String sBuffer = "";

            // if Mime type is not provided set default to text/html
            if (sMIMEHeader.Length == 0)
            {
                sMIMEHeader = "text/html";  // Default Mime Type is text/html
            }

            sBuffer = sBuffer + sHttpVersion + " " + sStatusCode + "\r\n";
            //sBuffer = sBuffer + ""
            sBuffer = sBuffer + "Server: C#Server\r\n";
            sBuffer = sBuffer + "Content-Type: " + sMIMEHeader + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            sBuffer = sBuffer + "Content-Length: " + iTotBytes + "\r\n\r\n";

            Byte[] bSendData = Encoding.ASCII.GetBytes(sBuffer);

            SendToBrowser(bSendData, networkStream);
        }

        public void SendToBrowser(String sSendData, Stream networkStream)
        {
            SendToBrowser(Encoding.ASCII.GetBytes(sSendData), networkStream);
        }

        public void SendToBrowser(Byte[] bSendData, Stream networkStream)
        {
            try
            {
                networkStream.Write(bSendData, 0, bSendData.Length);
                networkStream.Flush();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Occurred : {0} ", e);
            }
        }

        public void SendErrorPage(int code, string sHttpVersion, Stream networkStream)
        {
            string sErrorFolder = Path.Combine(Environment.CurrentDirectory, "Data\\Errors");
            string sErrorFile = "";
            string sErrorCode = "";

            switch (code)
            {
                case 404: sErrorFile = "404.html"; sErrorCode = "404 Not Found"; break;
                case 400: sErrorFile = "400.html"; sErrorCode = "400 Bad Request"; break;
            }

            String sErrorFilePath = Path.Combine(sErrorFolder, sErrorFile);
            StreamReader sr       = new StreamReader(sErrorFilePath);

            // Get the byte array 
            Byte[] bMessage = Encoding.ASCII.GetBytes(sr.ReadToEnd());

            SendHeader(sHttpVersion, "", bMessage.Length, sErrorCode, networkStream);
            SendToBrowser(bMessage, networkStream);
        }

        public void WriteLog(String _serverIP, String newDate, String elapsedTime, String sBuffer)
        {
            int iStartPos = sBuffer.IndexOf("HTTP", 1);
            String request = sBuffer.Substring(0, iStartPos - 1);
            String sDirectoryName = request.Substring(sBuffer.IndexOf("/"), request.LastIndexOf("/") - 3);
            String sRequestedFile = request.Substring(request.LastIndexOf("/") + 1);
            String URL = _serverIP + sDirectoryName + sRequestedFile;
            String newLog = _serverIP + ";" + newDate + ";" + elapsedTime + ";" + URL;
            _logModule.setLog(newLog);
        }
    }
}
