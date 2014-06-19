using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Webserver.Modules
{
    class LogModule
    {
        // for all the logs
        private string[] logs;
        private const int SIZE = 20;

        private int count;
        private int getPosition, putPosition;

        private const string LOG_FILE = @"Data\Logging\log.txt";
        public LogModule()
        {
            
        }

        public void Init()
        {
            logs = new string[SIZE];

            // new thread for write log
            Thread _writeThread = new Thread(new ThreadStart(writeLog));
            _writeThread.Start();

        }

        public void setLog(String newLog)
        {
            push(newLog);
        }

        public List<string> GetAllLogs()
        {
            String readLine = "";
            List<string> output = new List<string>();
            StreamReader sr = new StreamReader(Path.Combine(Environment.CurrentDirectory, LOG_FILE));
            while((readLine = sr.ReadLine()) != null)
            {
                output.Add(readLine);
            }
            return output;
        }

        private void push(String newLog)
        {
            Monitor.Enter(logs);
            try
            {
                while(count == logs.Length)
                {
                    Monitor.Wait(logs);
                }

                logs[putPosition] = newLog;
                putPosition = (putPosition + 1) % SIZE;
                count++;
            }
            finally
            {
                Monitor.Pulse(logs);
                Monitor.Exit(logs);
            }
            
        }

        private String pop()
        {
            Monitor.Enter(logs);
            try
            {
                while (count <= 0)
                {
                    Monitor.Wait(logs);
                }

                String entry = logs[getPosition];
                getPosition = (getPosition + 1) % SIZE;
                count--;

                return entry;
            }
            finally
            {
                Monitor.Pulse(logs);
                Monitor.Exit(logs);
            }
        }

        private void writeLog()
        {
            while(true)
            {
                Monitor.Enter(logs);
                StreamWriter sw = null;
                try
                {
                    while (count == 0)
                    {
                        Monitor.Wait(logs);
                    }

                    String entry = pop();
                    sw = new StreamWriter(Path.Combine(Environment.CurrentDirectory, LOG_FILE), true);
                    sw.WriteLine(entry);
                    sw.Close();
                }
                catch(IOException e)
                {
                    if (sw != null)
                        sw.Close();
                }
                finally
                {
                    Monitor.Pulse(logs);
                    Monitor.Exit(logs);
                }
            }
        }
    }
}
