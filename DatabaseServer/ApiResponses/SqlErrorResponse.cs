using System;
using System.Collections.Generic;
using System.Text;

namespace washared.DatabaseServer.ApiResponses
{
    public class SqlErrorResponse : ApiResponse
    {
        public readonly string Message;
        [Obsolete("Only used for JSON parsing. Use Get2DArrayResponse.Create() instead.")]
        public SqlErrorResponse(SqlResponseId responseId, string message, bool success)
        {
            ResponseId = responseId;
            Message = message;
            Success = success;
        }

        public static SqlErrorResponse Create(string message)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new SqlErrorResponse(SqlResponseId.Error, message, false);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
