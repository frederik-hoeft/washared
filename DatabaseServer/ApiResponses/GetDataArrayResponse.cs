using System;
using System.Collections.Generic;
using System.Text;

namespace washared.DatabaseServer.ApiResponses
{
    public class GetDataArrayResponse : ApiResponse
    {
        public readonly string[] Result;
        [Obsolete("Only used for JSON parsing. Use GetDataArrayResponse.Create() instead.")]
        public GetDataArrayResponse(ResponseId responseId, string[] result, bool success)
        {
            ResponseId = responseId;
            Result = result;
            Success = success;
        }

        public static GetDataArrayResponse Create(string[] result)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new GetDataArrayResponse(ResponseId.GetDataArray, result, result.Length > 0);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
