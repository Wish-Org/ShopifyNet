using GraphQLSharp;

namespace ShopifyNet;

public static class Extensions
{
    public static Cost GetCost<T>(this GraphQLResponse<T> response)
    {
        return response.GetExtension<Cost>("cost");
    }

    public static string GetRequestId<T>(this GraphQLResponse<T> response)
    {
        return response.HttpResponse.GetRequestId();
    }

    public static string GetRequestId(this GraphQLException ex)
    {
        return ex.HttpResponse?.GetRequestId();
    }

    public static string GetRequestId(this HttpResponse response)
    {
        return response.Headers.TryGetValues("X-Request-Id", out var headerValues) ? headerValues.First() : null;
    }

    public static string GetCode(this GraphQLError error)
    {
        return error.GetExtension<string>("code");
    }

    public static bool IsThrottled(this GraphQLResponse response)
    {
        return response.errors != null && response.errors.Any(e => e.GetCode() == "THROTTLED");
    }
}
public class Cost
{
    public int requestedQueryCost { get; set; }

    //main be null if query was throttled
    public int? actualQueryCost { get; set; }

    public ThrottleStatus throttleStatus { get; set; }

    public CostField[] fields { get; set; }

    public class ThrottleStatus
    {
        //not sure why but API returns decimals instead of int
        public decimal maximumAvailable { get; set; }
        public decimal currentlyAvailable { get; set; }
        public decimal restoreRate { get; set; }
    }

    public class CostField
    {
        public string[] path { get; set; }
        public int? definedCost { get; set; }
        public int? requestedTotalCost { get; set; }
        public int? requestedChildrenCost { get; set; }
    }
}
