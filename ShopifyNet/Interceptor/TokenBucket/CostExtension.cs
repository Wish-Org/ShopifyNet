using GraphQLSharp;

namespace ShopifyNet;

public static class GraphQLCostExtension
{
    public static Cost GetCost<T>(this GraphQLResponse<T> response)
    {
        return response.GetExtension<Cost>("cost");
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
