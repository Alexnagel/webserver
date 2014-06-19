using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Webserver
{
    class Logger
    {
        // set threads
        private const int INIT_THREADS = 20;
        private const int MAX_THREADS = 20;

        private Semaphore _logSemaphore;

        // for all the logs
        private string[] logs;

        public Logger()
        {
            _logSemaphore = new Semaphore(INIT_THREADS, MAX_THREADS);

            logs = new string[20];
        }

        public void setLog()
        {
            if(logs.Length == 20)
            {

            }
        }

        private void showLog()
        {

        }
    }
}
