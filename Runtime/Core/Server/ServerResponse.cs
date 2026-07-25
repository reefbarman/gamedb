using System.Collections.Generic;

namespace GameDBLibrary
{
    /// <summary>
    /// ServerResponse handles parsing basic responses from the GameDB server
    /// </summary>
    [System.Obsolete(LegacyRemoteApi.Message)]
    public
        class ServerResponse
    {
        /// <summary>
        /// Handles a basic response from the GameDBServer.
        /// </summary>
        /// <param name="response">The response object to parse.</param>
        /// <returns><c>null</c> is returned if no error is found, otherwise an error message is returned</returns>
        public static string HandleBasicResponse(IDownloadHandler response)
        {
            string errorMessage = null;

            if (!string.IsNullOrEmpty(response?.GetText()))
            {
                if (JsonSerialization.Deserialize(response.GetText()) is IDictionary<string, object> resp)
                {
                    if (resp.ContainsKey("error"))
                    {
                        errorMessage = resp["error"] is IDictionary<string, object> error ? $"server error occured: {error["message"]} ({error["code"]})" : $"invalid error response received: {response.GetText()}";
                    }
                    else if (!resp.ContainsKey("success"))
                    {
                        errorMessage = $"invalid response received: {response.GetText()}";
                    }
                }
                else
                {
                    errorMessage = "invalid response received";
                }
            }
            else
            {
                errorMessage = "empty response received";
            }

            return errorMessage;
        }
    }
}
