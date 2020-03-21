using System;
using System.Collections.Generic;
using System.Text;

namespace washared.DatabaseServer.ApiResponses
{
    public class SqlDataArrayResponse : ApiResponse
    {
        public readonly string[] Result;
        [Obsolete("Only used for JSON parsing. Use GetDataArrayResponse.Create() instead.")]
        public SqlDataArrayResponse(SqlResponseId responseId, string[] result, bool success)
        {
            ResponseId = responseId;
            Result = result;
            Success = success;
        }

        public static SqlDataArrayResponse Create(string[] result)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new SqlDataArrayResponse(SqlResponseId.GetDataArray, result, result.Length > 0);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
