using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webserver.Interfaces
{
    interface IPublicSettingsModule
    {
        void SetWebPort(int portNumber);

        int GetWebPort();

        void SetControlPort(int portNumber);

        int GetControlPort();

        Boolean GetAllowedDirectoryBrowsing();

        void SetAllowedDirectoryBrowsing(Boolean dirBrowsing);

        void SetWebroot(String rootDirectory);

        String GetWebroot();

        void SetDefaultPage(List<String> defaultPages);

        List<String> GetDefaultPage();

        List<String> GetControlDefaultPage();

        Boolean SaveSettings();
    }
}
