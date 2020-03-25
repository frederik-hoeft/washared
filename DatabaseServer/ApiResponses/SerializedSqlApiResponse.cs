using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace washared.DatabaseServer.ApiResponses
{
    public class SerializedSqlApiResponse
    {
        public readonly SqlResponseId ResponseId;
        public readonly string Json;
        [Obsolete("Only used for JSON parsing. Use SerializedApiResponse.Create() instead.")]
        public SerializedSqlApiResponse(SqlResponseId responseId, string json)
        {
            ResponseId = responseId;
            Json = json;
        }

        public static SerializedSqlApiResponse Create(ApiResponse apiResponse)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new SerializedSqlApiResponse(apiResponse.ResponseId, JsonConvert.SerializeObject(apiResponse));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        public ApiResponse Deserialize()
        {
            return ResponseId switch
            {
                SqlResponseId.Error => JsonConvert.DeserializeObject<SqlErrorResponse>(Json),
                SqlResponseId.Get2DArray => JsonConvert.DeserializeObject<Sql2DArrayResponse>(Json),
                SqlResponseId.GetDataArray => JsonConvert.DeserializeObject<SqlDataArrayResponse>(Json),
                SqlResponseId.GetSingleOrDefault => JsonConvert.DeserializeObject<SqlSingleOrDefaultResponse>(Json),
                SqlResponseId.ModifyData => JsonConvert.DeserializeObject<SqlModifyDataResponse>(Json),
                _ => null
            };
        }
    }
}
