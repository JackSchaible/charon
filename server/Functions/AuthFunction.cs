namespace API.Functions;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
public class AuthFunction
{
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogLine($"Received request: {request.HttpMethod} {request.Path}");

        if (request.Path == "/api/ping")
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = "Hello from API Function!",
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "text/plain" }
                }
            };
        
        // TODO: Handle other API routes here

        return await Task.FromResult(new APIGatewayProxyResponse
        {
            StatusCode = 404,
            Body = "Not Found",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "text/plain" }
            }
        });
    }
}