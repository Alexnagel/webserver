using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Webserver.Enums
{
    public enum Warning
    {
        IP_BLOCKED,
        INCORRECT_VALUES,
        SESSION_EXPIRED,
        NONE
    }

    public enum UserRights
    {
        FAILED = 0,
        ADMIN,
        USER
    }
}
