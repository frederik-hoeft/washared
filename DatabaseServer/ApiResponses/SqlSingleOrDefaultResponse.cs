using System;
using System.Collections.Generic;
using System.Text;

namespace washared.DatabaseServer.ApiResponses
{
    public class SqlSingleOrDefaultResponse : ApiResponse
    {
        public readonly string Result;
        [Obsolete("Only used for JSON parsing. Use GetSingleOrDefaultResponse.Create() instead.")]
        public SqlSingleOrDefaultResponse(ResponseId responseId, string result, bool success)
        {
            ResponseId = responseId;
            Result = result;
            Success = success;
        }

        public static SqlSingleOrDefaultResponse Create(string result)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new SqlSingleOrDefaultResponse(ResponseId.GetSingleOrDefault, result, !string.IsNullOrEmpty(result));
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
