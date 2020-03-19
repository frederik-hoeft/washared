using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace washared.DatabaseServer.ApiResponses
{
    public abstract class ApiResponse
    {
        public ResponseId ResponseId;
        public bool Success;

        /// <summary>
        /// Serializes the current object to a Json string.
        /// </summary>
        /// <returns>The current object as a JSON string.</returns>
        public virtual string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
