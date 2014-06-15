using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webserver.Interfaces
{
    interface IPublicSettingsModule
    {
        void setWebPort(int portNumber);

        int getWebPort();

        void setControlPort(int portNumber);

        int getControlPort();

        void setWebroot(String rootDirectory);

        String getWebroot();

        void setDefaultPage(List<String> defaultPages);

        List<String> getDefaultPage();

        List<String> getControlDefaultPage();

        Boolean saveSettings();
    }
}
