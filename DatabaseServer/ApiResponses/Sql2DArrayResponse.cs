using System;
using System.Collections.Generic;
using System.Text;

namespace washared.DatabaseServer.ApiResponses
{
    public class Sql2DArrayResponse : ApiResponse
    {
        public readonly string[][] Result;
        [Obsolete("Only used for JSON parsing. Use Get2DArrayResponse.Create() instead.")]
        public Sql2DArrayResponse(ResponseId responseId, string[][] result, bool success)
        {
            ResponseId = responseId;
            Result = result;
            Success = success;
        }

        public static Sql2DArrayResponse Create(string[][] result)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new Sql2DArrayResponse(ResponseId.Get2DArray, result, result.Length > 0);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
