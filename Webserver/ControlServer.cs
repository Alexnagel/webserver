using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Webserver.Enums;
using Webserver.Interfaces;
using Webserver.Models;
using Webserver.Modules;

namespace Webserver
{
    class ControlServer : AbstractServer
    {
        private const string CERTIFICATE_PATH = @"Data\webserverCA.pfx";
        private const string CERTIFICATE_PASSWORD = "webserver";
        private const string SETTINGS_CHANGED_PAGE = @"Data\Templates\Settingschanged.html";
        private const string LOGGED_OUT_TEMPLATE = @"Data\Templates\LogoutTemplate.html";
        private const string USER_ROW = @"Data\Templates\UserRow.html";
        private const string LOG_ROW = @"Data\Templates\LogRow.html";

        // set threads
        private const int INIT_THREADS = 20;
        private const int MAX_THREADS = 20;

        // semaphores
        private static Semaphore _connectionSemaphore;

        private static IPAddress _serverIP;
        private static int       _listenPort;

        // settings modules
        private IPublicSettingsModule _publicSettingsModule;
        private IServerSettingsModule _serverSettingsModule;

        // File Module
        private FileModule _fileModule;

        // Session Module
        private SessionModule _sessionModule;

        // Log Modul
        private LogModule _logModule;

        // Dictionary for all mimetypes
        private Dictionary<string, string> _allowedMimeTypes;

        private TcpListener _tcpListener;
        private Boolean     _isRunning;

        private X509Certificate certificate;

        private MySqlModule _mySqlModule;

        private Boolean _settingsChanged;

        public ControlServer(IPublicSettingsModule settingsModule, SessionModule sessionModule, LogModule logModule) : base(logModule)
        {
            // set the semaphore
            _connectionSemaphore = new Semaphore(INIT_THREADS, MAX_THREADS);

            // Connect DB
            _mySqlModule = new MySqlModule();

            // set the settingsmodules
            _publicSettingsModule = settingsModule;
            _serverSettingsModule = new SettingsModule();

            // Set the filemodule
            _fileModule = new FileModule();

            // Set the SessionModule 
            _sessionModule = sessionModule;

            _logModule = logModule;

            // set ip adress
            _serverIP = IPAddress.Parse("127.0.0.1");

            certificate = new X509Certificate2(CERTIFICATE_PATH, CERTIFICATE_PASSWORD);

            _settingsChanged = false;

            // set allowed mimetypes
            _allowedMimeTypes = _serverSettingsModule.getAllowedMIMETypes();
            _listenPort = settingsModule.GetControlPort();

            _isRunning = true;
            _tcpListener = new TcpListener(_serverIP, _listenPort);
            ListenForClients();
        }

        public void CloseListener()
        {
            _tcpListener.Stop();
        }

        private void ListenForClients()
        {
            _tcpListener.Start();
            Console.WriteLine("Controlserver listening on: " + _serverIP + ":" + _listenPort);


            while (_isRunning)
            {
                try
                {
                    _connectionSemaphore.WaitOne();
                    TcpClient tcpclient = _tcpListener.AcceptTcpClient();

                    Thread clientThread = new Thread(new ParameterizedThreadStart(handleClient));
                    clientThread.Start(tcpclient);
                }
                catch (Exception)
                {
                    //listening stopped
                }
            }
            _publicSettingsModule.SaveSettings();
        }

        private void handleClient(object client)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            TcpClient tcpclient = (TcpClient)client;

            IPEndPoint IPep = (IPEndPoint)tcpclient.Client.RemoteEndPoint;
            String sClientIP = IPep.Address.ToString();
            
            SslStream sslStream = new SslStream(tcpclient.GetStream(), false);
            sslStream.WriteTimeout = 100;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3;

            try
            {
                sslStream.AuthenticateAsServer(certificate);
            }
            catch(Exception e)
            {
                // For stupid Chrome
                sslStream.Close();
                sslStream.Dispose();
                return;
            }

            String sBuffer = readStream(sslStream);
            Console.WriteLine(sBuffer);

            if (sBuffer.Length > 3)
            {
                // Look for HTTP request
                int iStartPos = sBuffer.IndexOf("HTTP", 1);
                string sHttpVersion = null;
                if (iStartPos >= 0)
                    sHttpVersion = sBuffer.Substring(iStartPos, 8);

                string requestType = sBuffer.Substring(0, 4).Trim();
                switch (requestType)
                {
                    case "GET": handleGetRequest(sBuffer.Substring(0, iStartPos - 1), sHttpVersion, sClientIP, sslStream); break;
                    case "POST": handlePostRequest(sBuffer, sHttpVersion, sClientIP, sslStream); break;
                    default: SendErrorPage(400, sHttpVersion, sslStream); break;
                }
            }
            // response time
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string newDate = DateTime.Now.ToString("yyyy-MM-dd HH':'mm':'ss");
            string elapsedTime = ts.Milliseconds.ToString();
            WriteLog(_serverIP.ToString(), newDate, elapsedTime, sBuffer);

            sslStream.Close();
            _connectionSemaphore.Release();

            if (_settingsChanged)
            {
                _isRunning = false;
                CloseListener();
            }
        }

        private String readStream(SslStream stream)
        {
            StreamReader streamReader = new StreamReader(stream);
            char[] resultBuffer = new char[2048];
            String readString = "";
            int read = -1;
            do
            {
                try
                {
                    read = streamReader.Read(resultBuffer, 0, resultBuffer.Length);
                    readString += new String(resultBuffer).Trim('\0');
                }
                catch (Exception e)
                {
                    // break the while 
                    break;
                }

            } while (read < 3);

            return readString;
        }

        private void handleGetRequest(String sRequest, String sHttpVersion, String sClientIP, SslStream sslStream)
        {
            User user = null;

            // Replace the escaped slashes
            sRequest.Replace("\\", "/");

            if ((sRequest.IndexOf(".") < 1) && (!sRequest.EndsWith("/")))
                sRequest += "/";

            // Get the directory
            String sRequestDirectoryName = sRequest.Substring(sRequest.IndexOf("/"), sRequest.LastIndexOf("/") - 4);
            // If directory is root then give empty string
            sRequestDirectoryName = (sRequestDirectoryName.Equals("/")) ? "" : sRequestDirectoryName;

            // Add 'control' to directory name, as it's the control server
            String sDirectoryName = _fileModule.CombinePaths("control", sRequestDirectoryName);

            // Check if localPath exists
            String sLocalPath = _fileModule.GetLocalPath(sDirectoryName);
            if (String.IsNullOrEmpty(sLocalPath))
            {
                SendErrorPage(404, sHttpVersion, sslStream);
                return;
            }

            // Check if file is given, get default file if not given
            String sRequestedFile = sRequest.Substring(sRequest.LastIndexOf("/") + 1);
            if (string.IsNullOrEmpty(sRequestedFile))
            {
                sRequestedFile = _fileModule.GetControlDefaultPage(sLocalPath);
            }

            // Check file mimetype
            String mimeType = _fileModule.GetMimeType(sRequestedFile);
            if (String.IsNullOrEmpty(mimeType))
            {
                SendErrorPage(404, sHttpVersion, sslStream);
                return;
            }

            if (!sRequestedFile.Equals("login.html") && mimeType.Equals("text/html"))
            {
                if ((user = _sessionModule.CheckIPSession(sClientIP)) == null)
                    handleGetRequest("GET /", sHttpVersion, sClientIP, sslStream);
                else if (user.UserRight == UserRights.USER && (!sRequestedFile.Equals("user.html") && !sRequestedFile.Equals("logs.html")))
                    SendErrorPage(404, sHttpVersion, sslStream);
            } 
            else if ((user = _sessionModule.CheckIPSession(sClientIP)) != null && sRequestedFile.Equals("login.html"))
            {
                if (user.UserRight == UserRights.ADMIN)
                    handleGetRequest("GET /admin.html", sHttpVersion, sClientIP, sslStream);
                else
                    handleGetRequest("GET /user.html", sHttpVersion, sClientIP, sslStream);
            }

            switch(sRequestedFile)
            {
                case "user.html":
                case "admin.html": buildAdminPage(user, sHttpVersion, sslStream); return;
                case "users.html": buildUserOverview(sHttpVersion, sslStream); return;
                case "logs.html": buildLogsPage(sHttpVersion, sslStream); return;
            }

            // File to bytes
            String sPhysicalFilePath = Path.Combine(sLocalPath, sRequestedFile);
            byte[] bFileBytes = _fileModule.FileToBytes(sPhysicalFilePath);

            // Write data to the browser
            SendHeader(sHttpVersion, mimeType, bFileBytes.Length, "200 OK", sslStream);
            SendToBrowser(bFileBytes, sslStream);
        }

        private void handlePostRequest(String sRequest, String sHttpVersion, String sClientIP, SslStream sslStream)
        {
            // Get the post data content length
            int iStartPos      = sRequest.IndexOf("Content-Length: ") + 16;
            int iEndPos        = sRequest.IndexOf('\r', iStartPos);
            int iContentLength = int.Parse(sRequest.Substring(iStartPos, iEndPos - iStartPos));

            // Get the post data string
            String sPostData = sRequest.Substring(sRequest.LastIndexOf('\n') + 1, iContentLength);
            
            // Split post data and create dictionary of that data, key = input name, value = input value
            String[] saPostData = sPostData.Split('&');
            Dictionary<String, String> dPostData = new Dictionary<String, String>();
            for (int i = 0; i < saPostData.Length; i++ )
            {
                if (!string.IsNullOrEmpty(saPostData[i]))
                {
                    String[] saSeperateData = saPostData[i].Split('=');
                    dPostData.Add(saSeperateData[0], saSeperateData[1]);
                }
            }
            
            // Get post method
            String sPostMethod = "";
            int iPostStartPos = sRequest.IndexOf("HTTP", 1);
            sRequest = sRequest.Substring(0, iPostStartPos - 1);

            // Replace the escaped slashes
            sRequest.Replace("\\", "/");

            String sRequestedFile = sRequest.Substring(sRequest.IndexOf("/"));
            if (String.IsNullOrWhiteSpace(sRequestedFile))
            {
                sPostMethod = "login";
            }
            else
            {
                sPostMethod = Path.GetFileNameWithoutExtension(sRequestedFile);
            }

            handlePostMethod(sPostMethod, dPostData, sRequestedFile, sRequest, sHttpVersion, sClientIP, sslStream);
        }

        private void handlePostMethod(String sPostMethod, Dictionary<String, String> dPostData, String sRequestedFile, String sRequest, 
            String sHttpVersion, String sClientIP, SslStream sslStream)
        {
            switch(sPostMethod)
            {
                case "info": if (dPostData.Count < 3) loginMethod(sPostMethod, dPostData, sRequestedFile, sRequest, sHttpVersion, sClientIP, sslStream); else createMethod(dPostData, sHttpVersion, sClientIP, sslStream); break; // from login to info
                case "login":           loginMethod(sPostMethod, dPostData, sRequestedFile, sRequest, sHttpVersion, sClientIP, sslStream); break;
                case "logout":          logOutMethod(sHttpVersion, sClientIP, sslStream); break; 
                case "newuser":         createMethod(dPostData, sHttpVersion, sClientIP, sslStream); break;
                case "deleteuser":      deleteMethod(dPostData, sHttpVersion, sslStream); break;
                case "save":            saveSettings(dPostData, sHttpVersion, sslStream); break; // update settings
                default: SendErrorPage(404, sHttpVersion, sslStream); break;
            }
        }

        private void createMethod(Dictionary<String,String> dPostData, String sHttpVersion, String sClientIP, SslStream sslStream)
        {
            String username = dPostData.ElementAt(0).Value;
            String password = dPostData.ElementAt(1).Value;
            String right = dPostData.ElementAt(2).Value;
            _mySqlModule.CreateUser(username, password, right);

            handleGetRequest("GET /users", sHttpVersion, sClientIP, sslStream);
        }

        private void deleteMethod(Dictionary<String,String> dPostData, String sHttpVersion, SslStream sslStream)
        {
            Boolean success = false;
            try
            {
                success = _mySqlModule.DeleteUser(int.Parse(dPostData["id"]));
            }
            catch(FormatException)
            {
                
            }
            byte[] bPage = Encoding.ASCII.GetBytes("{\"success\": \"" + success + "\"}");
            SendHeader(sHttpVersion, "application/json", bPage.Length, "200 OK", sslStream);
            SendToBrowser(bPage, sslStream);
        }

        private void loginMethod(String sPostMethod, Dictionary<String, String> dPostData, String sRequestedFile, String sRequest, String sHttpVersion, String sClientIP, SslStream sslStream)
        {
            String username = dPostData.ElementAt(0).Value;
            String password = dPostData.ElementAt(1).Value;

            Warning warning;
            User loggedInUser = _sessionModule.LoginUser(sClientIP, username, password, out warning);
            if(warning == Warning.NONE && username != null)
            {
                buildAdminPage(loggedInUser, sHttpVersion, sslStream);
            }
            else
            {
                SendErrorPage(400, sHttpVersion, sslStream);
            }
        }

        private void logOutMethod(String sHttpVersion, String sClientIP, SslStream sslStream)
        {
            _sessionModule.LogOutUser(sClientIP);

            String sLoggedoutPath = Path.Combine(Environment.CurrentDirectory, LOGGED_OUT_TEMPLATE);
            String sLoggedout = "";
            using (StreamReader sr = new StreamReader(sLoggedoutPath))
                sLoggedout = sr.ReadToEnd();

            byte[] bPage = Encoding.ASCII.GetBytes(sLoggedout);
            SendHeader(sHttpVersion, "", bPage.Length, "200 OK", sslStream);
            SendToBrowser(bPage, sslStream);
        }

        private void buildAdminPage(User user, String sHttpVersion, SslStream sslStream)
        {
            String sAdminPagePath = "";
            Dictionary<String, String> dSettings = _serverSettingsModule.GetSettings();
            if (user.UserRight == UserRights.ADMIN)
            {
                sAdminPagePath = "admin.html";
            }
            else
            {
                sAdminPagePath = "user.html";
            }

            String sLocalPath        = _fileModule.GetLocalPath("control");
            String sPhysicalFilePath = _fileModule.CombinePaths(sLocalPath, sAdminPagePath);
            String sPageHTML         = _fileModule.GetFileString(sPhysicalFilePath);

            foreach (KeyValuePair<String, String> setting in dSettings)
                sPageHTML = sPageHTML.Replace("{{" + setting.Key + "}}", setting.Value);

            byte[] bPage = Encoding.ASCII.GetBytes(sPageHTML);
            SendHeader(sHttpVersion, "", bPage.Length, "200 OK", sslStream);
            SendToBrowser(bPage, sslStream);
        }

        private void saveSettings(Dictionary<String, String> dPostdata, String sHttpVersion, SslStream sslStream)
        {
            _publicSettingsModule.SetWebPort(int.Parse(dPostdata["webport"]));
            _publicSettingsModule.SetControlPort(int.Parse(dPostdata["controlport"]));

            _publicSettingsModule.SetWebroot(HttpUtility.UrlDecode(dPostdata["webroot"]));
            _publicSettingsModule.SetDefaultPage(HttpUtility.UrlDecode(dPostdata["defaultpage"]));

            if (dPostdata.ContainsKey("directorybrowsing"))
            {
                _publicSettingsModule.SetAllowedDirectoryBrowsing(true);
            }
            else
                _publicSettingsModule.SetAllowedDirectoryBrowsing(false);

            _settingsChanged = true;

            String sSettingsChangedPath = Path.Combine(Environment.CurrentDirectory, SETTINGS_CHANGED_PAGE);
            String sSettingsChanged = _fileModule.GetFileString(sSettingsChangedPath);

            byte[] bPage = Encoding.ASCII.GetBytes(sSettingsChanged);
            SendHeader(sHttpVersion, "", bPage.Length, "200 OK", sslStream);
            SendToBrowser(bPage, sslStream);
        }

        private void buildUserOverview(String sHttpVersion, SslStream sslStream)
        {
            List<User> lUsers           = _mySqlModule.GetAllUsers();
            String sUserOverViewPath    = Path.Combine(_fileModule.GetLocalPath("control"), "users.html");
            String sUserOverView        = _fileModule.GetFileString(sUserOverViewPath);

            String sUserRowPath         = Path.Combine(Environment.CurrentDirectory, USER_ROW);
            String sUserRow             = _fileModule.GetFileString(sUserRowPath);
            String sUserRows            = "";

            if (lUsers.Count > 0)
            {
                for (int i = 0; i < lUsers.Count; i++)
                {

                    sUserRows += sUserRow.Replace("{{id}}", lUsers[i].ID.ToString()).Replace("{{username}}", lUsers[i].Username).Replace("{{userrights}}", lUsers[i].UserRight.ToString()) + "\n";
                }
            }
            else
            {
                sUserRows = "<tr><td></td><td>No Users Found</td><td></td></tr>";
            }

            sUserOverView = sUserOverView.Replace("{{users}}", sUserRows);
            byte[] bPage = Encoding.ASCII.GetBytes(sUserOverView);
            SendHeader(sHttpVersion, "", bPage.Length, "200 OK", sslStream);
            SendToBrowser(bPage, sslStream);
        }

        private void buildLogsPage(String sHttpVersion, SslStream sslStream)
        {
            String sLogOverviewPath = Path.Combine(_fileModule.GetLocalPath("control"), "logs.html");
            String sLogOverview = _fileModule.GetFileString(sLogOverviewPath);
            String sLogRowPath = Path.Combine(Environment.CurrentDirectory, LOG_ROW);
            String sLogRow = _fileModule.GetFileString(sLogRowPath);
            String sLogRows = "";

            List<string> lLogs = _logModule.GetAllLogs();
            foreach(String log in lLogs)
            {
                String[] saLog = log.Split(';');
                sLogRows += sLogRow.Replace("{{ip}}", saLog[0]).Replace("{{requestdate}}", saLog[1]).Replace("{{requesttime}}", saLog[2]).Replace("{{url}}", saLog[3]) + "\n";
            }
            sLogOverview = sLogOverview.Replace("{{logs}}", sLogRows);

            byte[] bPage = Encoding.ASCII.GetBytes(sLogOverview);
            SendHeader(sHttpVersion, "", bPage.Length, "200 OK", sslStream);
            SendToBrowser(bPage, sslStream);
        }
    }
}