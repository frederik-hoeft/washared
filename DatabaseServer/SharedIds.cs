using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace washared.DatabaseServer
{
    public enum SqlRequestId
    {
        Get2DArray = 0,
        GetDataArray = 1,
        GetSingleOrDefault = 2,
        ModifyData = 3
    }
    public enum SqlResponseId
    {
        Get2DArray = 0,
        GetDataArray = 1,
        GetSingleOrDefault = 2,
        ModifyData = 3
    }
}