using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webserver.Interfaces
{
    interface IServerSettingsModule
    {
        Dictionary<String, String> getAllowedMIMETypes();

        List<String> getAllowedVirtualDirs();

        Dictionary<String, String> GetSettings();
    }
}
