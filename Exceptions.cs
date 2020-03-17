using System;
using System.Collections.Generic;
using System.Text;

namespace washared
{
    public class ConnectionDroppedException : Exception
    {
        public ConnectionDroppedException() { }
        public ConnectionDroppedException(string message) : base(message) { }
    }
}
